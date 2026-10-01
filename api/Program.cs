using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using BattleMapGenerator.Api.Auth;
using BattleMapGenerator.Api.Data;
using BattleMapGenerator.Api.Health;
using BattleMapGenerator.Api.Maps;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Cell kinds travel as camelCase strings ("floor", "door"), which also makes them a string enum in OpenAPI.
builder.Services.ConfigureHttpJsonOptions(options => MapJson.Configure(options.SerializerOptions));

// A stuck client could burn the F1 plan's daily CPU quota. Generation requires a session, so each
// account gets 10 generations per minute wherever it connects from, with no queue, and accounts
// behind one IP do not share a budget. Authorization runs first, so the "anonymous" bucket is
// unreachable; it is there because a null partition key throws.
// The per-IP auth policy uses the "unknown" bucket for the same reason: TestServer leaves the remote
// address null. Behind App Service the client IP comes from X-Forwarded-For (ASPNETCORE_FORWARDEDHEADERS_ENABLED).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
        context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        return ValueTask.CompletedTask;
    };
    options.AddPolicy(MapEndpoints.GenerateRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    // Register and login are public and hash a password: 10 calls per minute per client IP across both.
    options.AddPolicy(AuthEndpoints.AuthRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

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

// Email and password accounts. The email is the user name, so it must be unique.
// Length is the only password rule; lockout slows guessing one account's password.
builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

// All Identity cookie schemes, not only the application one: SignOutAsync also signs out
// the external and two-factor schemes.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

// The session cookie is protected by the key ring in the database, so it survives a restart.
// Secure follows the request scheme: App Service terminates TLS, local development is plain HTTP.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;

    // There is no server-side login page to send anyone to. The framework answers API endpoints
    // with 401/403 but still adds a Location header pointing at /Account/Login; send the bare status.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

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
// Served before authentication: a file needs no session, and reading a cookie loads the key ring
// from the database.
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

// Reading a session cookie decrypts it with the key ring in the database, and a forged cookie makes
// the ring reload on every read. The gate strips the cookie where no session is needed and limits
// the requests that carry one per client IP before authentication reads it (see SessionCookieGate).
app.UseSessionCookieGate();
app.UseAuthentication();
// Authentication and authorization run before the limiter: it must be able to see the user, and a
// request refused with 401 must not spend a permit.
app.UseAuthorization();
app.UseRateLimiter();

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

app.MapMapEndpoints();
app.MapAuthEndpoints();

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
