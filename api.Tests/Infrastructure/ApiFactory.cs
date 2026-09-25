using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace battle_map_generator_api.Tests.Infrastructure;

// Hosts the real app in memory against a given database.
// The environment is "Testing", not Development: appsettings.Development.json carries the developer's
// local container connection string and ready key, and tests must not depend on either.
// Every setting the app reads is supplied here, added last so it also wins over environment variables.
public sealed class ApiFactory(string connectionString, string? readyKey = ApiFactory.ReadyKey)
    : WebApplicationFactory<Program>
{
    public const string ReadyKey = "test-ready-key";
    public const string ReadyKeyHeader = "X-Health-Key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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
}
