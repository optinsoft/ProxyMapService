using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProxyMapService.Requests;
using ProxyMapService.Responses;
using ProxyMapService.WebLogging;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class EventLogController(ILogStorage logStorage) : ControllerBase
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
                message = "The event log  has been cleared."
            });
        }
    }
}
