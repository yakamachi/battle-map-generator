using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BattleMapGenerator.Api.Maps;
using BattleMapGenerator.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SavedMapRow = BattleMapGenerator.Api.Data.SavedMap;

namespace BattleMapGenerator.Api.Tests;

// The saved-maps library through the HTTP API. Each test builds its own factory (the auth and generate
// limiters count per app instance) and its accounts get unique emails. Tests that assert on how many
// maps exist, or that nothing was stored, get a fresh database of their own (lessons.md, "API tests
// control their own configuration and never share counted state").
[Collection(SqlServerCollection.Name)]
public sealed class SavedMapEndpointTests(SqlServerFixture sql)
{
    private const string MapsPath = "/api/maps";
    private const string GeneratePath = "/api/maps/generate";

    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Saved_map_round_trips_with_the_generated_grid()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var saved = await SaveAsync(client, new { title = "Crypt", seed = 42, roomCount = 8, encounter = "boss", bossSize = "huge" });

        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        using var savedJson = await ReadJsonAsync(saved);
        var id = savedJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal($"{MapsPath}/{id}", saved.Headers.Location?.OriginalString);

        var opened = await client.GetAsync(saved.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        using var openedJson = await ReadJsonAsync(opened);
        Assert.Equal("Crypt", openedJson.RootElement.GetProperty("title").GetString());

        // Both the save response and the opened map are exactly the generator's output.
        var expected = BspGenerator.Generate(42, new MapParameters(8, EncounterType.Boss, BossSize.Huge));
        using var expectedJson = JsonDocument.Parse(JsonSerializer.Serialize(expected, MapDeterminismTests.Json()));
        Assert.True(JsonElement.DeepEquals(expectedJson.RootElement, savedJson.RootElement.GetProperty("map")));
        Assert.True(JsonElement.DeepEquals(expectedJson.RootElement, openedJson.RootElement.GetProperty("map")));
    }

    [Fact]
    public async Task List_shows_only_own_maps_newest_first()
    {
        var time = new ManualTimeProvider(T0);
        await using var factory = new ApiFactory(await sql.CreateMigratedDatabaseAsync()) { Time = time };
        var first = await factory.CreateLoggedInClientAsync();
        var second = await factory.CreateLoggedInClientAsync();

        await SaveAsync(first, new { title = "Oldest", seed = 1 });
        time.Advance(TimeSpan.FromMinutes(1));
        await SaveAsync(second, new { title = "Someone else's", seed = 2 });
        time.Advance(TimeSpan.FromMinutes(1));
        await SaveAsync(first, new { title = "Middle", seed = 3, roomCount = 3 });
        time.Advance(TimeSpan.FromMinutes(1));
        await SaveAsync(first, new { title = "Newest", seed = 4, encounter = "boss", bossSize = "large" });

        var list = await first.GetAsync(MapsPath);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var json = await ReadJsonAsync(list);
        var items = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(["Newest", "Middle", "Oldest"], items.Select(item => item.GetProperty("title").GetString()));
        Assert.Equal([T0.AddMinutes(3), T0.AddMinutes(2), T0],
            items.Select(item => item.GetProperty("createdAt").GetDateTimeOffset()));

        // The summary carries what the list shows, without the grid.
        var newest = items[0];
        var expected = BspGenerator.Generate(4, new MapParameters(MapSize.DefaultRoomCount, EncounterType.Boss, BossSize.Large));
        Assert.Equal(4u, newest.GetProperty("seed").GetUInt32());
        Assert.Equal(expected.Width, newest.GetProperty("width").GetInt32());
        Assert.Equal(expected.Height, newest.GetProperty("height").GetInt32());
        Assert.Equal("boss", newest.GetProperty("parameters").GetProperty("encounter").GetString());
        Assert.Equal("large", newest.GetProperty("parameters").GetProperty("bossSize").GetString());
        Assert.Equal(T0.AddMinutes(3), newest.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.False(newest.TryGetProperty("cells", out _));
        Assert.False(newest.TryGetProperty("map", out _));

        using var secondJson = await ReadJsonAsync(await second.GetAsync(MapsPath));
        Assert.Equal(["Someone else's"],
            secondJson.RootElement.EnumerateArray().Select(item => item.GetProperty("title").GetString()));
    }

    // Ownership is part of every query: another account's map answers exactly like a missing one.
    [Fact]
    public async Task Another_accounts_map_is_404_for_get_rename_and_delete()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var intruder = await factory.CreateLoggedInClientAsync();
        var owner = await factory.CreateLoggedInClientAsync();
        var saved = await SaveAsync(owner, new { title = "Owner's map", seed = 7 });
        using var savedJson = await ReadJsonAsync(saved);
        var path = saved.Headers.Location!.OriginalString;

        var get = await intruder.GetAsync(path);
        var rename = await intruder.PatchAsJsonAsync(path, new { title = "Taken over" });
        var delete = await intruder.DeleteAsync(path);
        var missing = await intruder.GetAsync($"{MapsPath}/{Guid.NewGuid()}");

        foreach (var response in new[] { get, rename, delete, missing })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }

