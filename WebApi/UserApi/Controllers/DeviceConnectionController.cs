using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserApi.Models.Requests;
using UserApi.Models.Responses;

namespace UserApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceConnectionController : ControllerBase
    {
        [HttpPost("connect")]
        public ActionResult<ConnectDeviceResponse> Connect([FromBody] ConnectDeviceRequest request)
        {
            return Ok(new ConnectDeviceResponse { QrCode = "generated-qr-code" });
        }

        [HttpPost("disconnect")]
        public ActionResult<DisconnectDeviceResponse> Disconnect([FromBody] DisconnectDeviceRequest request)
        {
            return Ok(new DisconnectDeviceResponse { Disconnected = true });
        }
    }
}
