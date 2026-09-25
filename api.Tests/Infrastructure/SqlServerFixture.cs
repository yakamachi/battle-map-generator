using battle_map_generator_api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace battle_map_generator_api.Tests.Infrastructure;

// One SQL Server container per test run. The shared database is migrated once; tests that count
// Data Protection keys ask for their own database, so other tests writing keys cannot disturb them.
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same image as compose.yaml, so tests run against the SQL Server the app is developed on.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    // The migrated database shared by tests that do not depend on its key count.
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = await CreateMigratedDatabaseAsync("battlemap");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // A connection string to a database that does not exist yet (MigrateAsync creates it).
    public string NewDatabaseConnectionString(string? name = null)
    {
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = name ?? $"test_{Guid.NewGuid():N}",
        };
        return builder.ConnectionString;
    }

    public async Task<string> CreateMigratedDatabaseAsync(string? name = null)
    {
        var connectionString = NewDatabaseConnectionString(name);
        await using var db = CreateContext(connectionString);
        await db.Database.MigrateAsync();
        return connectionString;
    }

    public static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);

    public static async Task<int> CountKeysAsync(string connectionString)
    {
        await using var db = CreateContext(connectionString);
        return await db.DataProtectionKeys.CountAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
