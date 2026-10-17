using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace BattleMapGenerator.Api.Maps;

public static class MapEndpoints
{
    public const string GenerateRateLimitPolicy = "generate";

    // Generate calls per minute per account (the NameIdentifier claim).
    public const int PerAccountGenerateLimit = 10;

    // Chained onto the per-account generate limiter in Program.cs: one shared bucket across every
    // account, so a scripted client cannot escape the per-account ceiling by creating unlimited accounts.
    public const int GlobalGenerateLimit = 50;

    // Generation requires a session (401 without one) and is rate-limited per account; the limit and
    // the room-count/boss-size parameters bound its cost.
    // Generation is pure computation: it must never resolve AppDbContext or touch the database.
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        // The body is read by Generate itself, so Accepts keeps the request schema in the OpenAPI
        // document and still limits the endpoint to JSON.
        app.MapPost("/api/maps/generate", Generate)
            .Accepts<GenerateMapRequest>(isOptional: true, "application/json")
            .WithName("GenerateMap")
            .RequireAuthorization()
            .RequireRateLimiting(GenerateRateLimitPolicy)
            .WithBodySizeLimit()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);
        return app;
    }

    // Invalid parameters are the client's error: a 400 naming the field, never a generator exception.
    // The body is read here rather than bound by the framework, because a value binding rejects (an
    // unknown enum name, a number as an enum, text as a number) would otherwise get a 400 with no body.
    private static async Task<Results<Ok<GeneratedMap>, ValidationProblem>> Generate(
        HttpRequest http, IOptions<HttpJsonOptions> json, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        // MapEndpoints is static, so it cannot be used as the generic argument of ILogger<T> (CS0718);
        // ILoggerFactory.CreateLogger(Type) produces the same category name an injected ILogger<MapEndpoints>
        // would have had.
        var logger = loggerFactory.CreateLogger(typeof(MapEndpoints));

        GenerateMapRequest? request;
        try
        {
            // An empty body is allowed and means "all defaults", as it did when the framework bound it.
            request = await RequestBody.ReadAsync<GenerateMapRequest>(http, json.Value.SerializerOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            return TypedResults.ValidationProblem(RequestBody.BindingError(exception, FieldMessages, RequestMessage));
        }

        request ??= new GenerateMapRequest(Seed: null);
        if (RequestBody.Validate(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var seed = request.Seed ?? NewSeed();
        var parameters = request.ToParameters();
        try
        {
            return TypedResults.Ok(BspGenerator.Generate(seed, parameters));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map generation failed for seed {Seed}, roomCount {RoomCount}, encounter {Encounter}",
                seed, parameters.RoomCount, parameters.Encounter);
            throw;
        }
    }

    // Shared with Save, which takes the same fields plus a title.
    internal static readonly IReadOnlyDictionary<string, string> FieldMessages = new Dictionary<string, string>
    {
        ["seed"] = GenerateMapRequest.SeedMessage,
        ["roomCount"] = GenerateMapRequest.RoomCountMessage,
        ["encounter"] = GenerateMapRequest.EncounterMessage,
        ["bossSize"] = GenerateMapRequest.BossSizeMessage,
    };

    private const string RequestMessage = "The request body must be a JSON object with seed, roomCount, encounter and bossSize.";

    // A fresh seed only picks which map to generate, so it comes from the OS generator
    // rather than from any ambient or shared PRNG state.
    private static uint NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
}
