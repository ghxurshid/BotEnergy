using Domain.Dtos.Session;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace UserApi.Controllers
{
    // NOTE: This controller lives in UserApi so it shares the SignalR Hub instance.
    // Device firmware should call these endpoints via HTTP and connect to /hubs/session for real-time events.
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
        public async Task<IActionResult> Connect([FromBody] DeviceConnectBody request)
        {
            var dto = new DeviceConnectedDto
            {
                SessionToken = request.SessionToken,
                DeviceId = request.DeviceId,
                ProductType = request.ProductType
            };

            var result = await _sessionService.DeviceConnectAsync(dto);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { session_id = result.Result!.SessionId, message = result.Result.ResultMessage });
        }

        [HttpPost]
        public async Task<IActionResult> ReportProgress([FromBody] SessionProgressBody request)
        {
            var dto = new SessionProgressDto
            {
                SessionToken = request.SessionToken,
                DeviceId = request.DeviceId,
                Quantity = request.Quantity,
                TotalQuantity = request.TotalQuantity
            };

            var result = await _sessionService.ReportProgressAsync(dto);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { message = result.Result!.ResultMessage });
        }

        [HttpPost]
        public async Task<IActionResult> Finish([FromBody] DeviceFinishBody request)
        {
            var dto = new DeviceFinishDto
            {
                SessionToken = request.SessionToken,
                DeviceId = request.DeviceId,
                FinalQuantity = request.FinalQuantity
            };

            var result = await _sessionService.DeviceFinishAsync(dto);
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(new { total_delivered = result.Result!.TotalDelivered, message = result.Result.ResultMessage });
        }
    }

    public class DeviceConnectBody
    {
        public string SessionToken { get; set; } = string.Empty;
        public long DeviceId { get; set; }
        public string ProductType { get; set; } = string.Empty;
    }

    public class SessionProgressBody
    {
        public string SessionToken { get; set; } = string.Empty;
        public long DeviceId { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalQuantity { get; set; }
    }

    public class DeviceFinishBody
    {
        public string SessionToken { get; set; } = string.Empty;
        public long DeviceId { get; set; }
        public decimal FinalQuantity { get; set; }
    }
}
