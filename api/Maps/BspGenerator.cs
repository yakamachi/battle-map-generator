namespace BattleMapGenerator.Api.Maps;

// Binary space partitioning: the map is split into exactly as many leaves as rooms were asked for,
// each leaf gets one room, and sibling subtrees are joined by a 1-cell corridor found with a
// turn-averse shortest path. On a boss fight one leaf is reserved for the arena before the rest is split.
//
// Layout rules that make the guarantees hold by construction:
// - A room's wall ring (the cells 8-adjacent to its floor) stays at least one cell inside its leaf,
//   so rings of different rooms never touch and a free lane runs along every split line.
// - Corridors use only free interior cells (never a ring, never the map edge) and cross a ring only
//   through a door, straight from the room's floor to the outside, so each door has walkable cells
//   on two opposite sides and ring walls on the other two.
// - A door is never placed next to another door on the same ring, or on a ring corner.
// - Finally every ring cell that is not a door and every empty cell touching a corridor becomes a wall.
public static class BspGenerator
{
    private const int MinLeaf = 7;
    private const int MinRoom = 3;
    private const int RoomMargin = MapSize.RoomMargin;

    // Path costs: corridors reuse existing corridors, prefer straight runs, and avoid running
    // alongside another corridor (which would read as a 2-wide corridor). One-cell-wide corridors
    // hold through these costs, not by construction: the tests check it, so recheck after a change here.
    private const int NewCellCost = 2;
    private const int ExistingCorridorCost = 1;
    private const int TurnCost = 3;
    private const int AlongsideCorridorCost = 4;

    private static readonly (int Dx, int Dy)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    // The map's size follows from the parameters (see MapSize.For).
    public static GeneratedMap Generate(uint seed, MapParameters parameters)
    {
        var isBoss = parameters.Encounter == EncounterType.Boss;
        if (isBoss && parameters.BossSize is null)
        {
            throw new ArgumentException("A boss fight needs a boss size.", nameof(parameters));
        }

        var bossSize = isBoss ? parameters.BossSize : null;
        var (width, height) = MapSize.For(parameters.RoomCount, bossSize);
        return Generate(seed, width, height, parameters.RoomCount, bossSize);
    }

