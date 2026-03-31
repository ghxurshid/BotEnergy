using CommonConfiguration.Attributes;
using Domain.Dtos.Session;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserApi.Controllers
{
    // HTTP fallback — MQTT ishlamagan holatlarda device to'g'ridan-to'g'ri shu endpointlarga murojaat qiladi.
    [Route("api/[controller]/[action]")]
    [ApiController]
    [AllowAnonymous]
    [SkipPermissionCheck]
    public class DeviceSessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;

        public DeviceSessionController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [HttpPost]
        public async Task<IActionResult> Connect([FromBody] DeviceConnectBody request)
        {
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

        [HttpPost]
        public async Task<IActionResult> ReportProgress([FromBody] SessionProgressBody request)
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
        public async Task<IActionResult> Finish([FromBody] DeviceFinishBody request)
        {
            var result = await _sessionService.DeviceFinishAsync(new DeviceFinishDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                FinalQuantity = request.FinalQuantity
            });

            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { total_delivered = result.Result!.TotalDelivered, message = result.Result.ResultMessage });
        }
    }

    public class DeviceConnectBody
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
    }

    public class SessionProgressBody
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class DeviceFinishBody
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public decimal FinalQuantity { get; set; }
    }
}
