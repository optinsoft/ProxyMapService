using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProxyMapService.Requests;
using ProxyMapService.Responses;
using ProxyMapService.WebLogging;
using System.Text.Json.Serialization;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class EventLogController(ILogStorage logStorage, IEventLoggingSwitch loggingSwitch) : ControllerBase
    {
        [HttpGet("recent")]
        public ActionResult<IEnumerable<LogMessageEntry>> GetRecent()
        {
            var history = logStorage.GetRecentLogs();
            return Ok(history);
        }

        [HttpPost("clear")]
        public ActionResult<SuccessResponse> Clear()
        {
            logStorage.Clear();
            return Ok(new SuccessResponse
            {
                success = true,
                message = "The event log has been cleared."
            });
        }
        
        [HttpGet("status")]
        public ActionResult<CaptureStatusResponse> GetStatus()
        {
            return Ok(new CaptureStatusResponse
            { 
                capture = loggingSwitch.IsEventCapture 
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
            loggingSwitch.IsEventCapture = request.capture;
            return Ok(new SuccessResponse
            {
                success = true,
                message = $"Event logging status changed to: {loggingSwitch.IsEventCapture}"
            });
        }
    }
}
