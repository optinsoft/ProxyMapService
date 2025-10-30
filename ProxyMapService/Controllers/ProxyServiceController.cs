using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProxyMapService.Exceptions;
using ProxyMapService.Interfaces;
using ProxyMapService.Responses;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]

    public class ProxyServiceController(IProxyService service) : ControllerBase
    {
        [HttpPost("start")]
        public ActionResult<SuccessResponse> StartService()
        {
            try
            {
                service.StartProxyMappingTasks();
            }
            catch (ServiceAlreadyStartedException)
            {
                return Ok(new SuccessResponse
                {
                    success = false,
                    message = "Service already started."
                });
            }
            return Ok(new SuccessResponse
            {
                success = true,
                message = "Service started."
            });
        }

        [HttpPost("stop")]
        public ActionResult<SuccessResponse> StopService()
        {
            try
            {
                service.StopProxyMappingTasks();
            }
            catch (ServiceAlreadyTerminatedException)
            {
                return Ok(new SuccessResponse
                {
                    success = false,
                    message = "Service already terminated."
                });
            }
            return Ok(new SuccessResponse 
            {
                success = true,
                message = "Stop signal sent."
            });
        }
    }
}
