using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace battle_map_generator_api.Health;

// Proves the key ring loads from (or is created in) the database and can round-trip a payload.
// The first call on an empty database writes the default key.
public sealed class DataProtectionHealthCheck(IDataProtectionProvider provider) : IHealthCheck
{
    private const string Purpose = "battle-map-generator.health-check";
    private const string Payload = "health-check";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var protector = provider.CreateProtector(Purpose);
            var roundTrip = protector.Unprotect(protector.Protect(Payload));

            return Task.FromResult(roundTrip == Payload
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Data Protection round trip returned a different payload."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Data Protection round trip failed.", ex));
        }
    }
}
