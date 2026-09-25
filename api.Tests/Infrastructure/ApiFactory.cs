using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace battle_map_generator_api.Tests.Infrastructure;

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