    public static GeneratedMap Generate(uint seed, int width, int height,
        int roomCount = MapSize.DefaultRoomCount, BossSize? bossSize = null)
    {
        if (width < MapSize.MinWidth || width > MapSize.MaxWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width,
                $"Width must be between {MapSize.MinWidth} and {MapSize.MaxWidth}.");
        }
        if (height < MapSize.MinHeight || height > MapSize.MaxHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height,
                $"Height must be between {MapSize.MinHeight} and {MapSize.MaxHeight}.");
        }
        if (roomCount < MapSize.MinRoomCount || roomCount > MapSize.MaxRoomCount)
        {
            throw new ArgumentOutOfRangeException(nameof(roomCount), roomCount,
                $"Room count must be between {MapSize.MinRoomCount} and {MapSize.MaxRoomCount}.");
        }

        var rng = new Prng(seed);
        var layout = new Layout(width, height);
        var root = new Node(new Rect(0, 0, width, height));
        var leaves = new List<Node> { root };

        // The arena is placed first, so every other room can be kept smaller than it.
        var maxRoomArea = int.MaxValue;
        if (bossSize is { } boss)
        {
            var side = MapSize.MinArenaSide(boss);
            var arena = ReserveArena(root, side, roomCount, rng, leaves);
            SplitInto(leaves, roomCount - 1, roomCount, rng);
            PlaceRoom(arena, rng, layout, side, int.MaxValue, RoomKind.BossArena);
            var arenaRoom = layout.Rooms[arena.RoomIndex];
            maxRoomArea = arenaRoom.Width * arenaRoom.Height - 1;
        }
        else
        {
            SplitInto(leaves, roomCount, roomCount, rng);
        }

        PlaceRooms(root, rng, layout, maxRoomArea);
        Connect(root, layout);
        layout.BuildWalls();

        var parameters = new MapParameters(roomCount,
            bossSize is null ? EncounterType.Skirmish : EncounterType.Boss, bossSize);
        return new GeneratedMap(seed, parameters, width, height, layout.Cells, [.. layout.Rooms]);
    }

    // Cuts a full-height strip just wide enough for the arena off the left or right side of the map.
    // When the strip is high enough and more than two rooms were asked for, the strip is cut again
    // and its other part goes back to the leaves to split. Returns the arena's leaf; leaves ends up
    // holding every other leaf.
    private static Node ReserveArena(Node root, int side, int roomCount, Prng rng, List<Node> leaves)
    {
        var area = root.Area;
        var arenaLeaf = side + 2 * RoomMargin;
        if (area.Width < arenaLeaf + MinLeaf || area.Height < arenaLeaf)
        {
            throw new ArgumentOutOfRangeException("width",
                $"A {area.Width}x{area.Height} map cannot hold a boss arena with a {side}x{side} floor next to another room.");
        }

        var rest = new Node(area with { Width = area.Width - arenaLeaf });
        var strip = new Node(area with { X = area.X + area.Width - arenaLeaf, Width = arenaLeaf });
        if (rng.NextInt(0, 2) == 0)
        {
            strip = new Node(area with { Width = arenaLeaf });
            rest = new Node(area with { X = area.X + arenaLeaf, Width = area.Width - arenaLeaf });
            root.SetChildren(strip, rest);
        }
        else
        {
            root.SetChildren(rest, strip);
        }

        leaves.Clear();
        leaves.Add(rest);
        if (roomCount < 3 || area.Height < arenaLeaf + MinLeaf)
        {
            return strip;
        }

        // The arena's leaf stays close to square, so the arena does not turn into a hall.
        var maxHeight = Math.Min(area.Height - MinLeaf, arenaLeaf + side / 2);
        var height = rng.NextInt(arenaLeaf, maxHeight + 1);
        Node arena;
        if (rng.NextInt(0, 2) == 0)
        {
            arena = new Node(strip.Area with { Height = height });
            var spare = new Node(strip.Area with { Y = strip.Area.Y + height, Height = area.Height - height });
            strip.SetChildren(arena, spare);
            leaves.Add(spare);
        }
        else
        {
            var spare = new Node(strip.Area with { Height = area.Height - height });
            arena = new Node(strip.Area with { Y = strip.Area.Y + area.Height - height, Height = height });
            strip.SetChildren(spare, arena);
            leaves.Add(spare);
        }
        return arena;
    }

    // Splits the largest leaf again and again until there are exactly target leaves.
    //
    // A leaf's capacity is how many MinLeaf-sized leaves it can still be cut into. A cut may lose
    // capacity only while the total stays at or above the target, and a cut at a multiple of MinLeaf
    // never loses any, so the target is always reached when the starting capacity allows it.
    private static void SplitInto(List<Node> leaves, int target, int roomCount, Prng rng)
    {
        var capacity = 0;
        foreach (var leaf in leaves)
        {
            capacity += Capacity(leaf.Area.Width, leaf.Area.Height);
        }
        if (capacity < target || leaves.Count > target)
        {
            throw new ArgumentOutOfRangeException("width", $"The map is too small to hold {roomCount} rooms.");
        }

        var cuts = new List<int>();
        while (leaves.Count < target)
        {
            // Largest area first; equal areas are broken by position (top first, then left), which
            // is unique because leaves never overlap. The list's own order never decides.
            var pick = -1;
            for (var i = 0; i < leaves.Count; i++)
            {
                var candidate = leaves[i].Area;
                if (candidate.Width < 2 * MinLeaf && candidate.Height < 2 * MinLeaf)
                {
                    continue;
                }
                if (pick < 0 || ComesBefore(candidate, leaves[pick].Area))
                {
                    pick = i;
                }
            }

            var node = leaves[pick];
            var area = node.Area;
            var canSplitVertically = area.Width >= 2 * MinLeaf;
            var canSplitHorizontally = area.Height >= 2 * MinLeaf;
            bool vertical;
            if (canSplitVertically && canSplitHorizontally)
            {
                // Cut across the longer side so leaves stay roughly square.
                vertical = 4 * area.Width > 5 * area.Height
                    || (4 * area.Height <= 5 * area.Width && rng.NextInt(0, 2) == 0);
            }
            else
            {
                vertical = canSplitVertically;
            }

            var length = vertical ? area.Width : area.Height;
            var across = vertical ? area.Height : area.Width;
            cuts.Clear();
            for (var candidate = MinLeaf; candidate <= length - MinLeaf; candidate++)
            {
                if (capacity - LostCapacity(length, across, candidate) >= target)
                {
                    cuts.Add(candidate);
                }
            }

            var at = cuts[rng.NextInt(0, cuts.Count)];
            capacity -= LostCapacity(length, across, at);
            var first = new Node(vertical ? area with { Width = at } : area with { Height = at });
            var second = new Node(vertical
                ? area with { X = area.X + at, Width = area.Width - at }
                : area with { Y = area.Y + at, Height = area.Height - at });
            node.SetChildren(first, second);
            leaves[pick] = first;
            leaves.Add(second);
        }
    }

    private static bool ComesBefore(Rect a, Rect b)
    {
        var areaA = a.Width * a.Height;
        var areaB = b.Width * b.Height;
        if (areaA != areaB)
        {
            return areaA > areaB;
        }
        return a.Y != b.Y ? a.Y < b.Y : a.X < b.X;
    }

    private static int Capacity(int width, int height) => (width / MinLeaf) * (height / MinLeaf);

    private static int LostCapacity(int length, int across, int at) =>
        (length / MinLeaf - at / MinLeaf - (length - at) / MinLeaf) * (across / MinLeaf);

    private static void PlaceRooms(Node node, Prng rng, Layout layout, int maxArea)
    {
        if (node.Left is not null && node.Right is not null)
        {
            PlaceRooms(node.Left, rng, layout, maxArea);
            PlaceRooms(node.Right, rng, layout, maxArea);
            return;
        }

        // The arena's leaf already has its room.
        if (node.RoomIndex < 0)
        {
            PlaceRoom(node, rng, layout, MinRoom, maxArea, RoomKind.Room);
        }
    }

    private static void PlaceRoom(Node node, Prng rng, Layout layout, int minSide, int maxArea, RoomKind kind)
    {
        var leaf = node.Area;
        var availableWidth = leaf.Width - 2 * RoomMargin;
        var availableHeight = leaf.Height - 2 * RoomMargin;
        var width = rng.NextInt(Math.Max(minSide, availableWidth / 2), availableWidth + 1);
        var height = rng.NextInt(Math.Max(minSide, availableHeight / 2), availableHeight + 1);

        // On a boss fight every other room stays smaller than the arena: trim the longer side
        // (the width when both are equal) until it is.
        while (width * height > maxArea)
        {
            if (width >= height)
            {
                width--;
            }
            else
            {
                height--;
            }
        }

        var x = leaf.X + RoomMargin + rng.NextInt(0, availableWidth - width + 1);
        var y = leaf.Y + RoomMargin + rng.NextInt(0, availableHeight - height + 1);
        node.RoomIndex = layout.AddRoom(new Room(x, y, width, height, kind));
    }

    // Joins the two subtrees of every node through their closest pair of rooms. Every leaf is a
    // child of some node, so every room gets a door, and the whole tree ends up connected.
    private static List<int> Connect(Node node, Layout layout)
    {
        if (node.Left is null || node.Right is null)
        {
            return [node.RoomIndex];
        }

        var left = Connect(node.Left, layout);
        var right = Connect(node.Right, layout);

        // Equally close pairs keep the first one in tree order (left before right, outer loop first).
        var bestA = left[0];
        var bestB = right[0];
        var bestDistance = int.MaxValue;
        foreach (var a in left)
        {
            foreach (var b in right)
            {
                var distance = CenterDistance(layout.Rooms[a], layout.Rooms[b]);
                if (distance < bestDistance)
                {
                    (bestA, bestB, bestDistance) = (a, b, distance);
                }
            }
        }

        CarvePath(bestA, bestB, layout);
        return [.. left, .. right];
    }

    private static int CenterDistance(Room a, Room b)
    {
        // Doubled centres keep the arithmetic in integers.
        var dx = (2 * a.X + a.Width) - (2 * b.X + b.Width);
        var dy = (2 * a.Y + a.Height) - (2 * b.Y + b.Height);
        return dx * dx + dy * dy;
    }

    // Dijkstra over (cell, direction of arrival) from every floor cell of room a to any floor cell
    // of room b. The direction lets a door be crossed only straight through and makes turns cost more.
    private static void CarvePath(int from, int to, Layout layout)
    {
        var width = layout.Width;
        var stateCount = layout.Cells.Length * Directions.Length;
        var cost = new int[stateCount];
        var previous = new int[stateCount];
        Array.Fill(cost, int.MaxValue);
        Array.Fill(previous, -1);
        var queue = new PriorityQueue<int, long>();

        var source = layout.Rooms[from];
        for (var y = source.Y; y < source.Y + source.Height; y++)
        {
            for (var x = source.X; x < source.X + source.Width; x++)
            {
                for (var d = 0; d < Directions.Length; d++)
                {
                    var state = (y * width + x) * Directions.Length + d;
                    cost[state] = 0;
                    queue.Enqueue(state, Priority(0, state));
                }
            }
        }

        var end = -1;
        while (queue.TryDequeue(out var state, out var priority))
        {
            var stateCost = (int)(priority >> 32);
            if (stateCost != cost[state])
            {
                continue;
            }

            var cell = state / Directions.Length;
            var arrival = state % Directions.Length;
            var x = cell % width;
            var y = cell / width;

            if (layout.FloorOwner[cell] == to)
            {
                end = state;
                break;
            }

            for (var d = 0; d < Directions.Length; d++)
            {
                var next = Step(cell, d, x, y, layout);
                if (next < 0)
                {
                    continue;
                }

                int stepCost;
                if (layout.FloorOwner[cell] == from)
                {
                    // Leave the source room only through a door on its own ring.
                    if (layout.RingOwner[next] != from || !layout.CanBeDoor(next))
                    {
                        continue;
                    }
                    stepCost = 1;
                }
                else if (layout.RingOwner[cell] >= 0)
                {
                    // A door is crossed straight: into the corridor, or into the target's floor.
                    if (d != arrival)
                    {
                        continue;
                    }
                    var entersTarget = layout.FloorOwner[next] == to;
                    if (!entersTarget && !layout.IsFree(next))
                    {
                        continue;
                    }
                    stepCost = entersTarget ? 0 : StepCost(next, layout);
                }
                else if (layout.IsFree(next))
                {
                    stepCost = StepCost(next, layout) + (d == arrival ? 0 : TurnCost);
                }
                else if (layout.RingOwner[next] == to && layout.CanBeDoor(next))
                {
                    stepCost = 1 + (d == arrival ? 0 : TurnCost);
                }
                else
                {
                    continue;
                }

                var nextState = next * Directions.Length + d;
                var nextCost = stateCost + stepCost;
                if (nextCost < cost[nextState])
                {
                    cost[nextState] = nextCost;
                    previous[nextState] = state;
                    queue.Enqueue(nextState, Priority(nextCost, nextState));
                }
            }
        }

        if (end < 0)
        {
            // The free lanes along every split line make this unreachable; fail loudly if a change breaks that.
            throw new InvalidOperationException($"No corridor could join room {from} to room {to}.");
        }

        for (var state = previous[end]; state >= 0; state = previous[state])
        {
            var cell = state / Directions.Length;
            if (layout.RingOwner[cell] >= 0)
            {
                layout.Cells[cell] = CellKind.Door;
            }
            else if (layout.FloorOwner[cell] < 0)
            {
                layout.Cells[cell] = CellKind.Corridor;
            }
        }
    }

    // Equal costs are broken by the lower state index. PriorityQueue does not define the order of
    // equal priorities, and that order could change between .NET versions and with it which corridor
    // a seed produces. A state is only re-queued at a strictly lower cost, so every key is unique and
    // the dequeue order is fully defined by this code.
    private static long Priority(int cost, int state) => ((long)cost << 32) | (uint)state;

    private static int StepCost(int cell, Layout layout)
    {
        if (layout.Cells[cell] == CellKind.Corridor)
        {
            return ExistingCorridorCost;
        }
        return layout.TouchesCorridor(cell) ? NewCellCost + AlongsideCorridorCost : NewCellCost;
    }

    private static int Step(int cell, int direction, int x, int y, Layout layout)
    {
        var (dx, dy) = Directions[direction];
        var nx = x + dx;
        var ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= layout.Width || ny >= layout.Height)
        {
            return -1;
        }
        return cell + dy * layout.Width + dx;
    }

    private readonly record struct Rect(int X, int Y, int Width, int Height);

    private sealed class Node(Rect area)
    {
        public Rect Area { get; } = area;
        public Node? Left { get; private set; }
        public Node? Right { get; private set; }
        public int RoomIndex { get; set; } = -1;

        public void SetChildren(Node left, Node right)
        {
            Left = left;
            Right = right;
        }
    }

    private sealed class Layout
    {
        public Layout(int width, int height)
        {
            Width = width;
            Height = height;
            Cells = new CellKind[width * height];
            FloorOwner = new int[width * height];
            RingOwner = new int[width * height];
            Array.Fill(FloorOwner, -1);
            Array.Fill(RingOwner, -1);
        }

        public int Width { get; }
        public int Height { get; }
        public CellKind[] Cells { get; }
        public List<Room> Rooms { get; } = [];

        // Which room's floor, or which room's wall ring, a cell belongs to (-1 for none).
        public int[] FloorOwner { get; }
        public int[] RingOwner { get; }

        public int AddRoom(Room room)
        {
            var index = Rooms.Count;
            Rooms.Add(room);
            for (var y = room.Y - 1; y <= room.Y + room.Height; y++)
            {
                for (var x = room.X - 1; x <= room.X + room.Width; x++)
                {
                    var cell = y * Width + x;
                    var inside = x >= room.X && x < room.X + room.Width && y >= room.Y && y < room.Y + room.Height;
                    if (inside)
                    {
                        FloorOwner[cell] = index;
                        Cells[cell] = room.Kind == RoomKind.BossArena ? CellKind.BossArena : CellKind.Floor;
                    }
                    else
                    {
                        RingOwner[cell] = index;
                    }
                }
            }
            return index;
        }

        // Corridor space: inside the map edge and outside every room and wall ring.
        public bool IsFree(int cell)
        {
            var x = cell % Width;
            var y = cell / Width;
            return x > 0 && y > 0 && x < Width - 1 && y < Height - 1
                && FloorOwner[cell] < 0 && RingOwner[cell] < 0;
        }

        // A ring cell off the corners with no door beside it (or a door already, which can be reused).
        public bool CanBeDoor(int cell)
        {
            if (Cells[cell] == CellKind.Door)
            {
                return true;
            }

            var room = Rooms[RingOwner[cell]];
            var x = cell % Width;
            var y = cell / Width;
            var onVerticalSide = (x == room.X - 1 || x == room.X + room.Width) && y >= room.Y && y < room.Y + room.Height;
            var onHorizontalSide = (y == room.Y - 1 || y == room.Y + room.Height) && x >= room.X && x < room.X + room.Width;
            if (!onVerticalSide && !onHorizontalSide)
            {
                return false;
            }

            return Cells[cell - 1] != CellKind.Door && Cells[cell + 1] != CellKind.Door
                && Cells[cell - Width] != CellKind.Door && Cells[cell + Width] != CellKind.Door;
        }

        public bool TouchesCorridor(int cell) =>
            Cells[cell - 1] == CellKind.Corridor || Cells[cell + 1] == CellKind.Corridor
            || Cells[cell - Width] == CellKind.Corridor || Cells[cell + Width] == CellKind.Corridor;

        public void BuildWalls()
        {
            for (var cell = 0; cell < Cells.Length; cell++)
            {
                if (RingOwner[cell] >= 0 && Cells[cell] != CellKind.Door)
                {
                    Cells[cell] = CellKind.Wall;
                }
            }

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (Cells[y * Width + x] == CellKind.Void && TouchesCorridorAround(x, y))
                    {
                        Cells[y * Width + x] = CellKind.Wall;
                    }
                }
            }
        }

        private bool TouchesCorridorAround(int x, int y)
        {
            for (var ny = Math.Max(0, y - 1); ny <= Math.Min(Height - 1, y + 1); ny++)
            {
                for (var nx = Math.Max(0, x - 1); nx <= Math.Min(Width - 1, x + 1); nx++)
                {
                    if (Cells[ny * Width + nx] == CellKind.Corridor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
