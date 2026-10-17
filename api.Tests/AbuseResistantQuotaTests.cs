using System.Net;
using System.Net.Http.Json;
using BattleMapGenerator.Api.Auth;
using BattleMapGenerator.Api.Maps;
using BattleMapGenerator.Api.Tests.Infrastructure;

namespace BattleMapGenerator.Api.Tests;

// Each per-account and per-IP limiter is chained with one shared global limiter, so total throughput
// is a fixed number however many accounts or client IPs a caller spreads its traffic over. Every
// caller here stays inside its own budget, so only the shared ceiling can refuse it. The limiters count
// per app instance, so each test builds its own factory; requests are sequential and finish well inside
// one fixed window.
[Collection(SqlServerCollection.Name)]
public sealed class AbuseResistantQuotaTests(SqlServerFixture sql)
{
    private const string RegisterPath = "/api/auth/register";
    private const string GeneratePath = "/api/maps/generate";

    // One more IP than the global ceiling needs to fill it: the last IP has spent none of its own budget.
    [Fact]
    public async Task Registrations_spread_across_client_ips_stop_at_the_global_auth_limit()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);
        var ipCount = AuthEndpoints.GlobalAuthLimit / AuthEndpoints.PerIpAuthLimit + 1;
        var allowed = 0;

        for (var ip = 1; ip <= ipCount; ip++)
        {
            var client = factory.CreateClientFromIp($"203.0.113.{ip}");
            for (var attempt = 1; attempt <= AuthEndpoints.PerIpAuthLimit; attempt++)
            {
                var response = await client.PostAsJsonAsync(RegisterPath,
                    new { email = ApiFactory.NewEmail(), password = ApiFactory.TestPassword });

                if (ip < ipCount)
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    allowed++;
                }
                else
                {
                    // A fresh IP with its whole own budget left is refused by the shared ceiling.
                    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
                    Assert.True(response.Headers.RetryAfter?.Delta is { } delay && delay > TimeSpan.Zero && delay <= TimeSpan.FromMinutes(1),
                        $"Retry-After was '{response.Headers.RetryAfter}'.");
                }
            }
        }

        Assert.Equal(AuthEndpoints.GlobalAuthLimit, allowed);
    }

    // Accounts take turns, so when the ceiling is reached every account still has budget of its own left.
    [Fact]
    public async Task Generate_calls_spread_across_accounts_stop_at_the_global_generate_limit()
    {
        await using var factory = new ApiFactory(sql.ConnectionString);

        // 10 registrations from 2 IPs: well under both the per-IP and the global auth budget.
        var accounts = new List<HttpClient>();
        for (var ip = 1; ip <= 2; ip++)
        {
            for (var i = 1; i <= 5; i++)
            {
                var client = factory.CreateClientFromIp($"198.51.100.{ip}");
                var registered = await client.PostAsJsonAsync(RegisterPath,
                    new { email = ApiFactory.NewEmail(), password = ApiFactory.TestPassword });
                Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
                accounts.Add(client);
            }
        }

        var allowedPerAccount = new int[accounts.Count];
        var rejectedPerAccount = new int[accounts.Count];
        for (var round = 1; round <= MapEndpoints.PerAccountGenerateLimit; round++)
        {
            for (var account = 0; account < accounts.Count; account++)
            {
                var response = await accounts[account].PostAsJsonAsync(GeneratePath, new { seed = round });

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    allowedPerAccount[account]++;
                }
                else
                {
                    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
                    rejectedPerAccount[account]++;
                }
            }
        }

        Assert.Equal(MapEndpoints.GlobalGenerateLimit, allowedPerAccount.Sum());
        Assert.Equal(accounts.Count * MapEndpoints.PerAccountGenerateLimit - MapEndpoints.GlobalGenerateLimit, rejectedPerAccount.Sum());
        // Every account was refused while under its own limit: the refusal came from the shared ceiling.
        Assert.All(allowedPerAccount, allowed => Assert.InRange(allowed, 1, MapEndpoints.PerAccountGenerateLimit - 1));
        Assert.All(rejectedPerAccount, rejected => Assert.True(rejected > 0));
    }
}
