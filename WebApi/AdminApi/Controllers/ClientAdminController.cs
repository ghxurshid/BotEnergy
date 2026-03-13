using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientAdminController : ControllerBase
    {
        [HttpPost("register")]
        public ActionResult<RegisterClientResponse> Register([FromBody] RegisterClientRequest request)
        {
            return Ok(new RegisterClientResponse { Created = true });
        }
    }
}
