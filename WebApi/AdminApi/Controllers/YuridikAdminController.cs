using AdminApi.Models.Requests;
using AdminApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdminApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class YuridikAdminController : ControllerBase
    {
        [HttpPost("create")]
        public ActionResult<CreateYuridikAdminResponse> Create([FromBody] CreateYuridikAdminRequest request)
        {
            return Ok(new CreateYuridikAdminResponse { Created = true });
        }
    }
}
