using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using UsageSessionApi.Extensions;
using UsageSessionApi.Models.Requests;

namespace UsageSessionApi.Controllers
{
    /// <summary>
    /// HTTP fallback — MQTT ishlamagan holatlarda IoT qurilma
    /// to'g'ridan-to'g'ri shu endpointlarga murojaat qiladi.
    /// Autentifikatsiyasiz — qurilma session_token bilan ishlaydi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class DeviceSessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;

        public DeviceSessionController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [HttpPost]
        public async Task<IActionResult> Connect([FromBody] DeviceConnectRequest request)
        {
            var result = await _sessionService.DeviceConnectAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        [HttpPost]
        public async Task<IActionResult> ReportProgress([FromBody] DeviceProgressRequest request)
        {
            var result = await _sessionService.ReportProgressAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        [HttpPost]
        public async Task<IActionResult> Finish([FromBody] DeviceFinishRequest request)
        {
            var result = await _sessionService.DeviceFinishAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }
    }
}
