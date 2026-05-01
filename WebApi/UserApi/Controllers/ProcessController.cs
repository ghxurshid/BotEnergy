using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserApi.Extensions;
using UserApi.Models.Requests;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Sessiya ichidagi mahsulot berish jarayonlarini boshqaruvi.
    /// Bir sessiyada bir vaqtda faqat bitta aktiv jarayon bo'lishi mumkin —
    /// avvalgisi tugagandan keyin yangisi boshlanadi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ProcessController : ControllerBase
    {
        private readonly IProcessService _processService;

        public ProcessController(IProcessService processService)
        {
            _processService = processService;
        }

        /// <summary>
        /// Yangi mahsulot berish jarayonini boshlash.
        /// Sessiya `Connected` yoki `InProcess` holatida bo'lishi shart, balans tekshiriladi,
        /// qurilmaga RabbitMQ → DeviceApi → MQTT orqali start buyrug'i yuboriladi.
        /// </summary>
        /// <response code="200">Jarayon boshlandi</response>
        /// <response code="400">Sessiya yoki mahsulot mos kelmaydi, balans yetarli emas</response>
        /// <response code="409">Sessiyada hali tugamagan jarayon bor yoki qurilma boshqa user tomonidan band</response>
        [HttpPost]
        [HttpPost("/processes/start")]
        [RequirePermission(Permissions.ProcessStart)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Start([FromBody] StartProcessRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _processService.StartAsync(request.ToDto(userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Foydalanuvchi tomonidan jarayonni to'xtatish.
        /// Qurilmaga MQTT stop yuboriladi, balansdan to'langan miqdor uchun pul yechiladi.
        /// </summary>
        [HttpPost("{processId:long}")]
        [RequirePermission(Permissions.ProcessStop)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Stop(long processId, [FromBody] ProcessControlRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _processService.StopByUserAsync(RequestToDtoExtensions.ToControlDto(processId, userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        [HttpPost("/processes/{processId:long}/stop")]
        [RequirePermission(Permissions.ProcessStop)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StopById(long processId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _processService.StopByUserAsync(RequestToDtoExtensions.ToControlDto(processId, userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Jarayonni pauza qilish.
        /// </summary>
        [HttpPost("{processId:long}")]
        [RequirePermission(Permissions.ProcessPause)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Pause(long processId, [FromBody] ProcessControlRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _processService.PauseAsync(RequestToDtoExtensions.ToControlDto(processId, userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Pauza qilingan jarayonni davom ettirish.
        /// </summary>
        [HttpPost("{processId:long}")]
        [RequirePermission(Permissions.ProcessResume)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Resume(long processId, [FromBody] ProcessControlRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _processService.ResumeAsync(RequestToDtoExtensions.ToControlDto(processId, userId));
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
