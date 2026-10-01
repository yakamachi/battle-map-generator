using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace BattleMapGenerator.Api.Auth;

// Authentication decrypts the session cookie with the key ring in the database, and a forged cookie
// with an unknown key id makes the ring reload on every request. This gate runs before
// UseAuthentication:
// - outside the API routes that can need a session (liveness, readiness, the SPA fallback) it removes
//   the session cookie from the request, so it is never read there;
// - on those routes it limits requests carrying the cookie per client IP, far above what a real user
//   sends, so a forger can wake the database at most this often per address.
// UseAuthentication itself stays at the top level: moved into a branch, the framework would add its
// own at the very start of the pipeline, ahead of static files.
public static class SessionCookieGate
{
    public const int RequestsPerMinute = 60;

    public static readonly string CookieName = CookieAuthenticationDefaults.CookiePrefix + IdentityConstants.ApplicationScheme;

    public static IApplicationBuilder UseSessionCookieGate(this IApplicationBuilder app)
    {
        var limiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        return app.Use(async (context, next) =>
        {
            if (!context.Request.Cookies.ContainsKey(CookieName))
            {
                await next(context);
                return;
            }

            if (!NeedsSession(context.Request.Path))
            {
                RemoveSessionCookie(context.Request);
                await next(context);
                return;
            }

            using var lease = await limiter.AcquireAsync(context, cancellationToken: context.RequestAborted);
            if (!lease.IsAcquired)
            {
                var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                return;
            }

            await next(context);
        });
    }

    private static bool NeedsSession(PathString path) =>
        path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/health");

    // The cookie may be split into chunks (CookieName, then CookieNameC1, C2, ...); drop them all.
    private static void RemoveSessionCookie(HttpRequest request)
    {
        var kept = request.Headers.Cookie
            .SelectMany(header => (header ?? string.Empty).Split(';'))
            .Select(pair => pair.Trim())
            .Where(pair => pair.Length > 0 && !IsSessionCookie(pair));
        request.Headers.Cookie = string.Join("; ", kept);
    }

    private static bool IsSessionCookie(string pair)
    {
        var name = pair.Split('=', 2)[0].Trim();
        if (name == CookieName)
        {
            return true;
        }
        return name.Length > CookieName.Length + 1
            && name.StartsWith(CookieName + "C", StringComparison.Ordinal)
            && name[(CookieName.Length + 1)..].All(char.IsAsciiDigit);
    }
}
