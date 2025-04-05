using System.Net;

namespace ProxyMapService
{
    public class LocalhostMiddlware
    {
        public class LocalhostMiddleware
        {
            private readonly RequestDelegate _next;
            public LocalhostMiddleware(RequestDelegate next)
            {
                _next = next;

            }
            public async Task InvokeAsync(HttpContext context)
            {
                var remoteIpAddress = context.Connection.RemoteIpAddress;
                if (remoteIpAddress == null || !IPAddress.IsLoopback(remoteIpAddress))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Forbidden.");
                    return;
                }

                await _next(context);
            }
        }
    }
}
