using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace BattleMapGenerator.Api.Maps;

public static class MapEndpoints
{
    public const string GenerateRateLimitPolicy = "generate";

    // Public until S-04 adds login; the rate limit and the room-count limits bound its cost.
    // Generation is pure computation: it must never resolve AppDbContext or touch the database.
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        // The body is read by Generate itself, so Accepts keeps the request schema in the OpenAPI
        // document and still limits the endpoint to JSON.
        app.MapPost("/api/maps/generate", Generate)
            .Accepts<GenerateMapRequest>(isOptional: true, "application/json")
            .WithName("GenerateMap")
            .RequireRateLimiting(GenerateRateLimitPolicy);
        return app;
    }

    // Invalid parameters are the client's error: a 400 naming the field, never a generator exception.
    // The body is read here rather than bound by the framework, because a value binding rejects (an
    // unknown enum name, a number as an enum, text as a number) would otherwise get a 400 with no body.
    private static async Task<Results<Ok<GeneratedMap>, ValidationProblem>> Generate(
        HttpRequest http, IOptions<HttpJsonOptions> json, CancellationToken cancellationToken)
    {
        GenerateMapRequest? request;
        try
        {
            request = await ReadRequest(http, json.Value.SerializerOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            return TypedResults.ValidationProblem(BindingError(exception));
        }

        request ??= new GenerateMapRequest(Seed: null);
        if (Validate(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(BspGenerator.Generate(request.Seed ?? NewSeed(), request.ToParameters()));
    }

    // An empty body is allowed and means "all defaults", as it did when the framework bound it.
    private static async Task<GenerateMapRequest?> ReadRequest(
        HttpRequest http, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        await http.Body.CopyToAsync(body, cancellationToken);
        if (body.Length == 0)
        {
            return null;
        }
        body.Position = 0;
        return await JsonSerializer.DeserializeAsync<GenerateMapRequest>(body, options, cancellationToken);
    }

    // Names the field from the exception's JSON path ("$.encounter"). Broken JSON (the reader's own
    // exception, wrapped) is reported against the whole request, even when the path ends in a field.
    private static Dictionary<string, string[]> BindingError(JsonException exception)
    {
        var path = exception.Path ?? "$";
        var brokenJson = exception.InnerException is JsonException;
        var field = !brokenJson && path.StartsWith("$.", StringComparison.Ordinal)
            ? JsonNamingPolicy.CamelCase.ConvertName(path[2..])
            : "request";
        var message = field switch
        {
            "seed" => GenerateMapRequest.SeedMessage,
            "roomCount" => GenerateMapRequest.RoomCountMessage,
            "encounter" => GenerateMapRequest.EncounterMessage,
            "bossSize" => GenerateMapRequest.BossSizeMessage,
            _ => "The request body must be a JSON object with seed, roomCount, encounter and bossSize.",
        };
        return new Dictionary<string, string[]> { [field] = [message] };
    }

    // Runs the request's data annotations by hand. The framework does this itself once
    // AddValidation() is registered in Program.cs; switch to that when S-04 has landed.
    // Errors are keyed by the JSON field name ("roomCount"), as the client wrote it.
    private static Dictionary<string, string[]> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        var errors = new Dictionary<string, string[]>();
        foreach (var result in results)
        {
            // A rule that names no field is reported against the whole request, never dropped.
            var members = result.MemberNames.Any() ? result.MemberNames : ["Request"];
            foreach (var member in members)
            {
                var field = JsonNamingPolicy.CamelCase.ConvertName(member);
                errors[field] = [.. errors.GetValueOrDefault(field, []), result.ErrorMessage ?? "Invalid value."];
            }
        }
        return errors;
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
