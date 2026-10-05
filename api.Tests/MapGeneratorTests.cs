using BattleMapGenerator.Api.Maps;

namespace BattleMapGenerator.Api.Tests;

public sealed class MapGeneratorTests
{
    private const int SeedCount = 200;
    private const int SweepSeedCount = 1000;

    // Every room count for a skirmish and for each boss size.
    public static TheoryData<int, BossSize?> ParameterCombinations
    {
        get
        {
            var data = new TheoryData<int, BossSize?>();
            for (var roomCount = MapSize.MinRoomCount; roomCount <= MapSize.MaxRoomCount; roomCount++)
            {
                data.Add(roomCount, null);
                data.Add(roomCount, BossSize.Large);
                data.Add(roomCount, BossSize.Huge);
                data.Add(roomCount, BossSize.Gargantuan);
            }
            return data;
        }
    }

    // The size table from the plan: the contract between room count and map size.
    public static TheoryData<int, int, int> SizeTable => new()
    {
        { 2, 20, 14 }, { 3, 24, 14 }, { 4, 24, 18 }, { 5, 28, 18 }, { 6, 30, 20 }, { 7, 32, 22 },
        { 8, 34, 24 }, { 9, 36, 26 }, { 10, 38, 28 }, { 11, 40, 28 }, { 12, 42, 30 },
    };

    [Theory]
    [MemberData(nameof(ParameterCombinations))]
    public void Every_guarantee_holds_for_consecutive_seeds(int roomCount, BossSize? bossSize)
    {
        var parameters = Parameters(roomCount, bossSize);
        var (width, height) = MapSize.For(roomCount, bossSize);

        for (uint seed = 1; seed <= SweepSeedCount; seed++)
        {
            var map = BspGenerator.Generate(seed, parameters);
            var grid = new Grid(map);

            Assert.Equal(seed, map.Seed);
            Assert.Equal(parameters, map.Parameters);
            Assert.Equal(width, map.Width);
            Assert.Equal(height, map.Height);
            Assert.Equal(width * height, map.Cells.Length);
            Assert.True(map.Rooms.Length == roomCount, $"seed {seed}: {map.Rooms.Length} rooms instead of {roomCount}");
            AssertEdgesAreNotWalkable(grid, seed);
            AssertWalkableCellsAreConnected(grid, seed);
            AssertWallsEncloseWalkableCells(grid, seed);
            AssertRoomsAreFloorAndDoNotOverlap(grid, seed);
            AssertEveryRoomHasADoor(grid, seed);
            AssertDoorsHaveOneOrientation(grid, seed);
            AssertCorridorsAreOneCellWide(grid, seed);
            AssertArena(grid, bossSize, seed);
        }
    }

    [Theory]
    [MemberData(nameof(SizeTable))]
    public void Map_size_follows_the_room_count_and_grows_for_a_boss(int roomCount, int width, int height)
    {
        Assert.Equal((width, height), MapSize.For(roomCount, null));
        Assert.Equal((width + 8, Math.Max(height, 12)), MapSize.For(roomCount, BossSize.Large));
        Assert.Equal((width + 10, Math.Max(height, 14)), MapSize.For(roomCount, BossSize.Huge));
        Assert.Equal((width + 12, Math.Max(height, 16)), MapSize.For(roomCount, BossSize.Gargantuan));
    }

