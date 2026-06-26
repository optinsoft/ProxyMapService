using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProxyMapService.Responses;
using ProxyMapService.WebLogging;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class TrafficHistoryController(IHttpTrafficStorage httpTrafficStorage) : ControllerBase
    {
        [HttpGet("recent")]
        public ActionResult<HttpTrafficHistoryDto> GetRecent()
        {
            var history = httpTrafficStorage.GetRecentEntries();
            return Ok(history);
        }

        [HttpPost("clear")]
        public ActionResult<SuccessResponse> Clear()
        {
            httpTrafficStorage.Clear();
            return Ok(new SuccessResponse
            {
                success = true,
                message = "The http traffic history has been cleared."
            });
        }
    }
}
