using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProxyMapService.Exceptions;
using ProxyMapService.Interfaces;
using ProxyMapService.Requests;
using ProxyMapService.Responses;

namespace ProxyMapService.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]

    public class ProxyServiceController(IProxyService service) : ControllerBase
    {
        [HttpPost("start")]
        public ActionResult<SuccessResponse> StartService([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] StartRequest? request)
        {
            bool statsReset = false;
            try
            {
                if (request != null)
                {
                    if (request.resetStats ?? false)
                    {
                        service.ResetStats();
                        statsReset = true;
                    }
                }
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
                message = "Service started."  + (statsReset ? " The statistics has been reset." : "")
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
