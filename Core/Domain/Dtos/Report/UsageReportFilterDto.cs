using Domain.Dtos.Base;

namespace Domain.Dtos.Report
{
    /// <summary>
    /// Hisobot uchun davr va paginatsiya filtri.
    /// Davr — ishlab chiqarish jarayonining (<see cref="Domain.Entities.ProductProcessEntity.StartedAt"/>) sanasi bo'yicha.
    /// </summary>
    public class UsageReportFilterDto
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public PaginationParams Pagination { get; set; } = new();
    }
}