    [Fact]
    public void The_default_parameters_give_the_default_size_map()
    {
        Assert.Equal((MapSize.DefaultWidth, MapSize.DefaultHeight), MapSize.For(MapSize.DefaultRoomCount, null));

        var bySize = BspGenerator.Generate(42, MapSize.DefaultWidth, MapSize.DefaultHeight);
        var byParameters = BspGenerator.Generate(42, Parameters(MapSize.DefaultRoomCount, null));

        Assert.Equal(bySize.Cells, byParameters.Cells);
        Assert.Equal(bySize.Rooms, byParameters.Rooms);
        Assert.Equal(bySize.Parameters, byParameters.Parameters);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BossSize.Huge)]
    public void The_same_seed_and_parameters_give_identical_maps(BossSize? bossSize)
    {
        var parameters = Parameters(MapSize.DefaultRoomCount, bossSize);
        for (uint seed = 1; seed <= SeedCount; seed++)
        {
            var first = BspGenerator.Generate(seed, parameters);
            var second = BspGenerator.Generate(seed, parameters);

            Assert.Equal(first.Cells, second.Cells);
            Assert.Equal(first.Rooms, second.Rooms);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BossSize.Huge)]
    public void Different_seeds_give_different_maps(BossSize? bossSize)
    {
        var parameters = Parameters(MapSize.DefaultRoomCount, bossSize);
        var layouts = new HashSet<string>();
        for (uint seed = 1; seed <= SeedCount; seed++)
        {
            var map = BspGenerator.Generate(seed, parameters);
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

    [Theory]
    [InlineData(MapSize.MinWidth, MapSize.MinHeight, MapSize.DefaultRoomCount, null)]
    [InlineData(MapSize.DefaultWidth, MapSize.DefaultHeight, MapSize.MaxRoomCount, null)]
    [InlineData(MapSize.DefaultWidth, MapSize.DefaultHeight, MapSize.MinRoomCount - 1, null)]
    [InlineData(MapSize.DefaultWidth, MapSize.DefaultHeight, MapSize.MaxRoomCount + 1, null)]
    [InlineData(MapSize.DefaultWidth, MapSize.MinHeight, MapSize.MinRoomCount, BossSize.Gargantuan)]
    [InlineData(MapSize.MinWidth, MapSize.DefaultHeight, MapSize.MinRoomCount, BossSize.Large)]
    public void Sizes_that_cannot_hold_the_rooms_throw(int width, int height, int roomCount, BossSize? bossSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BspGenerator.Generate(1, width, height, roomCount, bossSize));
    }

    [Fact]
    public void A_boss_fight_without_a_boss_size_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            BspGenerator.Generate(1, new MapParameters(MapSize.DefaultRoomCount, EncounterType.Boss, null)));
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
                    Assert.Equal(room.Kind == RoomKind.BossArena ? CellKind.BossArena : CellKind.Floor, grid.At(x, y));
                }
            }
        }

        // Every floor and arena cell belongs to a listed room.
        Assert.Equal(owner.Count, grid.Map.Cells.Count(cell => cell is CellKind.Floor or CellKind.BossArena));
    }

    // A boss fight has exactly one arena: at least the boss size's minimum on both sides and larger
    // than every other room. A skirmish has no arena room and (with the check above) no arena cell.
    private static void AssertArena(Grid grid, BossSize? bossSize, uint seed)
    {
        var arenas = grid.Map.Rooms.Where(room => room.Kind == RoomKind.BossArena).ToList();
        if (bossSize is not { } boss)
        {
            Assert.True(arenas.Count == 0, $"seed {seed}: a skirmish has an arena room");
            Assert.DoesNotContain(CellKind.BossArena, grid.Map.Cells);
            return;
        }

        Assert.True(arenas.Count == 1, $"seed {seed}: {arenas.Count} arena rooms");
        var arena = arenas[0];
        var side = MapSize.MinArenaSide(boss);
        Assert.True(arena.Width >= side && arena.Height >= side, $"seed {seed}: arena {arena} is below {side}x{side}");
        Assert.Equal(arena.Width * arena.Height, grid.Map.Cells.Count(cell => cell == CellKind.BossArena));
        foreach (var room in grid.Map.Rooms.Where(room => room.Kind != RoomKind.BossArena))
        {
            Assert.True(room.Width * room.Height < arena.Width * arena.Height,
                $"seed {seed}: room {room} is not smaller than arena {arena}");
        }
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

    private static MapParameters Parameters(int roomCount, BossSize? bossSize) =>
        new(roomCount, bossSize is null ? EncounterType.Skirmish : EncounterType.Boss, bossSize);

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
            cell is CellKind.Floor or CellKind.Corridor or CellKind.Door or CellKind.BossArena;

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
