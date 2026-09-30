using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BattleMapGenerator.Api.Tests.Infrastructure;

// Hosts the real app in memory against a given database.
// The environment is "Testing", not Development: appsettings.Development.json carries the developer's
// local container connection string and ready key, and tests must not depend on either.
// Every setting the app reads is supplied here, added last so it also wins over environment variables.
// A null connection string models a missing ConnectionStrings:AppDb (the null in-memory value wins).
public sealed class ApiFactory(string? connectionString, string? readyKey = ApiFactory.ReadyKey)
    : WebApplicationFactory<Program>
{
    public const string ReadyKey = "test-ready-key";
    public const string ReadyKeyHeader = "X-Health-Key";
    public const string SpaShellMarker = "spa-shell-stub";
    public const string TestPassword = "test-password";

    // Nothing listens on port 1, so the connection is refused at once. The short Connect Timeout
    // keeps the suite fast even if the refusal turns into a timeout on some network setups.
    public const string UnreachableConnectionString =
        "Server=127.0.0.1,1;Database=battlemap;User Id=sa;Password=unused;Connect Timeout=2;Encrypt=False";

    // The real wwwroot is empty in CI (the SPA is copied in after the tests run), and then the SPA
    // fallback answers 404 too, which would hide a missing /api guard. A stub index.html makes the
    // fallback answer 200, so an /api path that ever reaches it fails the tests.
    private readonly string _webRoot = CreateStubWebRoot();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseWebRoot(_webRoot);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppDb"] = connectionString,
            ["Health:ReadyKey"] = readyKey,
        }));
    }

    public HttpClient CreateReadyClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ReadyKeyHeader, ReadyKey);
        return client;
    }

    // Registers an account (a unique one unless an email is given) and returns the client that keeps
    // its session cookie. Needs a migrated database and spends one permit of this factory's auth limit.
    public async Task<HttpClient> CreateLoggedInClientAsync(string? email = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = email ?? NewEmail(), password = TestPassword });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public static string NewEmail() => $"dm-{Guid.NewGuid():N}@example.com";

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }

    private static string CreateStubWebRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bmg-webroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "index.html"), $"<!doctype html><title>{SpaShellMarker}</title>");
        return path;
    }
}
