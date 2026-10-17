using System.Security.Claims;
using System.Text.Json;
using BattleMapGenerator.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using SavedMapRow = BattleMapGenerator.Api.Data.SavedMap;

namespace BattleMapGenerator.Api.Maps;

// The caller's own library of saved maps: list, open, save, rename and delete.
// Ownership is the shape of every query: each load, rename and delete filters on the map's id and the
// caller's NameIdentifier together, so another account's map is indistinguishable from a missing one
// (404, never 403), and there is no load-then-compare path that could leak it.
// Every route requires a session (401 without one). AppDbContext is a handler parameter, resolved
// only once authorization has passed, so an anonymous request never touches the database.
// No rate-limit policy applies beyond SessionCookieGate's per-IP limit; Save regenerates the map but
// spends no generate permit.
// Storage is compact: the parameters are columns and the cells and rooms are bytes (GridCodec). The
// list projects summary columns only, so one unreadable grid cannot break it.
public static class SavedMapEndpoints
{
    public const int MaxSavedMapsPerAccount = 100;

    public const string CapMessage = "You can keep at most 100 saved maps. Delete one to save another.";

    private const string SaveRequestMessage =
        "The request body must be a JSON object with title, seed, roomCount, encounter and bossSize.";

    private const string RenameRequestMessage = "The request body must be a JSON object with title.";

    private static readonly IReadOnlyDictionary<string, string> SaveFieldMessages =
        new Dictionary<string, string>(MapEndpoints.FieldMessages) { ["title"] = SavedMapTitleAttribute.Message };

    private static readonly IReadOnlyDictionary<string, string> RenameFieldMessages =
        new Dictionary<string, string> { ["title"] = SavedMapTitleAttribute.Message };

