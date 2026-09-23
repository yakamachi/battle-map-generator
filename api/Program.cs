using System.Security.Cryptography;
using System.Text;
using battle_map_generator_api.Data;
using battle_map_generator_api.Health;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Account store. The connection string is read when the context is built, not at startup:
// the EF migrations bundle builds this host in CI without one (it gets --connection at run time),
// and a running app without one must fail readiness instead of crashing.
// Retry covers the Azure SQL free offer failing the first connection after an auto-pause.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("AppDb");
    if (string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(sql => sql.EnableRetryOnFailure());
    }
    else
    {
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
    }
});

// Identity services only; login endpoints come with S-04.
builder.Services.AddIdentityCore<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();

// Keys live in the database so restarts and idle unloads keep the same key ring.
// A fixed application name keeps purpose isolation independent of the content root path.
builder.Services.AddDataProtection()
    .SetApplicationName("battle-map-generator")
    .PersistKeysToDbContext<AppDbContext>();

// Data Protection preloads the key ring in a hosted service before the app listens, which reads
// the database on every cold start and wakes a paused Azure SQL database. Load keys on first use instead.
// Single() fails startup loudly if a framework update renames the internal service.
builder.Services.Remove(builder.Services.Single(d =>
    d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "DataProtectionHostedService"));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"])
    .AddCheck<DataProtectionHealthCheck>("data-protection", tags: ["ready"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// The SPA from web/build/client is copied into wwwroot at build time.
// index.html is never cached so a deploy is picked up on refresh; hashed assets are immutable.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var headers = ctx.Context.Response.Headers;
        if (ctx.Context.Request.Path.StartsWithSegments("/assets"))
        {
            headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        }
        else
        {
            headers[HeaderNames.CacheControl] = "no-cache";
        }
    }
});

// Liveness runs no checks: 200 while the process serves requests.
app.MapHealthChecks("/api/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness hits the database, so it is gated by a shared key and fails closed (404, no checks run).
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .Add(endpoint =>
    {
        var next = endpoint.RequestDelegate!;
        endpoint.RequestDelegate = ctx =>
        {
            if (!HasValidHealthKey(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }
            return next(ctx);
        };
    });

// Unknown API routes are 404s, never the SPA shell.
app.MapFallback("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache"
});

app.Run();

static bool HasValidHealthKey(HttpContext ctx)
{
    var expected = ctx.RequestServices.GetRequiredService<IConfiguration>()["Health:ReadyKey"];
    if (string.IsNullOrEmpty(expected))
    {
        return false;
    }

    // Hashing first gives equal-length inputs, so the comparison time does not reveal the key length.
    var provided = ctx.Request.Headers["X-Health-Key"].ToString();
    return CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
        SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
