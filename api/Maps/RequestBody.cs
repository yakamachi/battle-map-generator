using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BattleMapGenerator.Api.Maps;

// The hand-run body read and validation shared by generate and the saved-maps library. Each endpoint
// reads its own body, so a value JSON binding rejects (an unknown enum name, a number as an enum, text
// as a number) gets a validation problem naming the field instead of a 400 with no body; its
// `.Accepts<T>` keeps the request schema in OpenAPI and still limits the endpoint to JSON.
internal static class RequestBody
{
    // An empty body (or a JSON null) gives null; each endpoint decides what that means.
    public static async Task<T?> ReadAsync<T>(
        HttpRequest http, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        await http.Body.CopyToAsync(body, cancellationToken);
        if (body.Length == 0)
        {
            return default;
        }
        body.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(body, options, cancellationToken);
    }

    // Names the field from the exception's JSON path ("$.encounter"). Broken JSON (the reader's own
    // exception, wrapped) is reported against the whole request, even when the path ends in a field.
    // A field without its own message gets the request message.
    public static Dictionary<string, string[]> BindingError(
        JsonException exception, IReadOnlyDictionary<string, string> fieldMessages, string requestMessage)
    {
        var path = exception.Path ?? "$";
        var brokenJson = exception.InnerException is JsonException;
        var field = !brokenJson && path.StartsWith("$.", StringComparison.Ordinal)
            ? JsonNamingPolicy.CamelCase.ConvertName(path[2..])
            : "request";
        var message = fieldMessages.GetValueOrDefault(field, requestMessage);
        return new Dictionary<string, string[]> { [field] = [message] };
    }

    // Runs the request's data annotations by hand, on purpose: .NET 10's AddValidation() only
    // validates a request bound as a real endpoint parameter, needs the project-wide
    // EnableRequestDelegateGenerator opt-in to do that at all, and even then keys its errors by the
    // C# property name ("RoomCount") rather than this app's camelCase JSON convention, with no
    // supported fix (context/foundation/lessons.md, "Triage a .NET native swap..."; checked 2026-10-02).
    // Errors are keyed by the JSON field name ("roomCount"), as the client wrote it.
    public static Dictionary<string, string[]> Validate(object request)
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
}
