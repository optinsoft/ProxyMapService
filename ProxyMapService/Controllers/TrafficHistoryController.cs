using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Requests;
using ProxyMapService.Responses;
using ProxyMapService.WebLogging;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class TrafficHistoryController(IHttpTrafficStorage httpTrafficStorage, IHttpLoggingSwitch loggingSwitch) : ControllerBase
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

        [HttpGet("status")]
        public ActionResult<CaptureStatusResponse> GetStatus()
        {
            return Ok(new CaptureStatusResponse
            {
                capture = loggingSwitch.IsHttpCapture
            });
        }

        [HttpPost("toggle")]
        public ActionResult<SuccessResponse> ToggleLogging([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ToggleCaptureRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Request body is required."
                });
            }
            loggingSwitch.IsHttpCapture = request.capture;
            return Ok(new SuccessResponse
            {
                success = true,
                message = $"Event logging status changed to: {loggingSwitch.IsHttpCapture}"
            });
        }
    }
}
