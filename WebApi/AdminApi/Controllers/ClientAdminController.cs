using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ClientAdminController : ControllerBase
    {
        [HttpPost]
        public ActionResult<RegisterClientResponse> Register([FromBody] RegisterClientRequest request)
        {
            return Ok(new RegisterClientResponse { Created = true });
        }
    }
}
