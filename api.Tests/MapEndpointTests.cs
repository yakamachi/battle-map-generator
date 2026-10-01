using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleMapGenerator.Api.Maps;
using BattleMapGenerator.Api.Tests.Infrastructure;

namespace BattleMapGenerator.Api.Tests;

// Generation requires a session, so tests that generate log in first on the shared SQL Server
// database (the session cookie is read through the key ring stored there). Generation itself never
// touches the database, and an anonymous request is refused without it: the tests that assert the
// database is never touched point at one that cannot be reached. The rate limiter counts per app instance, so each test builds its
// own factory, and only the 429 test makes more than 10 generate calls for one account.
[Collection(SqlServerCollection.Name)]
public sealed class MapEndpointTests(SqlServerFixture sql)
{
    private const string GeneratePath = "/api/maps/generate";

    [Fact]
    public async Task Generate_returns_the_default_size_grid_with_string_cell_kinds_for_the_given_seed()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath, new { seed = 42 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(42u, root.GetProperty("seed").GetUInt32());
        Assert.Equal(MapSize.DefaultWidth, root.GetProperty("width").GetInt32());
        Assert.Equal(MapSize.DefaultHeight, root.GetProperty("height").GetInt32());

        var cells = root.GetProperty("cells").EnumerateArray().Select(cell => cell.GetString()).ToList();
        Assert.Equal(MapSize.DefaultWidth * MapSize.DefaultHeight, cells.Count);
        Assert.All(cells, cell => Assert.Contains(cell, new[] { "void", "floor", "corridor", "wall", "door" }));
        Assert.Contains("door", cells);

        var room = root.GetProperty("rooms")[0];
        foreach (var property in new[] { "x", "y", "width", "height" })
        {
            Assert.Equal(JsonValueKind.Number, room.GetProperty(property).ValueKind);
        }

        // The response is exactly the generator's output for that seed.
        var expected = BspGenerator.Generate(42, MapSize.DefaultWidth, MapSize.DefaultHeight);
        Assert.Equal(expected.Cells.Select(cell => JsonNamingPolicy.CamelCase.ConvertName(cell.ToString())), cells);
    }

    // The body is optional but still JSON: a POST with no Content-Type at all does not match the
    // endpoint's accepted content types and falls through to the /api 404 guard.
    [Fact]
    public async Task Generate_with_an_empty_body_or_no_seed_draws_a_seed()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var withoutBody = await client.PostAsync(GeneratePath, new StringContent("", null, "application/json"));
        var withoutSeed = await client.PostAsJsonAsync(GeneratePath, new { });

