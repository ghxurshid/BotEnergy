using DeviceApi.Models.Requests;
using DeviceApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceController : ControllerBase
    {
        [HttpPost("info")]
        public ActionResult<DeviceInfoResponse> Info([FromBody] DeviceInfoRequest request)
        {
            return Ok(new DeviceInfoResponse { IsAllowed = true });
        }
    }
}
