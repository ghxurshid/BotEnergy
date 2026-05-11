using CommonConfiguration.Attributes;
using Domain.Dtos.Base;
using Domain.Dtos.Report;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = Domain.Constants.Permissions;

namespace AdminApi.Controllers
{
    /// <summary>
    /// Merchant uchun savdo hisoboti — uning stansiyalarida ko'rsatilgan xizmatlar.
    /// </summary>
    /// <remarks>
    /// **Endpointlar:**
    ///  - <c>GET /api/MerchantReport/Sales?merchantId=&amp;stationId=&amp;from=&amp;to=</c>
    ///    — <c>stationId</c> bo'sh bo'lsa: merchantning BARCHA stansiyalari.
    ///    — <c>stationId</c> ko'rsatilsa: faqat o'sha stansiya.
    ///  - <c>GET /api/MerchantReport/SalesExport?...</c> — Excel.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class MerchantReportController : ControllerBase
    {
        private const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IUsageReportService _service;

        public MerchantReportController(IUsageReportService service)
            => _service = service;

        /// <summary>
        /// Merchant savdo hisobotini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// **Permission:** `MerchantReport.Sales`
        ///
        /// **Query parametrlari:**
        ///
        /// | Maydon       | Turi     | Majburiy | Tavsif                                                  |
        /// |--------------|----------|----------|---------------------------------------------------------|
        /// | merchantId   | long     | **Ha**   | Merchant ID si.                                         |
        /// | stationId    | long?    | Yo'q     | Bitta stansiya bo'yicha filtrlash uchun. Bo'sh = barchasi. |
        /// | from / to    | datetime | **Ha**   | Davr.                                                   |
        /// | pageNumber   | int      | Yo'q     | Default 1.                                              |
        /// | pageSize     | int      | Yo'q     | Default 20, max 100.                                    |
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.MerchantReportSales)]
        [ProducesResponseType(typeof(PagedResult<UsageReportRowDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Sales(
            [FromQuery] long merchantId,
            [FromQuery] long? stationId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var scope = new UsageReportScope.Merchant(merchantId, stationId);
            var filter = BuildFilter(from, to, pageNumber, pageSize);

            var result = await _service.GetPagedAsync(scope, filter, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Merchant savdo hisobotini Excel sifatida yuklab olish.
        /// </summary>
        /// <remarks>
        /// **Permission:** `MerchantReport.SalesExport`
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.MerchantReportSalesExport)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SalesExport(
            [FromQuery] long merchantId,
            [FromQuery] long? stationId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken cancellationToken = default)
        {
            var scope = new UsageReportScope.Merchant(merchantId, stationId);
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
    }
}
