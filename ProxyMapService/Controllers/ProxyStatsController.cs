using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProxyMapService.Interfaces;
using ProxyMapService.Models;
using ProxyMapService.Responses;
using System.Linq;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class ProxyStatsController(IProxyService service) : ControllerBase
    {
        [HttpGet(Name = "getStats")]
        public ActionResult<StatsResponse> GetStats()
        {
            List<HostStatsResponse>? responseHosts = null;
            var hostStats = service.GetHostStats();
            if (hostStats != null)
            {
                responseHosts = new();
                foreach (var entry in hostStats)
                {
                    HostStats hs = entry.Value;
                    responseHosts.Add(new HostStatsResponse() { 
                        hostName = entry.Key,
                        requestsCount = hs.Count,
                        bytesRead = hs.BytesRead,
                        bytesSent = hs.BytesSent
                    });
                }
            }
            return Ok(new StatsResponse
            {
                serviceInfo = service.GetServiceInfo(),
                sessionsCount = service.GetSessionsCount(),
                authenticationNotRequired = service.GetAuthenticationNotRequired(),
                authenticationRequired = service.GetAuthenticationRequired(),
                authenticated = service.GetAuthenticated(),
                authenticationInvalid = service.GetAuthenticationInvalid(),
                httpRejected = service.GetHttpRejected(),
                connected = service.GetConnected(),
                connectionFailed = service.GetConnectionFailed(),
                headerFailed = service.GetHeaderFailed(),
                HostFailed = service.GetHostFailed(),
                totalBytesRead = service.GetTotalBytesRead(),
                totalBytesSent = service.GetTotalBytesSent(),
                hosts = responseHosts
            });
        }
    }
}