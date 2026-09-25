using battle_map_generator_api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace battle_map_generator_api.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class AccountStoreTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrations_apply_to_an_empty_database_and_leave_none_pending()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var db = SqlServerFixture.CreateContext(connectionString);

        await db.Database.MigrateAsync();

        Assert.NotEmpty(db.Database.GetMigrations());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    // Restart simulation: the second instance must read the key ring the first one wrote,
    // not create its own (which would log every user out after a restart).
    [Fact]
    public async Task Payload_protected_by_one_instance_is_unprotected_by_a_fresh_instance()
    {
        const string purpose = "account-store-tests";
        const string payload = "survives a restart";
        var connectionString = await sql.CreateMigratedDatabaseAsync();

        string protectedPayload;
        await using (var first = new ApiFactory(connectionString))
        {
            protectedPayload = first.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(purpose)
                .Protect(payload);
        }

        await using (var second = new ApiFactory(connectionString))
        {
            var unprotected = second.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(purpose)
                .Unprotect(protectedPayload);

            Assert.Equal(payload, unprotected);
        }

        Assert.Equal(1, await SqlServerFixture.CountKeysAsync(connectionString));
    }

    [Fact]
    public async Task User_created_in_one_instance_is_found_by_email_from_another()
    {
        var email = $"dm-{Guid.NewGuid():N}@example.com";

        await using (var first = new ApiFactory(sql.ConnectionString))
        await using (var scope = first.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var result = await users.CreateAsync(new IdentityUser { UserName = email, Email = email }, "Str0ng-Test-Passw0rd!");
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await using (var second = new ApiFactory(sql.ConnectionString))
        await using (var scope = second.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var found = await users.FindByEmailAsync(email);

            Assert.NotNull(found);
            Assert.Equal(email, found.Email);
        }
    }
}