        Assert.Equal(HttpStatusCode.OK, withoutBody.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withoutSeed.StatusCode);
        using var first = JsonDocument.Parse(await withoutBody.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await withoutSeed.Content.ReadAsStringAsync());
        Assert.Equal(MapSize.DefaultWidth * MapSize.DefaultHeight, first.RootElement.GetProperty("cells").GetArrayLength());
        Assert.NotEqual(first.RootElement.GetProperty("seed").GetUInt32(), second.RootElement.GetProperty("seed").GetUInt32());
    }

    // The limit is per account: a second account behind the same client IP keeps its own budget.
    [Fact]
    public async Task The_eleventh_generate_within_a_minute_returns_429_with_retry_after()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        for (var i = 1; i <= 10; i++)
        {
            var allowed = await client.PostAsJsonAsync(GeneratePath, new { seed = i });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(GeneratePath, new { seed = 11 });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta is { } delay && delay > TimeSpan.Zero && delay <= TimeSpan.FromMinutes(1),
            $"Retry-After was '{rejected.Headers.RetryAfter}'.");

        var secondAccount = await factory.CreateLoggedInClientAsync();
        var other = await secondAccount.PostAsJsonAsync(GeneratePath, new { seed = 12 });
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    // A caller that skips the UI gets a bare 401: no redirect, no SPA shell, no session cookie.
    // Authorization runs before the limiter, so eleven refused calls spend no permit: the account
    // that logs in afterwards still has all 10 in the same app instance.
    [Fact]
    public async Task Anonymous_generate_returns_401_and_spends_no_permits()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var anonymous = factory.CreateClient();

        for (var i = 1; i <= 11; i++)
        {
            var response = i % 2 == 0
                ? await anonymous.PostAsync(GeneratePath, new StringContent("", null, "application/json"))
                : await anonymous.PostAsJsonAsync(GeneratePath, new { seed = i });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.DoesNotContain(ApiFactory.SpaShellMarker, await response.Content.ReadAsStringAsync());
        }

        var client = await factory.CreateLoggedInClientAsync();
        for (var i = 1; i <= 10; i++)
        {
            var allowed = await client.PostAsJsonAsync(GeneratePath, new { seed = i });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }
    }

    // The database lesson: startup and an anonymous generate are answered, quickly, while the
    // database is unreachable. A request without a session cookie never loads the key ring.
    [Fact]
    public async Task Startup_and_anonymous_generate_answer_401_quickly_with_an_unreachable_database()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(GeneratePath, new { seed = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Startup and anonymous generate took {stopwatch.Elapsed}.");
    }

    // ApiFactory serves a stub index.html, so without the /api guard these paths would get 200.
    [Theory]
    [InlineData("GET", "/api/maps/does-not-exist")]
    [InlineData("POST", "/api/maps/does-not-exist")]
    [InlineData("POST", "/api/maps/generate/extra")]
    public async Task Unknown_maps_paths_return_404(string method, string path)
    {
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Generate_applies_and_echoes_the_parameters()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath,
            new { seed = 42, roomCount = 8, encounter = "boss", bossSize = "huge" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var parameters = root.GetProperty("parameters");
        Assert.Equal(8, parameters.GetProperty("roomCount").GetInt32());
        Assert.Equal("boss", parameters.GetProperty("encounter").GetString());
        Assert.Equal("huge", parameters.GetProperty("bossSize").GetString());

        var (width, height) = MapSize.For(8, BossSize.Huge);
        Assert.Equal(width, root.GetProperty("width").GetInt32());
        Assert.Equal(height, root.GetProperty("height").GetInt32());

        var rooms = root.GetProperty("rooms").EnumerateArray().Select(room => room.GetProperty("kind").GetString()).ToList();
        Assert.Equal(8, rooms.Count);
        Assert.Single(rooms, kind => kind == "bossArena");
        Assert.Equal(7, rooms.Count(kind => kind == "room"));

        // The response is exactly the generator's output for that seed and those parameters.
        var expected = BspGenerator.Generate(42, new MapParameters(8, EncounterType.Boss, BossSize.Huge));
        var cells = root.GetProperty("cells").EnumerateArray().Select(cell => cell.GetString()).ToList();
        Assert.Equal(expected.Cells.Select(cell => JsonNamingPolicy.CamelCase.ConvertName(cell.ToString())), cells);
        Assert.Contains("bossArena", cells);
    }

    [Fact]
    public async Task Generate_without_parameters_echoes_the_defaults()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath, new { seed = 42 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var parameters = json.RootElement.GetProperty("parameters");
        Assert.Equal(MapSize.DefaultRoomCount, parameters.GetProperty("roomCount").GetInt32());
        Assert.Equal("skirmish", parameters.GetProperty("encounter").GetString());
        Assert.Equal(JsonValueKind.Null, parameters.GetProperty("bossSize").ValueKind);
        Assert.Equal(MapSize.DefaultRoomCount, json.RootElement.GetProperty("rooms").GetArrayLength());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public async Task A_room_count_outside_the_limits_returns_400_naming_the_field(int roomCount)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath, new { seed = 42, roomCount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("roomCount", out _));
    }

    [Fact]
    public async Task A_boss_fight_without_a_boss_size_returns_400_naming_the_field()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath, new { seed = 42, encounter = "boss" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("bossSize", out _));
    }

    [Theory]
    [InlineData("""{ "seed": 42, "encounter": "ambush" }""")]
    [InlineData("""{ "seed": 42, "encounter": "boss", "bossSize": "colossal" }""")]
    [InlineData("""{ "seed": 42, "encounter": 7 }""")]
    public async Task An_unknown_enum_value_returns_400(string body)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync(GeneratePath, new StringContent(body, null, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_boss_size_sent_with_a_skirmish_is_ignored_and_echoed_as_null()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath,
            new { seed = 42, encounter = "skirmish", bossSize = "gargantuan" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("skirmish", root.GetProperty("parameters").GetProperty("encounter").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parameters").GetProperty("bossSize").ValueKind);
        Assert.Equal(MapSize.DefaultWidth, root.GetProperty("width").GetInt32());
        Assert.Equal(MapSize.DefaultHeight, root.GetProperty("height").GetInt32());
        Assert.DoesNotContain("bossArena", root.GetProperty("cells").EnumerateArray().Select(cell => cell.GetString()));
    }

    // The contract lists the enums as strings: a number and a number in a string are rejected.
    [Theory]
    [InlineData("""{ "seed": 42, "encounter": 1, "bossSize": "large" }""")]
    [InlineData("""{ "seed": 42, "encounter": "1", "bossSize": "large" }""")]
    [InlineData("""{ "seed": 42, "encounter": "boss", "bossSize": 2 }""")]
    [InlineData("""{ "seed": 42, "encounter": "skirmish", "bossSize": 9 }""")]
    public async Task An_enum_sent_as_a_number_returns_400(string body)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync(GeneratePath, new StringContent(body, null, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // What the web client sends before it knows about parameters: no body, or a body without them.
    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{ "seed": null }""")]
    public async Task An_empty_body_or_a_body_without_parameters_gives_the_default_map(string body)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync(GeneratePath, new StringContent(body, null, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(MapSize.DefaultWidth, root.GetProperty("width").GetInt32());
        Assert.Equal(MapSize.DefaultHeight, root.GetProperty("height").GetInt32());
        Assert.Equal(MapSize.DefaultRoomCount, root.GetProperty("rooms").GetArrayLength());
        var parameters = root.GetProperty("parameters");
        Assert.Equal(MapSize.DefaultRoomCount, parameters.GetProperty("roomCount").GetInt32());
        Assert.Equal("skirmish", parameters.GetProperty("encounter").GetString());
        Assert.Equal(JsonValueKind.Null, parameters.GetProperty("bossSize").ValueKind);
    }

    // A value JSON binding rejects still gets a validation problem naming the field.
    [Theory]
    [InlineData("""{ "encounter": "ambush" }""", "encounter")]
    [InlineData("""{ "encounter": 1 }""", "encounter")]
    [InlineData("""{ "encounter": "boss", "bossSize": "colossal" }""", "bossSize")]
    [InlineData("""{ "roomCount": "8" }""", "roomCount")]
    [InlineData("""{ "seed": -1 }""", "seed")]
    [InlineData("""{ "roomCount": 8""", "request")]
    [InlineData("""[1, 2]""", "request")]
    public async Task A_value_the_body_cannot_hold_returns_a_validation_problem_naming_the_field(string body, string field)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync(GeneratePath, new StringContent(body, null, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages), $"errors: {errors}");
        Assert.NotEqual("", messages[0].GetString());
    }

    // The converter reads a comma list as combined flags; an undefined combination is a 400, not a 500.
    [Fact]
    public async Task A_comma_list_boss_size_that_is_no_single_size_returns_400()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync(GeneratePath,
            new StringContent("""{ "encounter": "boss", "bossSize": "huge, gargantuan" }""", null, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("bossSize", out _));
    }

    [Fact]
    public async Task A_room_count_out_of_range_says_so_in_words()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(GeneratePath, new { roomCount = 13 });

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(GenerateMapRequest.RoomCountMessage,
            json.RootElement.GetProperty("errors").GetProperty("roomCount")[0].GetString());
    }

    // The body is read by the endpoint, but the endpoint still only takes JSON.
    [Fact]
    public async Task A_body_that_is_not_json_is_not_accepted()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = await factory.CreateLoggedInClientAsync();

        var response = await client.PostAsync(GeneratePath, new StringContent("roomCount=8", null, "text/plain"));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
