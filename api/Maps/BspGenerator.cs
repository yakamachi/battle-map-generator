namespace battle_map_generator_api.Maps;

// Binary space partitioning: the map is split recursively into leaves, each leaf gets one room,
// and sibling subtrees are joined by a 1-cell corridor found with a turn-averse shortest path.
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

    // Distance from a leaf's edge to its room's floor: one free lane plus the wall ring.
    private const int RoomMargin = 2;

    // Leaves up to this size may stop splitting early, so layouts vary between seeds.
    private const int OptionalSplitSize = 16;

    // Path costs: corridors reuse existing corridors, prefer straight runs, and avoid running
    // alongside another corridor (which would read as a 2-wide corridor).
    private const int NewCellCost = 2;
    private const int ExistingCorridorCost = 1;
    private const int TurnCost = 3;
    private const int AlongsideCorridorCost = 4;

    private static readonly (int Dx, int Dy)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    public static GeneratedMap Generate(uint seed, int width, int height)
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

        var rng = new Prng(seed);
        var layout = new Layout(width, height);
        var root = Split(new Rect(0, 0, width, height), rng, isRoot: true);
        PlaceRooms(root, rng, layout);
        Connect(root, layout);
        layout.BuildWalls();

        return new GeneratedMap(seed, width, height, layout.Cells, [.. layout.Rooms]);
    }

    private static Node Split(Rect area, Prng rng, bool isRoot)
    {
        var canSplitVertically = area.Width >= 2 * MinLeaf;
        var canSplitHorizontally = area.Height >= 2 * MinLeaf;
        if (!canSplitVertically && !canSplitHorizontally)
        {
            return new Node(area);
        }
        // The root always splits: a lone room would have no corridor and so no door.
        if (!isRoot && area.Width <= OptionalSplitSize && area.Height <= OptionalSplitSize && rng.NextInt(0, 4) == 0)
        {
            return new Node(area);
        }

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

        if (vertical)
        {
            var at = rng.NextInt(MinLeaf, area.Width - MinLeaf + 1);
            return new Node(area,
                Split(area with { Width = at }, rng, isRoot: false),
                Split(area with { X = area.X + at, Width = area.Width - at }, rng, isRoot: false));
        }
        else
        {
            var at = rng.NextInt(MinLeaf, area.Height - MinLeaf + 1);
            return new Node(area,
                Split(area with { Height = at }, rng, isRoot: false),
                Split(area with { Y = area.Y + at, Height = area.Height - at }, rng, isRoot: false));
        }
    }

    private static void PlaceRooms(Node node, Prng rng, Layout layout)
    {
        if (node.Left is not null && node.Right is not null)
        {
            PlaceRooms(node.Left, rng, layout);
            PlaceRooms(node.Right, rng, layout);
            return;
        }

        var leaf = node.Area;
        var availableWidth = leaf.Width - 2 * RoomMargin;
        var availableHeight = leaf.Height - 2 * RoomMargin;
        var width = rng.NextInt(Math.Max(MinRoom, availableWidth / 2), availableWidth + 1);
        var height = rng.NextInt(Math.Max(MinRoom, availableHeight / 2), availableHeight + 1);
        var x = leaf.X + RoomMargin + rng.NextInt(0, availableWidth - width + 1);
        var y = leaf.Y + RoomMargin + rng.NextInt(0, availableHeight - height + 1);
        node.RoomIndex = layout.AddRoom(new Room(x, y, width, height));
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
        var queue = new PriorityQueue<int, int>();

        var source = layout.Rooms[from];
        for (var y = source.Y; y < source.Y + source.Height; y++)
        {
            for (var x = source.X; x < source.X + source.Width; x++)
            {
                for (var d = 0; d < Directions.Length; d++)
                {
                    var state = (y * width + x) * Directions.Length + d;
                    cost[state] = 0;
                    queue.Enqueue(state, 0);
                }
            }
        }

        var end = -1;
        while (queue.TryDequeue(out var state, out var stateCost))
        {
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
                    queue.Enqueue(nextState, nextCost);
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

    private sealed class Node(Rect area, Node? left = null, Node? right = null)
    {
        public Rect Area { get; } = area;
        public Node? Left { get; } = left;
        public Node? Right { get; } = right;
        public int RoomIndex { get; set; } = -1;
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
                        Cells[cell] = CellKind.Floor;
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
