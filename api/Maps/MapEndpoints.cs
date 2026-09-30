using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
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
        request ??= new GenerateMapRequest(Seed: null);
        if (Validate(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(BspGenerator.Generate(request.Seed ?? NewSeed(), request.ToParameters()));
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
            foreach (var member in result.MemberNames)
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
