using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BattleMapGenerator.Api.Maps;

public static class MapEndpoints
{
    public const string GenerateRateLimitPolicy = "generate";

    // Generation requires a session (401 without one) and is rate-limited per account; the limit and
    // the fixed default size bound its cost.
    // Generation is pure computation: it must never resolve AppDbContext or touch the database.
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/maps/generate", Generate)
            .WithName("GenerateMap")
            .RequireAuthorization()
            .RequireRateLimiting(GenerateRateLimitPolicy);
        return app;
    }

    private static Ok<GeneratedMap> Generate(GenerateMapRequest? request)
    {
        var seed = request?.Seed ?? NewSeed();
        return TypedResults.Ok(BspGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight));
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
