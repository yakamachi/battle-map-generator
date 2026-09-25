using System.Diagnostics;
using System.Net;
using battle_map_generator_api.Tests.Infrastructure;

namespace battle_map_generator_api.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class HealthProbeTests(SqlServerFixture sql)
{
    // Nothing listens on port 1, so the connection is refused at once. The short Connect Timeout
    // keeps the suite fast even if the refusal turns into a timeout on some network setups.
    private const string UnreachableConnectionString =
        "Server=127.0.0.1,1;Database=battlemap;User Id=sa;Password=unused;Connect Timeout=2;Encrypt=False";

    [Fact]
    public async Task Live_and_ready_return_200_against_the_database()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateReadyClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/ready")).StatusCode);
    }

    // The key ring loads lazily: app startup and liveness never touch the database, so a cold start
    // does not wake a paused Azure SQL database. Only ready (and, later, auth) reads the key ring.
    // A database of its own keeps other tests' keys out of the count.
    [Fact]
    public async Task Startup_and_live_do_not_touch_the_database_but_ready_creates_the_key()
    {
        var connectionString = await sql.CreateMigratedDatabaseAsync();
        await using var factory = new ApiFactory(connectionString);
        var client = factory.CreateReadyClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
        Assert.Equal(0, await SqlServerFixture.CountKeysAsync(connectionString));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/ready")).StatusCode);
        Assert.Equal(1, await SqlServerFixture.CountKeysAsync(connectionString));
    }

    [Fact]
    public async Task Startup_and_live_succeed_quickly_with_an_unreachable_database()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var factory = new ApiFactory(UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Startup and live took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Ready_returns_503_with_an_unreachable_database()
    {
        await using var factory = new ApiFactory(UnreachableConnectionString);
        var client = factory.CreateReadyClient();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/health/ready")).StatusCode);
    }

    // A missing connection string (e.g. a lost app setting) must not crash the host:
    // live keeps answering and ready reports the database as unavailable.
    [Fact]
    public async Task Missing_connection_string_keeps_live_up_and_fails_ready()
    {
        await using var factory = new ApiFactory(connectionString: null);
        var client = factory.CreateReadyClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/health/ready")).StatusCode);
    }

    // Fail closed: 404 and no check runs. The Data Protection check would write the first key
    // to this empty key ring, so a key count of zero proves no check ran.
    [Theory]
    [InlineData(null, ApiFactory.ReadyKey)]       // no header
    [InlineData("wrong-key", ApiFactory.ReadyKey)] // wrong key
    [InlineData(ApiFactory.ReadyKey, null)]        // no Health:ReadyKey configured
    [InlineData("", "")]                           // empty key configured and sent
    public async Task Ready_returns_404_and_runs_no_check_without_a_valid_key(string? sentKey, string? configuredKey)
    {
        var connectionString = await sql.CreateMigratedDatabaseAsync();
        await using var factory = new ApiFactory(connectionString, configuredKey);
        var client = factory.CreateClient();
        if (sentKey is not null)
        {
            client.DefaultRequestHeaders.Add(ApiFactory.ReadyKeyHeader, sentKey);
        }

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await SqlServerFixture.CountKeysAsync(connectionString));
    }

    // ApiFactory serves a stub index.html, so without the /api guard these paths would get 200.
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/does-not-exist")]
    public async Task Removed_and_unknown_api_paths_return_404(string path)
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Deep_links_outside_api_return_the_spa_shell()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/maps/some-page");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ApiFactory.SpaShellMarker, await response.Content.ReadAsStringAsync());
    }
}
