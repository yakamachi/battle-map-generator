using System.Text.Json;
using System.Text.Json.Serialization;

namespace battle_map_generator_api.Maps;

// The semantic grid shared with web/: what each cell is, never which sprite to draw.
// Serialised as camelCase strings ("void", "floor", ...) through MapJson.
public enum CellKind
{
    Void,
    Floor,
    Corridor,
    Wall,
    Door,
}

// The floor rectangle of a room; its walls lie on the ring just outside it.
public sealed record Room(int X, int Y, int Width, int Height);

// Cells are row-major: index = y * Width + x.
public sealed record GeneratedMap(uint Seed, int Width, int Height, CellKind[] Cells, Room[] Rooms);

// The body is optional; without a seed the server draws one.
public sealed record GenerateMapRequest(uint? Seed);

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
