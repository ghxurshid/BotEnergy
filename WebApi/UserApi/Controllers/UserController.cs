using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserApi.Models.Requests;
using UserApi.Models.Responses;

namespace UserApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpPost("start-product")]
        public ActionResult<StartProductResponse> StartProduct([FromBody] StartProductRequest request)
        {
            return Ok(new StartProductResponse { Started = true });
        }

        [HttpPost("cancel-process")]
        public ActionResult<CancelProcessResponse> CancelProcess([FromBody] CancelProcessRequest request)
        {
            return Ok(new CancelProcessResponse { Cancelled = true });
        }

        [HttpPost("get-expenses")]
        public ActionResult<GetUserExpensesResponse> GetExpenses([FromBody] GetUserExpensesRequest request)
        {
            return Ok(new GetUserExpensesResponse { Expenses = new List<UserExpenseDto>() });
        }
    }
}
