using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class DeviceAdminController : ControllerBase
    {
        [HttpPost]
        public ActionResult<RegisterDeviceResponse> Register([FromBody] RegisterDeviceRequest request)
        {
            return Ok(new RegisterDeviceResponse { Created = true });
        }

        [HttpPost]
        public ActionResult<ChangeDeviceStatusResponse> ChangeStatus([FromBody] ChangeDeviceStatusRequest request)
        {
            return Ok(new ChangeDeviceStatusResponse { Updated = true });
        }
    }
}
