using DeviceApi.Models.Requests;
using DeviceApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeviceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DevicePaymentController : ControllerBase
    {
        [HttpPost("pay")]
        public ActionResult<DevicePayResponse> Pay([FromBody] DevicePayRequest request)
        {
            return Ok(new DevicePayResponse { PaymentApproved = true });
        }
    }
}
