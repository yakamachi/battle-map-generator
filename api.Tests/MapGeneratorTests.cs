using battle_map_generator_api.Maps;

namespace battle_map_generator_api.Tests;

public sealed class MapGeneratorTests
{
    private const int SeedCount = 200;

    public static TheoryData<int, int> Sizes => new()
    {
        { MapSize.DefaultWidth, MapSize.DefaultHeight },
        { MapSize.MinWidth, MapSize.MinHeight },
        { MapSize.MaxWidth, MapSize.MaxHeight },
        { MapSize.MaxWidth, MapSize.MinHeight },
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Every_guarantee_holds_for_consecutive_seeds(int width, int height)
    {
        for (uint seed = 1; seed <= SeedCount; seed++)
        {
            var map = BspGenerator.Generate(seed, width, height);
            var grid = new Grid(map);

            Assert.Equal(seed, map.Seed);
            Assert.Equal(width * height, map.Cells.Length);
            AssertEdgesAreNotWalkable(grid, seed);
            AssertWalkableCellsAreConnected(grid, seed);
            AssertWallsEncloseWalkableCells(grid, seed);
            AssertRoomsAreFloorAndDoNotOverlap(grid, seed);
            AssertEveryRoomHasADoor(grid, seed);
            AssertDoorsHaveOneOrientation(grid, seed);
            AssertCorridorsAreOneCellWide(grid, seed);
        }
    }

    [Fact]
    public void The_same_seed_and_size_give_identical_maps()
    {
        for (uint seed = 1; seed <= SeedCount; seed++)
        {
            var first = BspGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
            var second = BspGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);

            Assert.Equal(first.Cells, second.Cells);
            Assert.Equal(first.Rooms, second.Rooms);
        }
    }

    [Fact]
    public void Different_seeds_give_different_maps()
    {
        var layouts = new HashSet<string>();
        for (uint seed = 1; seed <= SeedCount; seed++)
        {
            var map = BspGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
            layouts.Add(string.Concat(map.Cells.Select(cell => (char)('0' + (int)cell))));
        }

        Assert.Equal(SeedCount, layouts.Count);
    }

