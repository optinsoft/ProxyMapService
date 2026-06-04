using Microsoft.AspNetCore.SignalR;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Utils;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public class HttpTrafficMonitor
    {
        private readonly IHubContext<LogHub> _hubContext;

        public HttpTrafficMonitor(IHubContext<LogHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task LogRequestAsync(HttpRequestHeader requestHeader, string route)
        {
            // Guard check to avoid emitting failures if the header configuration collapsed completely
            if (requestHeader.BadRequest) return;

            var dto = new HttpRequestDto
            {
                Method = requestHeader.HTTPVerb,
                Url = requestHeader.HTTPTarget,
                Route = route,
                Headers = HttpHeaderParser.ParseRawHeaders(requestHeader.Headers)
            };

            await _hubContext.Clients.All.SendAsync("HttpRequest", dto);
        }
    }
}
