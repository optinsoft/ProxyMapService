using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ProxyMapService.WebLogging
{
    [Authorize]
    public class LogHub : Hub
    {
    }
}
