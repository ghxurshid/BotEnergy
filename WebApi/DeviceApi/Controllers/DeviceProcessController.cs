using DeviceApi.Models.Requests;
using DeviceApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceProcessController : ControllerBase
    {
        [HttpPost("process")]
        public ActionResult<DeviceProcessResponse> Process([FromBody] DeviceProcessRequest request)
        {
            return Ok(new DeviceProcessResponse
            {
                LimitAmount = 1000,
                ProductId = request.ProductId,
                Command = "END"
            });
        }
    }
}
