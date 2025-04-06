using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProxyMapService.Interfaces;
using ProxyMapService.Responses;

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
                sessionsCount = service.GetSessionsCount(),
                authenticationNotRequired = service.GetAuthenticationNotRequired(),
                authenticationRequired = service.GetAuthenticationRequired(),
                authenticated = service.GetAuthenticated(),
                authenticationInvalid = service.GetAuthenticationInvalid(),
                httpRejected = service.GetHttpRejected(),
                connected = service.GetConnected(),
                connectionFailed = service.GetConnectionFailed(),
                headerFailed = service.GetHeaderFailed(),
                totalBytesRead = service.GetTotalBytesRead(),
                totalBytesSent = service.GetTotalBytesSent()
            });
        }
    }
}