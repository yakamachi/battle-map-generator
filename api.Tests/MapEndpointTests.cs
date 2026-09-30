using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BattleMapGenerator.Api.Maps;
using BattleMapGenerator.Api.Tests.Infrastructure;

namespace BattleMapGenerator.Api.Tests;

// Generation never needs the database, so these tests run without the SQL Server container: every
// factory points at a database that cannot be reached, and a request that touched it would fail.
// The rate limiter counts per app instance, so each test builds its own factory, and only the 429
// test makes more than 10 generate calls.
public sealed class MapEndpointTests
{
    private const string GeneratePath = "/api/maps/generate";


    [Fact]
    public async Task Generate_returns_the_default_size_grid_with_string_cell_kinds_for_the_given_seed()
    {
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

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
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

        var withoutBody = await client.PostAsync(GeneratePath, new StringContent("", null, "application/json"));
        var withoutSeed = await client.PostAsJsonAsync(GeneratePath, new { });

        Assert.Equal(HttpStatusCode.OK, withoutBody.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withoutSeed.StatusCode);
        using var first = JsonDocument.Parse(await withoutBody.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await withoutSeed.Content.ReadAsStringAsync());
        Assert.Equal(MapSize.DefaultWidth * MapSize.DefaultHeight, first.RootElement.GetProperty("cells").GetArrayLength());
        Assert.NotEqual(first.RootElement.GetProperty("seed").GetUInt32(), second.RootElement.GetProperty("seed").GetUInt32());
    }

    [Fact]
    public async Task The_eleventh_generate_within_a_minute_returns_429_with_retry_after()
    {
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

        for (var i = 1; i <= 10; i++)
        {
            var allowed = await client.PostAsJsonAsync(GeneratePath, new { seed = i });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(GeneratePath, new { seed = 11 });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta is { } delay && delay > TimeSpan.Zero && delay <= TimeSpan.FromMinutes(1),
            $"Retry-After was '{rejected.Headers.RetryAfter}'.");
    }

    // The database lesson: startup and generation succeed, quickly, while the database is unreachable.
    [Fact]
    public async Task Generate_succeeds_quickly_with_an_unreachable_database()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var factory = new ApiFactory(ApiFactory.UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(GeneratePath, new { seed = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Startup and generate took {stopwatch.Elapsed}.");
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
}
