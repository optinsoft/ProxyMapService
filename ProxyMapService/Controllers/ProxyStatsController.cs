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
            return Ok(new StatsResponse
            {
                serviceInfo = service.GetServiceInfo(),
                started = service.Started,
                startTime = service.GetStartTime(),
                stopTime = service.GetStopTime(),
                currentTime = service.GetCurrentTime(),
                sessionsCount = service.GetSessionsCount(),
                authenticationNotRequired = service.GetAuthenticationNotRequired(),
                authenticationRequired = service.GetAuthenticationRequired(),
                authenticated = service.GetAuthenticated(),
                authenticationInvalid = service.GetAuthenticationInvalid(),
                httpRejected = service.GetHttpRejected(),
                headerFailed = service.GetHeaderFailed(),
                noHost = service.GetNoHost(),
                hostRejected = service.GetHostRejected(),
                hostProxified = service.GetHostProxified(),
                hostBypassed = service.GetHostBypassed(),
                proxyConnected = service.GetProxyConnected(),
                proxyFailed = service.GetProxyFailed(),
                bypassConnected = service.GetBypassConnected(),
                bypassFailed = service.GetBypassFailed(),
                totalBytesRead = service.GetTotalBytesRead(),
                totalBytesSent = service.GetTotalBytesSent(),
                proxyBytesRead = service.GetProxyBytesRead(),
                proxyBytesSent = service.GetProxyBytesSent(),
                bypassBytesRead = service.GetBypassBytesRead(),
                bypassBytesSent = service.GetBypassBytesSent()
            });
        }

        [HttpGet("Hosts")]
        public ActionResult<HostsResponse> GetHosts()
        {
            List<HostStatsResponse>? responseHosts = null;
            var hostStats = service.GetHostStats();
            if (hostStats != null)
            {
                responseHosts = [];
                foreach (var entry in hostStats)
                {
                    HostStats hs = entry.Value;
                    responseHosts.Add(new HostStatsResponse()
                    {
                        hostName = entry.Key,
                        proxified = hs.Proxified,
                        bypassed = hs.Bypassed,
                        requestsCount = hs.Count,
                        bytesRead = hs.BytesRead,
                        bytesSent = hs.BytesSent
                    });
                }
            }
            return Ok(new HostsResponse
            {
                hosts = responseHosts
            });
        }

        [HttpPost("reset")]
        public ActionResult<SuccessResponse> ResetStats()
        {
            service.ResetStats();
            return Ok(new SuccessResponse()
            {
                success = true,
                message = "The statistics has been reset."
            });
        }
    }
}