    // POST and PATCH read their bodies themselves (like generate), so Accepts keeps the request schema
    // in the OpenAPI document and limits them to JSON: any other content type falls to the /api 404.
    // The {id:guid} constraint keeps these routes apart from /api/maps/generate; a non-guid id is a 404.
    public static IEndpointRouteBuilder MapSavedMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/maps", List)
            .WithName("ListSavedMaps")
            .RequireAuthorization()
            .Produces(StatusCodes.Status401Unauthorized);
        app.MapGet("/api/maps/{id:guid}", Get)
            .WithName("GetSavedMap")
            .RequireAuthorization()
            .Produces(StatusCodes.Status401Unauthorized);
        app.MapPost("/api/maps", Save)
            .Accepts<SaveMapRequest>("application/json")
            .WithName("SaveMap")
            .RequireAuthorization()
            .WithBodySizeLimit()
            .Produces(StatusCodes.Status401Unauthorized);
        app.MapPatch("/api/maps/{id:guid}", Rename)
            .Accepts<RenameMapRequest>("application/json")
            .WithName("RenameSavedMap")
            .RequireAuthorization()
            .WithBodySizeLimit()
            .Produces(StatusCodes.Status401Unauthorized);
        app.MapDelete("/api/maps/{id:guid}", Delete)
            .WithName("DeleteSavedMap")
            .RequireAuthorization()
            .Produces(StatusCodes.Status401Unauthorized);
        return app;
    }

    // Newest first; maps saved at the same instant are ordered by id, never left to the database.
    // The projection selects the summary columns only: the query never reads Cells or Rooms.
    private static async Task<Ok<SavedMapSummary[]>> List(
        ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        var ownerId = OwnerId(user);
        var summaries = await db.SavedMaps
            .AsNoTracking()
            .Where(m => m.OwnerId == ownerId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new SavedMapSummary(
                m.Id, m.Title, (uint)m.Seed, m.Width, m.Height, new MapParameters(m.RoomCount, m.Encounter, m.BossSize),
                m.CreatedAt, m.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(summaries);
    }

    private static async Task<Results<Ok<SavedMap>, NotFound>> Get(
        Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        var ownerId = OwnerId(user);
        var row = await db.SavedMaps
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, cancellationToken);
        return row is null ? TypedResults.NotFound() : TypedResults.Ok(ToSavedMap(row));
    }

    // The map is regenerated here from seed and parameters, so the stored grid is always genuine
    // generator output. If the generator changed since the client generated it, the stored map differs
    // from the one on screen: the client shows the map from this response.
    private static async Task<Results<Created<SavedMap>, ValidationProblem>> Save(
        HttpRequest http, ClaimsPrincipal user, AppDbContext db, TimeProvider time,
        IOptions<HttpJsonOptions> json, CancellationToken cancellationToken)
    {
        var options = json.Value.SerializerOptions;
        SaveMapRequest? request;
        try
        {
            request = await RequestBody.ReadAsync<SaveMapRequest>(http, options, cancellationToken);
        }
        catch (JsonException exception)
        {
            return TypedResults.ValidationProblem(RequestBody.BindingError(exception, SaveFieldMessages, SaveRequestMessage));
        }

        // An empty body names every missing field rather than the whole request.
        request ??= new SaveMapRequest(Title: null, Seed: null);
        var errors = RequestBody.Validate(request);
        foreach (var (field, messages) in RequestBody.Validate(request.ToGenerateRequest()))
        {
            errors[field] = [.. errors.GetValueOrDefault(field, []), .. messages];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var ownerId = OwnerId(user);
        // A soft cap: count, then insert. Two saves racing at 99 maps can both pass and leave 101;
        // that is accepted, since the cap bounds storage per account rather than guarding an invariant.
        if (await db.SavedMaps.CountAsync(m => m.OwnerId == ownerId, cancellationToken) >= MaxSavedMapsPerAccount)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [CapMessage] });
        }

        var map = BspGenerator.Generate(request.Seed!.Value, request.ToGenerateRequest().ToParameters());
        var now = time.GetUtcNow();
        var row = new SavedMapRow
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = SavedMapTitleAttribute.Normalize(request.Title!),
            Seed = map.Seed,
            Width = map.Width,
            Height = map.Height,
            RoomCount = map.Parameters.RoomCount,
            Encounter = map.Parameters.Encounter,
            BossSize = map.Parameters.BossSize,
            Cells = GridCodec.EncodeCells(map.Cells),
            Rooms = GridCodec.EncodeRooms(map.Rooms),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.SavedMaps.Add(row);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/maps/{row.Id}", new SavedMap(row.Id, row.Title, row.CreatedAt, row.UpdatedAt, map));
    }

    // Only the title changes; UpdatedAt records when. A delete landing between the load and the update
    // makes the update touch no row, which EF reports as a concurrency conflict: the map is gone, so 404.
    private static async Task<Results<Ok<SavedMap>, ValidationProblem, NotFound>> Rename(
        Guid id, HttpRequest http, ClaimsPrincipal user, AppDbContext db, TimeProvider time,
        IOptions<HttpJsonOptions> json, CancellationToken cancellationToken)
    {
        var options = json.Value.SerializerOptions;
        RenameMapRequest? request;
        try
        {
            request = await RequestBody.ReadAsync<RenameMapRequest>(http, options, cancellationToken);
        }
        catch (JsonException exception)
        {
            return TypedResults.ValidationProblem(RequestBody.BindingError(exception, RenameFieldMessages, RenameRequestMessage));
        }

        request ??= new RenameMapRequest(Title: null);
        if (RequestBody.Validate(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var ownerId = OwnerId(user);
        var row = await db.SavedMaps.SingleOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, cancellationToken);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        row.Title = SavedMapTitleAttribute.Normalize(request.Title!);
        row.UpdatedAt = time.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(ToSavedMap(row));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        var ownerId = OwnerId(user);
        var deleted = await db.SavedMaps
            .Where(m => m.Id == id && m.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    // Every route requires authorization, so a signed-in caller always carries the claim.
    private static string OwnerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authorized request has no NameIdentifier claim.");

    // Rebuilds the GeneratedMap from the columns and the decoded bytes. A row whose bytes do not decode
    // throws InvalidDataException (a 500 for that one map, reported to the error tracker).
    private static SavedMap ToSavedMap(SavedMapRow row) =>
        new(row.Id, row.Title, row.CreatedAt, row.UpdatedAt, new GeneratedMap(
            (uint)row.Seed,
            new MapParameters(row.RoomCount, row.Encounter, row.BossSize),
            row.Width,
            row.Height,
            GridCodec.DecodeCells(row.Cells, row.Width, row.Height),
            GridCodec.DecodeRooms(row.Rooms)));
}
