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
    /// Yuridik tashkilot (kompaniya admini) uchun iste'mol hisoboti.
    /// Tashkilot xodimlari (LegalUser) tomonidan qilingan barcha jarayonlarni ko'rsatadi.
    /// </summary>
    /// <remarks>
    /// **Endpointlar:**
    ///  - <c>GET /api/OrganizationReport/Usage?organizationId=&amp;from=&amp;to=</c> — paginatsiya bilan.
    ///  - <c>GET /api/OrganizationReport/UsageExport?organizationId=&amp;from=&amp;to=</c> — Excel.
    /// </remarks>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class OrganizationReportController : ControllerBase
    {
        private const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IUsageReportService _service;

        public OrganizationReportController(IUsageReportService service)
            => _service = service;

        /// <summary>
        /// Tashkilotning iste'mol hisobotini sahifalab olish.
        /// </summary>
        /// <remarks>
        /// **Permission:** `OrganizationReport.Usage`
        /// </remarks>
        /// <response code="200">Sahifalangan natija.</response>
        /// <response code="400">Davr noto'g'ri.</response>
        [HttpGet]
        [RequirePermission(Permissions.OrganizationReportUsage)]
        [ProducesResponseType(typeof(PagedResult<UsageReportRowDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Usage(
            [FromQuery] long organizationId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var scope = new UsageReportScope.Organization(organizationId);
            var filter = BuildFilter(from, to, pageNumber, pageSize);

            var result = await _service.GetPagedAsync(scope, filter, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Result)
                : StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });
        }

        /// <summary>
        /// Tashkilotning iste'mol hisobotini Excel sifatida yuklab olish.
        /// </summary>
        /// <remarks>
        /// **Permission:** `OrganizationReport.UsageExport`
        /// </remarks>
        [HttpGet]
        [RequirePermission(Permissions.OrganizationReportUsageExport)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UsageExport(
            [FromQuery] long organizationId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken cancellationToken = default)
        {
            var scope = new UsageReportScope.Organization(organizationId);
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
