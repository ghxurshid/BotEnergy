using System.Runtime.CompilerServices;
using Domain.Dtos.Base;
using Domain.Dtos.Report;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    /// <summary>
    /// ProductProcessEntity ustidan hisobot uchun read-only proyeksiya.
    ///
    /// Performance:
    ///  - <see cref="GetPagedAsync"/> — Skip/Take + indekslangan StartedAt + scope predikati.
    ///    Faqat sahifa hajmida yozuv DB dan tortiladi (5000 dan 1-20 olish — 20 ta yozuv).
    ///  - <see cref="StreamAllAsync"/> — IAsyncEnumerable orqali har satrni alohida tortadi.
    ///    Excel eksport chog'ida butun ro'yxat xotiraga yuklanmaydi.
    ///  - Barcha querylarda AsNoTracking — entity tracking ortiqcha, faqat o'qish.
    ///  - Snapshot maydonlar (ProductName, PricePerUnit, Unit) DB ning o'zidan keladi —
    ///    Product entity bilan join kerak emas.
    /// </summary>
    public class UsageReportRepository : IUsageReportRepository
    {
        private readonly AppDbContext _context;

        public UsageReportRepository(AppDbContext context)
            => _context = context;

        public async Task<PagedResult<UsageReportRowDto>> GetPagedAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var query = BuildQuery(scope, filter);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await Project(query)
                .OrderByDescending(r => r.StartedAt)
                .Skip((filter.Pagination.PageNumber - 1) * filter.Pagination.PageSize)
                .Take(filter.Pagination.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<UsageReportRowDto>
            {
                Items = items,
                PageNumber = filter.Pagination.PageNumber,
                PageSize = filter.Pagination.PageSize,
                TotalCount = totalCount
            };
        }

        public async IAsyncEnumerable<UsageReportRowDto> StreamAllAsync(
            UsageReportScope scope,
            UsageReportFilterDto filter,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stream = Project(BuildQuery(scope, filter))
                .OrderByDescending(r => r.StartedAt)
                .AsAsyncEnumerable();

            await foreach (var row in stream.WithCancellation(cancellationToken))
                yield return row;
        }

        // ── Helpers ──────────────────────────────────────────────────

        private IQueryable<ProductProcessEntity> BuildQuery(UsageReportScope scope, UsageReportFilterDto filter)
        {
            var from = filter.From;
            var to = filter.To;

            var query = _context.ProductProcesses
                .AsNoTracking()
                .Where(p => p.StartedAt >= from && p.StartedAt < to);

            return scope switch
            {
                UsageReportScope.User u =>
                    query.Where(p => p.Session!.UserId == u.UserId),

                UsageReportScope.Organization o =>
                    query.Where(p => p.Session!.User!.OrganizationId == o.OrganizationId),

                UsageReportScope.Merchant m when m.StationId is null =>
                    query.Where(p => p.Session!.Device!.Station!.MerchantId == m.MerchantId),

                UsageReportScope.Merchant m =>
                    query.Where(p =>
                        p.Session!.Device!.StationId == m.StationId
                        && p.Session.Device.Station!.MerchantId == m.MerchantId),

                _ => throw new NotSupportedException($"Noma'lum scope turi: {scope.GetType().Name}")
            };
        }

        private static IQueryable<UsageReportRowDto> Project(IQueryable<ProductProcessEntity> query)
        {
            return query.Select(p => new UsageReportRowDto
            {
                ProcessId = p.Id,
                SessionId = p.SessionId,
                StartedAt = p.StartedAt,
                EndedAt = p.EndedAt,
                ProductName = p.ProductName,
                Unit = p.Unit.ToString(),
                RequestedAmount = p.RequestedAmount,
                GivenAmount = p.GivenAmount,
                PricePerUnit = p.PricePerUnit,
                TotalCost = p.GivenAmount * p.PricePerUnit,
                Status = p.Status.ToString(),
                EndReason = p.EndReason == null ? null : p.EndReason.ToString(),
                DeviceSerial = p.Session!.Device!.SerialNumber,
                StationName = p.Session.Device.Station!.Name,
                UserPhone = p.Session.User!.PhoneNumber
            });
        }
    }
}
