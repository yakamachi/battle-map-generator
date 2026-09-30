using System.Text.Json;
using System.Text.Json.Serialization;

namespace BattleMapGenerator.Api.Maps;

// The semantic grid shared with web/: what each cell is, never which sprite to draw.
// Serialised as camelCase strings ("void", "floor", ...) through MapJson.
public enum CellKind
{
    Void,
    Floor,
    Corridor,
    Wall,
    Door,
    // The floor of the boss arena; walkable like Floor.
    BossArena,
}

public enum EncounterType
{
    Skirmish,
    Boss,
}

public enum BossSize
{
    Large,
    Huge,
    Gargantuan,
}

public enum RoomKind
{
    Room,
    BossArena,
}

// The floor rectangle of a room; its walls lie on the ring just outside it.
public sealed record Room(int X, int Y, int Width, int Height, RoomKind Kind);

// The parameters a map was generated with. BossSize is set only on a boss fight.
public sealed record MapParameters(int RoomCount, EncounterType Encounter, BossSize? BossSize);

// Cells are row-major: index = y * Width + x.
public sealed record GeneratedMap(uint Seed, MapParameters Parameters, int Width, int Height, CellKind[] Cells, Room[] Rooms);

// The body and every field are optional: without a seed the server draws one, and the other
// fields fall back to the defaults (6 rooms, skirmish).
public sealed record GenerateMapRequest(
    uint? Seed,
    int? RoomCount = null,
    EncounterType? Encounter = null,
    BossSize? BossSize = null);

public static class MapSize
{
    public const int DefaultWidth = 30;
    public const int DefaultHeight = 20;

    // The cap bounds the CPU cost of one request on the free hosting plan.
    public const int MaxWidth = 60;
    public const int MaxHeight = 60;

    // Smallest size that always splits into two rooms joined by a corridor.
    public const int MinWidth = 14;
    public const int MinHeight = 14;

    public const int MinRoomCount = 2;
    public const int MaxRoomCount = 12;
    public const int DefaultRoomCount = 6;

    // Distance from a leaf's edge to its room's floor: one free lane plus the wall ring.
    public const int RoomMargin = 2;

    // Map size by room count (index 0 is 2 rooms), roughly 100 squares per room.
    // The row for the default count is the default size.
    private static readonly (int Width, int Height)[] SizeByRoomCount =
    [
        (20, 14), (24, 14), (24, 18), (28, 18), (DefaultWidth, DefaultHeight), (32, 22),
        (34, 24), (36, 26), (38, 28), (40, 28), (42, 30),
    ];

    // The arena's floor is at least this many squares on both sides.
    public static int MinArenaSide(BossSize bossSize) => bossSize switch
    {
        BossSize.Large => 8,
        BossSize.Huge => 10,
        BossSize.Gargantuan => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(bossSize), bossSize, "Unknown boss size."),
    };

    // A boss fight widens the map by the arena's minimum floor side and makes it at least as high
    // as the arena's leaf. The largest map is 12 rooms with a Gargantuan boss: 54×30.
    public static (int Width, int Height) For(int roomCount, BossSize? bossSize)
    {
        if (roomCount < MinRoomCount || roomCount > MaxRoomCount)
        {
            throw new ArgumentOutOfRangeException(nameof(roomCount), roomCount,
                $"Room count must be between {MinRoomCount} and {MaxRoomCount}.");
        }

        var (width, height) = SizeByRoomCount[roomCount - MinRoomCount];
        if (bossSize is not { } boss)
        {
            return (width, height);
        }

        var side = MinArenaSide(boss);
        return (width + side, Math.Max(height, side + 2 * RoomMargin));
    }
}

// One place for the map's JSON shape, so the endpoint and the fixture files serialise identically.
// Numbers are strict: the web defaults also accept numbers written as strings, which would make every
// integer in the OpenAPI contract "integer | string" for the generated client.
public static class MapJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
