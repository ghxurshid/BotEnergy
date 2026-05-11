using System.Globalization;
using Domain.Dtos.Base;
using Domain.Dtos.Report;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Foydalanish hisoboti — paginatsiya va Excel eksport.
    /// Validatsiya, scope tekshiruvi va ustun konfiguratsiyasi shu yerda.
    /// </summary>
    public sealed class UsageReportService : IUsageReportService
    {
        /// <summary>Maksimal davr — bir martalik so'rovda. 1 yil etarli, undan kattasi tizimni og'irlashtiradi.</summary>
        private static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(366);

        private readonly IUsageReportRepository _reportRepo;
        private readonly IExcelReportExporter _excelExporter;

        public UsageReportService(
            IUsageReportRepository reportRepo,
            IExcelReportExporter excelExporter)
        {
            _reportRepo = reportRepo;
            _excelExporter = excelExporter;
        }

        public async Task<GenericDto<PagedResult<UsageReportRowDto>>> GetPagedAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var validation = ValidatePeriod(filter);
            if (validation is not null)
                return GenericDto<PagedResult<UsageReportRowDto>>.Error(validation.Code, validation.ErrorMessage);

            var result = await _reportRepo.GetPagedAsync(scope, filter, cancellationToken);
            return GenericDto<PagedResult<UsageReportRowDto>>.Success(result);
        }

        public async Task ExportToExcelAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            Stream output,
            CancellationToken cancellationToken = default)
        {
            var validation = ValidatePeriod(filter);
            if (validation is not null)
                throw new ArgumentException(validation.ErrorMessage);

            var rows = _reportRepo.StreamAllAsync(scope, filter, cancellationToken);

            await _excelExporter.ExportAsync(
                sheetName: "Usage Report",
                columns: BuildColumns(scope),
                rows: rows,
                output: output,
                cancellationToken: cancellationToken);
        }

        public string BuildExportFileName(UsageReportScope scope, UsageReportFilterDto filter)
        {
            var prefix = scope switch
            {
                UsageReportScope.User u => $"user-{u.UserId}",
                UsageReportScope.Organization o => $"organization-{o.OrganizationId}",
                UsageReportScope.Merchant { StationId: null } m => $"merchant-{m.MerchantId}",
                UsageReportScope.Merchant m => $"merchant-{m.MerchantId}-station-{m.StationId}",
                _ => "report"
            };

            var from = filter.From.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var to = filter.To.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            return $"usage-report-{prefix}-{from}-{to}.xlsx";
        }

        // ── Internal ─────────────────────────────────────────────────

        private static Error? ValidatePeriod(UsageReportFilterDto filter)
        {
            if (filter.From == default || filter.To == default)
                return new Error { Code = 400, ErrorMessage = "Davr (from/to) ko'rsatilishi shart." };

            if (filter.To <= filter.From)
                return new Error { Code = 400, ErrorMessage = "to sanasi from sanasidan keyin bo'lishi kerak." };

            if (filter.To - filter.From > MaxPeriod)
                return new Error { Code = 400, ErrorMessage = $"Davr {MaxPeriod.Days} kundan oshmasligi kerak." };

            return null;
        }

        /// <summary>
        /// Ustunlar konfiguratsiyasi — scope ga qarab UserPhone ustunini qo'shish.
        /// </summary>
        private static IReadOnlyList<ExcelColumn<UsageReportRowDto>> BuildColumns(UsageReportScope scope)
        {
            var includeUser = scope is not UsageReportScope.User;

            var columns = new List<ExcelColumn<UsageReportRowDto>>
            {
                new() { Header = "#", ValueSelector = r => r.ProcessId, Width = 8 },
                new() { Header = "Boshlangan vaqti", ValueSelector = r => r.StartedAt,
                        NumberFormat = "yyyy-MM-dd HH:mm:ss", Width = 20 },
                new() { Header = "Tugagan vaqti", ValueSelector = r => r.EndedAt,
                        NumberFormat = "yyyy-MM-dd HH:mm:ss", Width = 20 },
                new() { Header = "Mahsulot", ValueSelector = r => r.ProductName, Width = 24 },
                new() { Header = "Birlik", ValueSelector = r => r.Unit, Width = 10 },
                new() { Header = "Berilgan miqdor", ValueSelector = r => r.GivenAmount,
                        NumberFormat = "#,##0.0000", Width = 16 },
                new() { Header = "So'ralgan miqdor", ValueSelector = r => r.RequestedAmount,
                        NumberFormat = "#,##0.0000", Width = 16 },
                new() { Header = "Birlik narxi", ValueSelector = r => r.PricePerUnit,
                        NumberFormat = "#,##0.00", Width = 14 },
                new() { Header = "Jami summa", ValueSelector = r => r.TotalCost,
                        NumberFormat = "#,##0.00", Width = 16 },
                new() { Header = "Status", ValueSelector = r => r.Status, Width = 12 },
                new() { Header = "Tugash sababi", ValueSelector = r => r.EndReason, Width = 14 },
                new() { Header = "Stansiya", ValueSelector = r => r.StationName, Width = 24 },
                new() { Header = "Qurilma seriyasi", ValueSelector = r => r.DeviceSerial, Width = 20 },
            };

            if (includeUser)
                columns.Add(new() { Header = "Foydalanuvchi (telefon)", ValueSelector = r => r.UserPhone, Width = 18 });

            return columns;
        }
    }
}
