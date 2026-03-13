using BillingApi.Models.Requests;
using BillingApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BillingApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BalanceController : ControllerBase
    {
        [HttpPost("get")]
        public ActionResult<GetBalanceResponse> GetBalance([FromBody] GetBalanceRequest request)
        {
            return Ok(new GetBalanceResponse { Balance = 1000 });
        }

        [HttpPost("add")]
        public ActionResult<AddBalanceResponse> AddBalance([FromBody] AddBalanceRequest request)
        {
            return Ok(new AddBalanceResponse { Balance = request.Amount + 1000 });
        }
    }
}
