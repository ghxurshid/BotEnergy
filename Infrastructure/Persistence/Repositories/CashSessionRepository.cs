using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories
{
    public class CashSessionRepository : ICashSessionRepository
    {
        private readonly AppDbContext _context;

        /// <summary>Ochiq deb sanaladigan holatlar — partial unique index filtri bilan bir xil.</summary>
        private static readonly CashSessionStatus[] OpenStatuses =
            { CashSessionStatus.Accepting, CashSessionStatus.Committing };

        public CashSessionRepository(AppDbContext context)
            => _context = context;

        public Task<CashSessionEntity?> GetByIdAsync(long id)
            => _context.CashSessions.FirstOrDefaultAsync(s => s.Id == id);

        public Task<CashSessionEntity?> GetActiveByDeviceAsync(long deviceId)
            => _context.CashSessions
                .Where(s => s.DeviceId == deviceId && OpenStatuses.Contains(s.Status))
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

        public Task<CashSessionEntity?> GetByIdempotencyKeyAsync(string idempotencyKey)
            => _context.CashSessions.FirstOrDefaultAsync(s => s.IdempotencyKey == idempotencyKey);

        public async Task<CashSessionEntity> CreateAsync(CashSessionEntity session)
        {
            await _context.CashSessions.AddAsync(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<CashSessionEntity> UpdateAsync(CashSessionEntity session)
        {
            _context.CashSessions.Update(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<CashBillAddResult> TryAddBillAsync(
            long sessionId, long deviceId, string serialNumber, decimal denomination, int billSeq)
        {
            // 1. Kupyurani yozamiz. ON CONFLICT DO NOTHING — takroriy yuborilgan xabar
            //    (device javobni olmay qayta yuborsa) jimgina tashlab yuboriladi.
            var insertedIds = await _context.Database
                .SqlQuery<long>($@"
INSERT INTO app.cash_session_bills
    (cash_session_id, device_id, serial_number, denomination, currency, bill_seq,
     accepted_at, created_date, updated_date, is_deleted)
VALUES
    ({sessionId}, {deviceId}, {serialNumber}, {denomination}, 'UZS', {billSeq},
     LOCALTIMESTAMP, LOCALTIMESTAMP, LOCALTIMESTAMP, false)
ON CONFLICT (cash_session_id, bill_seq) DO NOTHING
RETURNING id AS ""Value""")
                .ToListAsync();

            var added = insertedIds.Count > 0;

            if (added)
            {
                // 2. Jamini relative UPDATE bilan oshiramiz — read-modify-write yo'q.
                //    status sharti: faqat qabul qilish rejimidagi sessiya summasi o'sadi.
                var totals = await _context.Database
                    .SqlQuery<decimal>($@"
UPDATE app.cash_sessions
SET accepted_amount = accepted_amount + {denomination},
    bill_count = bill_count + 1,
    last_activity_at = LOCALTIMESTAMP,
    updated_date = LOCALTIMESTAMP
WHERE id = {sessionId} AND status = {(int)CashSessionStatus.Accepting} AND is_deleted = false
RETURNING accepted_amount AS ""Value""")
                    .ToListAsync();

                if (totals.Count == 0)
                {
                    // Sessiya oraliqda yopilgan — kupyura yozuvi audit uchun qoladi,
                    // lekin jami oshmadi. Chaqiruvchi tranzaksiyani rollback qiladi.
                    added = false;
                }
            }

            // 3. Har holatda joriy haqiqiy qiymatlarni qaytaramiz — qurilma ekranida
            //    serverdagi summa ko'rsatiladi.
            var snapshot = await _context.CashSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId)
                .Select(s => new { s.AcceptedAmount, s.BillCount })
                .FirstOrDefaultAsync();

            return new CashBillAddResult(
                added,
                snapshot?.AcceptedAmount ?? 0m,
                snapshot?.BillCount ?? 0);
        }

        public async Task<List<CashSessionEntity>> ClaimDueAsync(string ownerId, DateTime leaseUntil, int batch)
        {
            // Raw SQL da DateTime parametrining Npgsql standart turi 'timestamptz' — u faqat UTC
            // qabul qiladi. Ustun esa 'timestamp without time zone', shuning uchun turni oshkora beramiz.
            var leaseParam = new NpgsqlParameter("lease", NpgsqlDbType.Timestamp)
            {
                Value = DateTime.SpecifyKind(leaseUntil, DateTimeKind.Unspecified)
            };

            var claimedIds = await _context.Database
                .SqlQuery<long>($@"
UPDATE app.cash_sessions
SET locked_by = {ownerId}, lease_until = {leaseParam}, updated_date = LOCALTIMESTAMP
WHERE id IN (
    SELECT id FROM app.cash_sessions
    WHERE status = {(int)CashSessionStatus.PayoutFailed}
      AND next_attempt_at IS NOT NULL AND next_attempt_at <= LOCALTIMESTAMP
      AND (lease_until IS NULL OR lease_until < LOCALTIMESTAMP)
      AND is_deleted = false
    ORDER BY next_attempt_at
    LIMIT {batch}
    FOR UPDATE SKIP LOCKED)
RETURNING id AS ""Value""")
                .ToListAsync();

            if (claimedIds.Count == 0)
                return new List<CashSessionEntity>();

            return await _context.CashSessions
                .Where(s => claimedIds.Contains(s.Id))
                .OrderBy(s => s.NextAttemptAt)
                .ToListAsync();
        }

        public Task ReleaseLeaseAsync(long id, string ownerId)
            => _context.CashSessions
                .Where(s => s.Id == id && s.LockedBy == ownerId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.LockedBy, (string?)null)
                    .SetProperty(s => s.LeaseUntil, (DateTime?)null)
                    .SetProperty(s => s.UpdatedDate, DateTime.Now));

        public Task<List<CashSessionEntity>> GetIdleAsync(DateTime threshold)
            => _context.CashSessions
                .Where(s => s.Status == CashSessionStatus.Accepting && s.LastActivityAt < threshold)
                .OrderBy(s => s.LastActivityAt)
                .ToListAsync();

        public Task<PagedResult<CashSessionEntity>> GetAllAsync(
            PaginationParams param, long? merchantId = null, CashSessionStatus? status = null)
            => _context.CashSessions
                .Include(s => s.Device)!
                    .ThenInclude(d => d!.Station)
                .Where(s => merchantId == null || s.Device!.Station!.MerchantId == merchantId)
                .Where(s => status == null || s.Status == status)
                .ApplyListQuery(param)
                .ToPagedResultAsync(param);
    }
}
