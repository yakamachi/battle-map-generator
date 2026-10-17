namespace BattleMapGenerator.Api.Tests.Infrastructure;

// Tags every outgoing request with a fixed X-Forwarded-For address, the way App Service's front end
// reports the client IP in production. ApiFactory turns on the framework's forwarded-headers handling,
// so the app sees this address as HttpContext.Connection.RemoteIpAddress.
public sealed class ForwardedForHandler(string simulatedIp) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Forwarded-For");
        request.Headers.Add("X-Forwarded-For", simulatedIp);
        return base.SendAsync(request, cancellationToken);
    }
}
