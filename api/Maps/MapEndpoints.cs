using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BattleMapGenerator.Api.Maps;

public static class MapEndpoints
{
    public const string GenerateRateLimitPolicy = "generate";

    // Public until S-04 adds login; the rate limit and the room-count limits bound its cost.
    // Generation is pure computation: it must never resolve AppDbContext or touch the database.
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/maps/generate", Generate)
            .WithName("GenerateMap")
            .RequireRateLimiting(GenerateRateLimitPolicy);
        return app;
    }

    // Invalid parameters are the client's error: a 400 naming the field, never a generator exception.
    private static Results<Ok<GeneratedMap>, ValidationProblem> Generate(GenerateMapRequest? request)
    {
        var roomCount = request?.RoomCount ?? MapSize.DefaultRoomCount;
        var encounter = request?.Encounter ?? EncounterType.Skirmish;
        var bossSize = request?.BossSize;

        var errors = new Dictionary<string, string[]>();
        if (roomCount < MapSize.MinRoomCount || roomCount > MapSize.MaxRoomCount)
        {
            errors["roomCount"] = [$"Room count must be between {MapSize.MinRoomCount} and {MapSize.MaxRoomCount}."];
        }
        // An unknown encounter or boss size never gets this far: JSON binding rejects it with a 400.
        if (encounter == EncounterType.Boss && bossSize is null)
        {
            errors["bossSize"] = ["A boss fight needs a boss size: 'large', 'huge' or 'gargantuan'."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // A boss size sent with a skirmish is ignored.
        var parameters = new MapParameters(roomCount, encounter, encounter == EncounterType.Boss ? bossSize : null);
        return TypedResults.Ok(BspGenerator.Generate(request?.Seed ?? NewSeed(), parameters));
    }

    // A fresh seed only picks which map to generate, so it comes from the OS generator
    // rather than from any ambient or shared PRNG state.
    private static uint NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
}