        var untouched = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, untouched.StatusCode);
        using var untouchedJson = await ReadJsonAsync(untouched);
        Assert.True(JsonElement.DeepEquals(savedJson.RootElement, untouchedJson.RootElement));
    }

    [Fact]
    public async Task Rename_changes_title_and_updated_at_only()
    {
        var time = new ManualTimeProvider(T0);
        await using var factory = new ApiFactory(sql.ConnectionString) { Time = time };
        var client = await factory.CreateLoggedInClientAsync();
        var saved = await SaveAsync(client, new { title = "Old name", seed = 9 });
        using var savedJson = await ReadJsonAsync(saved);
        var path = saved.Headers.Location!.OriginalString;
        time.Advance(TimeSpan.FromMinutes(5));

        var renamed = await client.PatchAsJsonAsync(path, new { title = "New name" });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var opened = await client.GetAsync(path);
        using var openedJson = await ReadJsonAsync(opened);
        using var renamedJson = await ReadJsonAsync(renamed);
        foreach (var root in new[] { renamedJson.RootElement, openedJson.RootElement })
        {
            Assert.Equal(savedJson.RootElement.GetProperty("id").GetGuid(), root.GetProperty("id").GetGuid());
            Assert.Equal("New name", root.GetProperty("title").GetString());
            Assert.Equal(T0, root.GetProperty("createdAt").GetDateTimeOffset());
            Assert.Equal(T0.AddMinutes(5), root.GetProperty("updatedAt").GetDateTimeOffset());
            Assert.True(JsonElement.DeepEquals(savedJson.RootElement.GetProperty("map"), root.GetProperty("map")));
        }
    }

    [Fact]
    public async Task Delete_removes_the_map_and_a_second_delete_is_404()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var kept = await SaveAsync(client, new { title = "Kept", seed = 10 });
        var saved = await SaveAsync(client, new { title = "Doomed", seed = 11 });
        var path = saved.Headers.Location!.OriginalString;

        var deleted = await client.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync(path, new { title = "Back" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(kept.Headers.Location)).StatusCode);
        using var list = await ReadJsonAsync(await client.GetAsync(MapsPath));
        Assert.Equal(["Kept"], list.RootElement.EnumerateArray().Select(item => item.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Title_rules_are_enforced_on_save_and_rename()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var existing = await SaveAsync(client, new { title = "Existing", seed = 12 });
        var path = existing.Headers.Location!.OriginalString;

        // A control character left after trimming (a newline, a tab, a bell) is refused like a bad length.
        string?[] invalid = ["", "   ", new string('t', 81), null, "Two\nlines", "Tab\tinside", "Bell\u0007", "Ends\r\nhere\u0000"];
        foreach (var title in invalid)
        {
            foreach (var response in new[]
            {
                await SaveAsync(client, new { title, seed = 13 }),
                await client.PatchAsJsonAsync(path, new { title }),
            })
            {
                var errors = await ReadValidationErrorsAsync(response);
                Assert.Equal(["title"], errors.Keys);
                Assert.Equal([SavedMapTitleAttribute.Message], errors["title"]);
            }
        }

        // A body without a title at all is the same error.
        var withoutTitle = await ReadValidationErrorsAsync(await client.PatchAsJsonAsync(path, new { }));
        Assert.Equal(["title"], withoutTitle.Keys);

        // 80 characters, duplicates and surrounding whitespace are fine; the stored title is trimmed.
        var longest = new string('t', 80);
        Assert.Equal(longest, await SavedTitleAsync(await SaveAsync(client, new { title = longest, seed = 14 })));
        Assert.Equal("Existing", await SavedTitleAsync(await SaveAsync(client, new { title = "Existing", seed = 15 })));
        Assert.Equal("Trimmed", await SavedTitleAsync(await SaveAsync(client, new { title = "  Trimmed  ", seed = 16 })));
        Assert.Equal("Renamed", await SavedTitleAsync(await client.PatchAsJsonAsync(path, new { title = "\tRenamed " })));
        Assert.Equal(longest, await SavedTitleAsync(await client.PatchAsJsonAsync(path, new { title = $" {longest} " })));
    }

    // Save takes generate's fields, so the same invalid body gets the same errors from both.
    [Theory]
    [InlineData("""{ "seed": 42, "roomCount": 13 }""", "roomCount")]
    [InlineData("""{ "seed": 42, "roomCount": "8" }""", "roomCount")]
    [InlineData("""{ "seed": 42, "encounter": "ambush" }""", "encounter")]
    [InlineData("""{ "seed": 42, "encounter": 1 }""", "encounter")]
    [InlineData("""{ "seed": 42, "encounter": "boss", "bossSize": "colossal" }""", "bossSize")]
    [InlineData("""{ "seed": 42, "encounter": "boss", "bossSize": "huge, gargantuan" }""", "bossSize")]
    [InlineData("""{ "seed": 42, "encounter": "boss" }""", "bossSize")]
    [InlineData("""{ "seed": -1 }""", "seed")]
    public async Task Invalid_parameters_on_save_use_the_generate_messages(string generateBody, string field)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var saveBody = generateBody.Replace("{ ", """{ "title": "Map", """, StringComparison.Ordinal);

        var fromGenerate = await ReadValidationErrorsAsync(await PostJsonAsync(client, GeneratePath, generateBody));
        var fromSave = await ReadValidationErrorsAsync(await PostJsonAsync(client, MapsPath, saveBody));

        Assert.Equal([field], fromSave.Keys);
        Assert.Equal(fromGenerate, fromSave);
    }

    // Unlike generate, Save needs the seed: the map is regenerated from it.
    [Fact]
    public async Task Save_without_a_seed_or_with_a_body_that_is_not_an_object_is_refused()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var withoutSeed = await ReadValidationErrorsAsync(await PostJsonAsync(client, MapsPath, """{ "title": "Map" }"""));
        Assert.Equal(["seed"], withoutSeed.Keys);
        Assert.Equal([SaveMapRequest.SeedRequiredMessage], withoutSeed["seed"]);

        var empty = await ReadValidationErrorsAsync(await PostJsonAsync(client, MapsPath, ""));
        Assert.Equal(["seed", "title"], empty.Keys.Order());

        foreach (var body in new[] { """{ "title": "Map", "seed": 1""", "[1, 2]" })
        {
            var errors = await ReadValidationErrorsAsync(await PostJsonAsync(client, MapsPath, body));
            Assert.Equal(["request"], errors.Keys);
        }
    }

    // The 100 rows are written straight to the database; the cap is checked through the API.
    [Fact]
    public async Task Save_is_refused_at_the_100_map_cap()
    {
        var connectionString = await sql.CreateMigratedDatabaseAsync();
        await using var factory = new ApiFactory(connectionString);
        var email = ApiFactory.NewEmail();
        var client = await factory.CreateLoggedInClientAsync(email);
        await using (var db = SqlServerFixture.CreateContext(connectionString))
        {
            var ownerId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
            var map = BspGenerator.Generate(1, MapSize.DefaultWidth, MapSize.DefaultHeight);
            for (var i = 0; i < SavedMapEndpoints.MaxSavedMapsPerAccount; i++)
            {
                db.SavedMaps.Add(Row(ownerId, $"Map {i}", map, T0.AddMinutes(i)));
            }
            await db.SaveChangesAsync();
        }

        var refused = await SaveAsync(client, new { title = "One too many", seed = 2 });

        var errors = await ReadValidationErrorsAsync(refused);
        Assert.Equal(["request"], errors.Keys);
        Assert.Equal([SavedMapEndpoints.CapMessage], errors["request"]);
        Assert.Equal(SavedMapEndpoints.MaxSavedMapsPerAccount, await CountMapsAsync(connectionString));

        // Deleting one makes room for another.
        using var list = await ReadJsonAsync(await client.GetAsync(MapsPath));
        Assert.Equal(SavedMapEndpoints.MaxSavedMapsPerAccount, list.RootElement.GetArrayLength());
        var oldest = list.RootElement[SavedMapEndpoints.MaxSavedMapsPerAccount - 1].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{MapsPath}/{oldest}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SaveAsync(client, new { title = "Fits now", seed = 2 })).StatusCode);
    }

    // The database lesson: every library route refuses an anonymous request with a bare 401 before
    // resolving AppDbContext, so startup and all five answer quickly with an unreachable database.
    [Fact]
    public async Task Library_routes_answer_401_without_a_session_and_never_touch_the_database()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();
        var path = $"{MapsPath}/{Guid.NewGuid()}";

        var responses = new[]
        {
            await client.GetAsync(MapsPath),
            await client.GetAsync(path),
            await client.PostAsJsonAsync(MapsPath, new { title = "Map", seed = 1 }),
            await client.PatchAsJsonAsync(path, new { title = "Map" }),
            await client.DeleteAsync(path),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.DoesNotContain(ApiFactory.SpaShellMarker, await response.Content.ReadAsStringAsync());
        }
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Startup and the library routes took {stopwatch.Elapsed}.");
    }

    // There are no antiforgery tokens: Save and Rename take JSON only, which a cross-site form cannot send.
    [Fact]
    public async Task Form_and_text_bodies_are_refused_on_save_and_rename()
    {
        var connectionString = await sql.CreateMigratedDatabaseAsync();
        await using var factory = new ApiFactory(connectionString);
        var client = await factory.CreateLoggedInClientAsync();

        foreach (var contentType in new[] { "application/x-www-form-urlencoded", "text/plain", "multipart/form-data" })
        {
            var body = contentType == "text/plain" ? """{"title":"Forged","seed":1}""" : "title=Forged&seed=1";
            var response = await client.PostAsync(MapsPath, new StringContent(body, Encoding.UTF8, contentType));
            Assert.False(response.IsSuccessStatusCode, $"Save accepted {contentType} with {(int)response.StatusCode}.");
        }
        // A cross-site no-cors fetch can send a body with no Content-Type at all, without a preflight.
        var withoutContentType = await client.PostAsync(MapsPath, WithoutContentType("""{"title":"Forged","seed":1}"""));
        Assert.False(withoutContentType.IsSuccessStatusCode, $"Save accepted no Content-Type with {(int)withoutContentType.StatusCode}.");
        Assert.Equal(0, await CountMapsAsync(connectionString));

        var saved = await SaveAsync(client, new { title = "Original", seed = 1 });
        var path = saved.Headers.Location!.OriginalString;
        foreach (var contentType in new[] { "application/x-www-form-urlencoded", "text/plain", "multipart/form-data" })
        {
            var body = contentType == "text/plain" ? """{"title":"Forged"}""" : "title=Forged";
            var response = await client.PatchAsync(path, new StringContent(body, Encoding.UTF8, contentType));
            Assert.False(response.IsSuccessStatusCode, $"Rename accepted {contentType} with {(int)response.StatusCode}.");
        }
        var renameWithoutContentType = await client.PatchAsync(path, WithoutContentType("""{"title":"Forged"}"""));
        Assert.False(renameWithoutContentType.IsSuccessStatusCode,
            $"Rename accepted no Content-Type with {(int)renameWithoutContentType.StatusCode}.");
        Assert.Equal("Original", await SavedTitleAsync(await client.GetAsync(path)));
        Assert.Equal(1, await CountMapsAsync(connectionString));
    }

    // The list reads summary columns only, so a row whose grid bytes are unreadable cannot break it;
    // only opening that one map fails.
    [Fact]
    public async Task List_still_answers_when_a_rows_cells_are_corrupted()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var healthy = await SaveAsync(client, new { title = "Healthy", seed = 21 });
        var broken = await SaveAsync(client, new { title = "Broken", seed = 22, roomCount = 3 });
        using var brokenJson = await ReadJsonAsync(broken);
        var brokenId = brokenJson.RootElement.GetProperty("id").GetGuid();
        await using (var db = SqlServerFixture.CreateContext(sql.ConnectionString))
        {
            var updated = await db.SavedMaps
                .Where(m => m.Id == brokenId)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.Cells, new byte[] { 0xFF, 0xFF, 0xFF }));
            Assert.Equal(1, updated);
        }

        var list = await client.GetAsync(MapsPath);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = await ReadJsonAsync(list);
        var items = listJson.RootElement.EnumerateArray().ToList();
        Assert.Equal(["Broken", "Healthy"], items.Select(item => item.GetProperty("title").GetString()).Order());
        var brokenItem = items.Single(item => item.GetProperty("id").GetGuid() == brokenId);
        Assert.Equal(3, brokenItem.GetProperty("parameters").GetProperty("roomCount").GetInt32());
        Assert.Equal("skirmish", brokenItem.GetProperty("parameters").GetProperty("encounter").GetString());

        // Opening the corrupted map fails (the codec refuses the bytes); the healthy one still opens.
        await AssertServerErrorAsync(() => client.GetAsync(broken.Headers.Location));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(healthy.Headers.Location)).StatusCode);
    }

    // Real bodies are well under 1 KB; a body over 16 KB is refused before it is buffered, and nothing
    // is stored or changed. A body of exactly 16 KB still goes through.
    [Fact]
    public async Task A_body_over_16_kb_is_refused_on_save_rename_and_generate()
    {
        var connectionString = await sql.CreateMigratedDatabaseAsync();
        await using var factory = new ApiFactory(connectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var saved = await SaveAsync(client, new { title = "Original", seed = 1 });
        var path = saved.Headers.Location!.OriginalString;
        const int limit = 16 * 1024;

        var save = await PostJsonAsync(client, MapsPath, Padded("""{"title":"Too big","seed":2}""", limit + 1));
        var rename = await PatchJsonAsync(client, path, Padded("""{"title":"Too big"}""", limit + 1));
        var generate = await PostJsonAsync(client, GeneratePath, Padded("""{"seed":3}""", limit + 1));

        foreach (var response in new[] { save, rename, generate })
        {
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        Assert.Equal(1, await CountMapsAsync(connectionString));
        Assert.Equal("Original", await SavedTitleAsync(await client.GetAsync(path)));

        Assert.Equal(HttpStatusCode.Created, (await PostJsonAsync(client, MapsPath, Padded("""{"title":"Fits","seed":2}""", limit))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchJsonAsync(client, path, Padded("""{"title":"Fits"}""", limit))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostJsonAsync(client, GeneratePath, Padded("""{"seed":3}""", limit))).StatusCode);
    }

    // Save regenerates the map but is not rate-limited as a generation: after more saves than the
    // per-account generate limit, generate still has its whole budget.
    [Fact]
    public async Task Save_does_not_spend_a_generate_permit()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        for (var i = 1; i <= MapEndpoints.PerAccountGenerateLimit + 1; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await SaveAsync(client, new { title = $"Map {i}", seed = i })).StatusCode);
        }

        for (var i = 1; i <= MapEndpoints.PerAccountGenerateLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(GeneratePath, new { seed = i })).StatusCode);
        }
    }

    // ApiFactory serves a stub index.html, so without the /api guard these paths would get 200.
    [Theory]
    [InlineData("GET", "/api/maps/not-a-guid")]
    [InlineData("PATCH", "/api/maps/not-a-guid")]
    [InlineData("DELETE", "/api/maps/not-a-guid")]
    [InlineData("GET", "/api/maps/generate")]
    [InlineData("DELETE", "/api/maps/generate")]
    [InlineData("PUT", "/api/maps")]
    public async Task Non_guid_id_falls_to_the_api_404(string method, string path)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method is "PATCH" or "PUT" ? JsonContent.Create(new { title = "Map" }) : null,
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(ApiFactory.SpaShellMarker, await response.Content.ReadAsStringAsync());
    }

    private static SavedMapRow Row(string ownerId, string title, GeneratedMap map, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        Title = title,
        Seed = map.Seed,
        Width = map.Width,
        Height = map.Height,
        RoomCount = map.Parameters.RoomCount,
        Encounter = map.Parameters.Encounter,
        BossSize = map.Parameters.BossSize,
        Cells = GridCodec.EncodeCells(map.Cells),
        Rooms = GridCodec.EncodeRooms(map.Rooms),
        CreatedAt = at,
        UpdatedAt = at,
    };

    // JSON followed by spaces up to exactly the given number of UTF-8 bytes (still valid JSON).
    private static string Padded(string json, int bytes) => json + new string(' ', bytes - Encoding.UTF8.GetByteCount(json));

    private static ByteArrayContent WithoutContentType(string body)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        Assert.Null(content.Headers.ContentType);
        return content;
    }

    // The app has no exception handler: TestServer hands an unhandled exception to the client instead of
    // the 500 Kestrel would send. Either way the request does not succeed.
    private static async Task AssertServerErrorAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            var response = await send();
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        catch (InvalidDataException)
        {
        }
    }

    private static Task<HttpResponseMessage> PatchJsonAsync(HttpClient client, string path, string body) =>
        client.PatchAsync(path, new StringContent(body, null, "application/json"));

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync(MapsPath, body);

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string body) =>
        client.PostAsync(path, new StringContent(body, null, "application/json"));

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<string?> SavedTitleAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("title").GetString();
    }

    private static async Task<int> CountMapsAsync(string connectionString)
    {
        await using var db = SqlServerFixture.CreateContext(connectionString);
        return await db.SavedMaps.CountAsync();
    }

    private static async Task<Dictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            field => field.Name,
            field => field.Value.EnumerateArray().Select(message => message.GetString()!).ToArray());
    }
}
