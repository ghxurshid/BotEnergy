using Domain.Dtos.Base;
using Domain.Dtos.Report;

namespace Domain.Interfaces
{
    /// <summary>
    /// Foydalanish hisoboti — paginatsiya va Excel eksport.
    /// </summary>
    public interface IUsageReportService
    {
        /// <summary>
        /// Sahifalangan hisobot. DB dan faqat <see cref="PaginationParams.PageSize"/> ta yozuv olinadi.
        /// </summary>
        Task<GenericDto<PagedResult<UsageReportRowDto>>> GetPagedAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// To'liq Excel hisobotini berilgan oqimga yozadi (HTTP response body).
        /// IAsyncEnumerable orqali stream qilinadi — xotiraga butun ro'yxat yuklanmaydi.
        /// </summary>
        Task ExportToExcelAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            Stream output,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Eksport faylining tavsiya etilgan nomi (scope va davr asosida).
        /// </summary>
        string BuildExportFileName(UsageReportScope scope, UsageReportFilterDto filter);
    }
}
