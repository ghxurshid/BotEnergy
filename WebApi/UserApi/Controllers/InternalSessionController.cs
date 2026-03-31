using CommonConfiguration.Attributes;
using Domain.Dtos.Session;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Models.Requests;

namespace UserApi.Controllers
{
    // DeviceApi tomonidan chaqiriladigan ichki endpoint.
    // JWT autentifikatsiya yo'q — uning o'rniga X-Internal-Secret header tekshiriladi.
    [Route("api/internal/session")]
    [ApiController]
    [AllowAnonymous]
    [SkipPermissionCheck]
    public class InternalSessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly IConfiguration _configuration;

        public InternalSessionController(ISessionService sessionService, IConfiguration configuration)
        {
            _sessionService = sessionService;
            _configuration = configuration;
        }

        private bool IsAuthorized()
        {
            var expected = _configuration["InternalApi:SharedSecret"];
            var received = Request.Headers["X-Internal-Secret"].FirstOrDefault();
            return !string.IsNullOrEmpty(expected) && expected == received;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] InternalDeviceConnectRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { message = "Ichki secret noto'g'ri." });

            var result = await _sessionService.DeviceConnectAsync(new DeviceConnectedDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                ProductType = request.ProductType
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { session_id = result.Result!.SessionId, message = result.Result.ResultMessage });
        }

        [HttpPost("progress")]
        public async Task<IActionResult> Progress([FromBody] InternalDeviceProgressRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { message = "Ichki secret noto'g'ri." });

            var result = await _sessionService.ReportProgressAsync(new SessionProgressDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                Quantity = request.Quantity
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { message = result.Result!.ResultMessage });
        }

        [HttpPost("finish")]
        public async Task<IActionResult> Finish([FromBody] InternalDeviceFinishRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { message = "Ichki secret noto'g'ri." });

            var result = await _sessionService.DeviceFinishAsync(new DeviceFinishDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                FinalQuantity = request.FinalQuantity
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new
            {
                total_delivered = result.Result!.TotalDelivered,
                message = result.Result.ResultMessage
            });
        }
    }
}
