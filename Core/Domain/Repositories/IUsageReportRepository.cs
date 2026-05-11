using Domain.Dtos.Base;
using Domain.Dtos.Report;

namespace Domain.Repositories
{
    /// <summary>
    /// Hisobot uchun ProductProcess ustidan o'qish abstraksiyasi.
    /// Paged metod faqat sahifa hajmidagi yozuvlarni DB dan oladi (Skip/Take + indekslangan filter).
    /// Stream metod IAsyncEnumerable orqali har satrni alohida tortadi — Excel exportga butun ro'yxatni
    /// xotiraga yuklamasdan yozish imkonini beradi.
    /// </summary>
    public interface IUsageReportRepository
    {
        Task<PagedResult<UsageReportRowDto>> GetPagedAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<UsageReportRowDto> StreamAllAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            CancellationToken cancellationToken = default);
    }
}
