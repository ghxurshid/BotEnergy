using CommonConfiguration.Attributes;
using Domain.Dtos.Base;
using Domain.Dtos.Report;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchining shaxsiy iste'mol hisoboti.
    /// Jismoniy va yuridik shaxs xodimlari uchun ham ishlaydi — har ikkalasi ham faqat o'z iste'molini ko'radi.
    /// </summary>
    /// <remarks>
    /// **Endpointlar:**
    ///  - <c>GET /api/Report/MyUsage</c> — paginatsiya bilan (DB dan faqat sahifa hajmida).
    ///  - <c>GET /api/Report/MyUsageExport</c> — to'liq Excel fayl (.xlsx) sifatida.
    ///
    /// **Performance:** Excel eksport <c>IAsyncEnumerable</c> orqali HTTP response body ga stream qilinadi.
    /// Davr 1 yildan oshmasligi kerak.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IUsageReportService _service;

        public ReportController(IUsageReportService service)
            => _service = service;

        /// <summary>
        /// Shaxsiy iste'mol hisobotini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// **Query parametrlari:**
        ///
        /// | Maydon     | Turi     | Majburiy | Default | Tavsif                                                |
        /// |------------|----------|----------|---------|-------------------------------------------------------|
        /// | from       | datetime | **Ha**   | —       | Boshlanish sanasi (ISO 8601).                         |
        /// | to         | datetime | **Ha**   | —       | Tugash sanasi (ISO 8601). from dan keyin bo'lishi kerak. |
        /// | pageNumber | int      | Yo'q     | 1       | Sahifa raqami.                                        |
        /// | pageSize   | int      | Yo'q     | 20      | Bir sahifadagi yozuvlar (max 100).                    |
        ///
        /// **Permission:** `Report.MyUsage`
        /// </remarks>
        /// <response code="200">Sahifalangan natija (items + totalCount + hasNext/hasPrevious).</response>
        /// <response code="400">Davr noto'g'ri yoki 1 yildan ko'p.</response>
        [HttpGet]
        [RequirePermission(Permissions.ReportMyUsage)]
        [ProducesResponseType(typeof(PagedResult<UsageReportRowDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MyUsage(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var filter = BuildFilter(from, to, pageNumber, pageSize);
            var result = await _service.GetPagedAsync(new UsageReportScope.User(userId), filter, cancellationToken);

            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Shaxsiy iste'mol hisobotini Excel (.xlsx) sifatida yuklab olish.
        /// </summary>
        /// <remarks>
        /// Davr ichidagi BARCHA yozuvlar bitta faylda qaytariladi. Pagination yo'q.
        /// Fayl HTTP response ga stream qilib yoziladi (xotirada to'liq buferlanmaydi).
        ///
        /// **Permission:** `Report.MyUsageExport`
        /// </remarks>
        /// <response code="200">.xlsx fayl.</response>
        /// <response code="400">Davr noto'g'ri.</response>
        [HttpGet]
        [RequirePermission(Permissions.ReportMyUsageExport)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MyUsageExport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            return await ExportAsync(new UsageReportScope.User(userId), from, to, cancellationToken);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private async Task<IActionResult> ExportAsync(
            UsageReportScope scope,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            var filter = BuildFilter(from, to, 1, 1);
            var fileName = _service.BuildExportFileName(scope, filter);

            Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
            Response.ContentType = ExcelMimeType;

            try
            {
                await _service.ExportToExcelAsync(scope, filter, Response.Body, cancellationToken);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            return new EmptyResult();
        }

        private static UsageReportFilterDto BuildFilter(DateTime from, DateTime to, int pageNumber, int pageSize)
            => new()
            {
                From = from,
                To = to,
                Pagination = new PaginationParams { PageNumber = pageNumber, PageSize = pageSize }
            };

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }
}
