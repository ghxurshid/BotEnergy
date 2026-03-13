using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceAdminController : ControllerBase
    {
        [HttpPost("register")]
        public ActionResult<RegisterDeviceResponse> Register([FromBody] RegisterDeviceRequest request)
        {
            return Ok(new RegisterDeviceResponse { Created = true });
        }

        [HttpPost("change-status")]
        public ActionResult<ChangeDeviceStatusResponse> ChangeStatus([FromBody] ChangeDeviceStatusRequest request)
        {
            return Ok(new ChangeDeviceStatusResponse { Updated = true });
        }
    }
}
