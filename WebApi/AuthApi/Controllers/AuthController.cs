using AuthApi.Models.Requests;
using AuthApi.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpPost("register")]
        public ActionResult<RegisterUserResponse> Register([FromBody] RegisterUserRequest request)
        {
            // Implement registration logic here
            return Ok(new RegisterUserResponse { Success = true, UserId = "generated-user-id" });
        }

        [HttpPost("verify-user")]
        public ActionResult<RegisterUserResponse> VerifyUser([FromBody] RegisterUserRequest request)
        {
            return Ok(new RegisterUserResponse { Success = true, UserId = "verified-user-id" });
        }

        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            return Ok(new LoginResponse { AccessToken = "jwt-token" });
        }

        [HttpPost("send-otp")]
        public ActionResult<SendOtpResponse> SendOtp([FromBody] SendOtpRequest request)
        {
            return Ok(new SendOtpResponse { Sent = true });
        }

        [HttpPost("verify-otp")]
        public ActionResult<VerifyOtpResponse> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            return Ok(new VerifyOtpResponse { Verified = true });
        }
    }
}
