using Domain.Dtos.Session;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
            var result = await _sessionService.DeviceConnectAsync(new DeviceConnectedDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { session_id = result.Result!.SessionId, message = result.Result.ResultMessage });
        }

        [HttpPost]
        public async Task<IActionResult> ReportProgress([FromBody] DeviceProgressRequest request)
        {
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

        [HttpPost]
        public async Task<IActionResult> Finish([FromBody] DeviceFinishRequest request)
        {
            var result = await _sessionService.DeviceFinishAsync(new DeviceFinishDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                FinalQuantity = request.FinalQuantity,
                EndReason = request.EndReason
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

    // ── Request modellari ──────────────────────────────────────────────

    public class DeviceConnectRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class DeviceProgressRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class DeviceFinishRequest
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
        public string? EndReason { get; set; }
    }
}
