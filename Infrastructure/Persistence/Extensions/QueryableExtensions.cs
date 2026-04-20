using Domain.Dtos.Base;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query,
            PaginationParams param,
            CancellationToken cancellationToken = default)
        {
            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((param.PageNumber - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<T>
            {
                Items = items,
                PageNumber = param.PageNumber,
                PageSize = param.PageSize,
                TotalCount = totalCount
            };
        }
    }
}