    [Theory]
    [InlineData(MapSize.MaxWidth + 1, MapSize.DefaultHeight)]
    [InlineData(MapSize.DefaultWidth, MapSize.MaxHeight + 1)]
    [InlineData(MapSize.MaxWidth + 1, MapSize.MaxHeight + 1)]
    [InlineData(MapSize.MinWidth - 1, MapSize.DefaultHeight)]
    [InlineData(MapSize.DefaultWidth, MapSize.MinHeight - 1)]
    [InlineData(0, 0)]
    [InlineData(-1, MapSize.DefaultHeight)]
    public void Sizes_outside_the_limits_throw(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BspGenerator.Generate(1, width, height));
    }

    [Fact]
    public void Prng_repeats_its_sequence_for_a_seed_and_stays_in_range()
    {
        var first = new Prng(42);
        var second = new Prng(42);
        for (var i = 0; i < 1000; i++)
        {
            var value = first.NextInt(-3, 7);
            Assert.Equal(value, second.NextInt(-3, 7));
            Assert.InRange(value, -3, 6);
        }
    }

    private static void AssertEdgesAreNotWalkable(Grid grid, uint seed)
    {
        for (var x = 0; x < grid.Width; x++)
        {
            Assert.False(grid.IsWalkable(x, 0), $"seed {seed}: walkable cell at ({x}, 0)");
            Assert.False(grid.IsWalkable(x, grid.Height - 1), $"seed {seed}: walkable cell at ({x}, {grid.Height - 1})");
        }
        for (var y = 0; y < grid.Height; y++)
        {
            Assert.False(grid.IsWalkable(0, y), $"seed {seed}: walkable cell at (0, {y})");
            Assert.False(grid.IsWalkable(grid.Width - 1, y), $"seed {seed}: walkable cell at ({grid.Width - 1}, {y})");
        }
    }

    private static void AssertWalkableCellsAreConnected(Grid grid, uint seed)
    {
        var walkable = grid.Cells().Where(c => grid.IsWalkable(c.X, c.Y)).ToList();
        Assert.NotEmpty(walkable);

        var seen = new HashSet<(int X, int Y)> { walkable[0] };
        var queue = new Queue<(int X, int Y)>([walkable[0]]);
        while (queue.TryDequeue(out var cell))
        {
            foreach (var (dx, dy) in Grid.Orthogonal)
            {
                var next = (cell.X + dx, cell.Y + dy);
                if (grid.IsWalkable(next.Item1, next.Item2) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        Assert.True(seen.Count == walkable.Count, $"seed {seed}: {walkable.Count - seen.Count} walkable cells are unreachable");
    }

    private static void AssertWallsEncloseWalkableCells(Grid grid, uint seed)
    {
        foreach (var (x, y) in grid.Cells().Where(c => grid.IsWalkable(c.X, c.Y)))
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var neighbour = grid.At(x + dx, y + dy);
                    Assert.True(Grid.IsWalkable(neighbour) || neighbour == CellKind.Wall,
                        $"seed {seed}: ({x + dx}, {y + dy}) next to walkable ({x}, {y}) is {neighbour}");
                }
            }
        }
    }

    private static void AssertRoomsAreFloorAndDoNotOverlap(Grid grid, uint seed)
    {
        var owner = new Dictionary<(int, int), int>();
        for (var i = 0; i < grid.Map.Rooms.Length; i++)
        {
            var room = grid.Map.Rooms[i];
            for (var y = room.Y; y < room.Y + room.Height; y++)
            {
                for (var x = room.X; x < room.X + room.Width; x++)
                {
                    Assert.True(owner.TryAdd((x, y), i), $"seed {seed}: rooms {owner.GetValueOrDefault((x, y))} and {i} overlap at ({x}, {y})");
                    Assert.Equal(CellKind.Floor, grid.At(x, y));
                }
            }
        }

        // Every floor cell belongs to a listed room.
        Assert.Equal(owner.Count, grid.Map.Cells.Count(cell => cell == CellKind.Floor));
    }

    private static void AssertEveryRoomHasADoor(Grid grid, uint seed)
    {
        foreach (var room in grid.Map.Rooms)
        {
            var hasDoor = false;
            for (var y = room.Y - 1; y <= room.Y + room.Height; y++)
            {
                for (var x = room.X - 1; x <= room.X + room.Width; x++)
                {
                    hasDoor |= grid.At(x, y) == CellKind.Door;
                }
            }
            Assert.True(hasDoor, $"seed {seed}: room {room} has no door");
        }
    }

    private static void AssertDoorsHaveOneOrientation(Grid grid, uint seed)
    {
        foreach (var (x, y) in grid.Cells().Where(c => grid.At(c.X, c.Y) == CellKind.Door))
        {
            var west = grid.At(x - 1, y);
            var east = grid.At(x + 1, y);
            var north = grid.At(x, y - 1);
            var south = grid.At(x, y + 1);
            var passesEastWest = Grid.IsWalkable(west) && Grid.IsWalkable(east) && north == CellKind.Wall && south == CellKind.Wall;
            var passesNorthSouth = Grid.IsWalkable(north) && Grid.IsWalkable(south) && west == CellKind.Wall && east == CellKind.Wall;
            Assert.True(passesEastWest || passesNorthSouth,
                $"seed {seed}: door at ({x}, {y}) has W={west} E={east} N={north} S={south}");
        }
    }

    // No 2×2 block of corridor cells: a corridor never widens into a second lane.
    private static void AssertCorridorsAreOneCellWide(Grid grid, uint seed)
    {
        for (var y = 0; y < grid.Height - 1; y++)
        {
            for (var x = 0; x < grid.Width - 1; x++)
            {
                var block = new[] { grid.At(x, y), grid.At(x + 1, y), grid.At(x, y + 1), grid.At(x + 1, y + 1) };
                Assert.False(block.All(cell => cell == CellKind.Corridor), $"seed {seed}: 2x2 corridor block at ({x}, {y})");
            }
        }
    }

    private sealed class Grid(GeneratedMap map)
    {
        public static readonly (int Dx, int Dy)[] Orthogonal = [(1, 0), (-1, 0), (0, 1), (0, -1)];

        public GeneratedMap Map { get; } = map;
        public int Width => Map.Width;
        public int Height => Map.Height;

        // Outside the map counts as void, so an edge cell's missing neighbours never pass as walls.
        public CellKind At(int x, int y) =>
            x < 0 || y < 0 || x >= Width || y >= Height ? CellKind.Void : Map.Cells[y * Width + x];

        public bool IsWalkable(int x, int y) => IsWalkable(At(x, y));

        public static bool IsWalkable(CellKind cell) =>
            cell is CellKind.Floor or CellKind.Corridor or CellKind.Door;

        public IEnumerable<(int X, int Y)> Cells()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    yield return (x, y);
                }
            }
        }
    }
}
