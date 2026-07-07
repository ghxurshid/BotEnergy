using Domain.Interfaces;
using Persistence.Context;

namespace Persistence.Repositories
{
    public sealed class TransactionRunner : ITransactionRunner
    {
        private readonly AppDbContext _context;

        public TransactionRunner(AppDbContext context) => _context = context;

        public async Task<T> RunAsync<T>(Func<Task<T>> action)
        {
            // Tashqi tranzaksiya bor bo'lsa — unga qo'shilamiz (commit/rollback tashqarida).
            if (_context.Database.CurrentTransaction is not null)
                return await action();

            await using var tx = await _context.Database.BeginTransactionAsync();
            var result = await action();
            await tx.CommitAsync();
            return result;
        }

        public async Task RunAsync(Func<Task> action)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action();
                return;
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            await action();
            await tx.CommitAsync();
        }
    }
}
