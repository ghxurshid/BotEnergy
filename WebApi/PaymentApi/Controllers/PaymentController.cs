using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaymentApi.Models.Requests;
using PaymentApi.Models.Responses;

namespace PaymentApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        [HttpPost("create")]
        public ActionResult<CreatePaymentResponse> Create([FromBody] CreatePaymentRequest request)
        {
            return Ok(new CreatePaymentResponse { QrCode = "generated-payment-qr" });
        }

        [HttpPost("verify")]
        public ActionResult<VerifyPaymentResponse> Verify([FromBody] VerifyPaymentRequest request)
        {
            return Ok(new VerifyPaymentResponse { Paid = true });
        }
    }
}
