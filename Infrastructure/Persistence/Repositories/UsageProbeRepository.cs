using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories
{
    /// <summary>
    /// <see cref="IUsageProbeRepository"/> implementatsiyasi — hammasi <c>AnyAsync</c>/<c>CountAsync</c>,
    /// entity yuklamaydi. Global soft-delete filtri avtomatik qo'llanadi.
    /// </summary>
    public class UsageProbeRepository : IUsageProbeRepository
    {
        /// <summary>Yopilmagan sessiya holatlari — bulardan biri bo'lsa qurilma/foydalanuvchi band.</summary>
        private static readonly SessionStatus[] LiveSessionStatuses =
        {
            SessionStatus.Created,
            SessionStatus.Connected,
            SessionStatus.InProcess,
            SessionStatus.Paused,
            SessionStatus.Settling
        };

        /// <summary>Tugallanmagan naqd sessiya holatlari (pul hali kartaga o'tmagan yoki o'tmoqda).</summary>
        private static readonly CashSessionStatus[] OpenCashStatuses =
        {
            CashSessionStatus.Accepting,
            CashSessionStatus.Committing,
            CashSessionStatus.PayoutFailed
        };

        /// <summary>Yakunlanmagan inkassatsiya holatlari.</summary>
        private static readonly CashCollectionStatus[] OpenCollectionStatuses =
        {
            CashCollectionStatus.Requested,
            CashCollectionStatus.BoxOpened
        };

        private readonly AppDbContext _context;

        public UsageProbeRepository(AppDbContext context) => _context = context;

        // ── Qurilma ───────────────────────────────────────────────

        public Task<bool> DeviceHasActiveSessionAsync(long deviceId)
            => _context.Sessions
                .AnyAsync(s => s.DeviceId == deviceId && LiveSessionStatuses.Contains(s.Status));

        public Task<bool> DeviceHasOpenCashSessionAsync(long deviceId)
            => _context.CashSessions
                .AnyAsync(s => s.DeviceId == deviceId && OpenCashStatuses.Contains(s.Status));

        public Task<bool> DeviceHasOpenCollectionAsync(long deviceId)
            => _context.CashCollections
                .AnyAsync(c => c.DeviceId == deviceId && OpenCollectionStatuses.Contains(c.Status));

        // ── Stansiya ──────────────────────────────────────────────

        public Task<int> StationDeviceCountAsync(long stationId)
            => _context.Devices.CountAsync(d => d.StationId == stationId);

        public Task<bool> StationHasActiveSessionAsync(long stationId)
            => _context.Sessions
                .AnyAsync(s => s.Device!.StationId == stationId && LiveSessionStatuses.Contains(s.Status));

        // ── Merchant ──────────────────────────────────────────────

        public Task<int> MerchantStationCountAsync(long merchantId)
            => _context.Stations.CountAsync(s => s.MerchantId == merchantId);

        public Task<int> MerchantOperatorCountAsync(long merchantId)
            => _context.PlatformUsers.CountAsync(u => u.MerchantId == merchantId);

        public Task<bool> MerchantHasActiveSessionAsync(long merchantId)
            => _context.Sessions
                .AnyAsync(s => s.Device!.Station!.MerchantId == merchantId
                            && LiveSessionStatuses.Contains(s.Status));

        // ── Tashkilot ─────────────────────────────────────────────

        public Task<int> OrganizationUserCountAsync(long organizationId)
            => _context.CustomerUsers.CountAsync(u => u.OrganizationId == organizationId);

        // ── Mahsulot ──────────────────────────────────────────────

        public Task<bool> ProductHasActiveProcessAsync(long productId)
            => _context.ProductProcesses
                .AnyAsync(p => p.ProductId == productId && p.Status != ProcessStatus.Ended);

        // ── Rol ───────────────────────────────────────────────────

        public Task<int> PlatformRoleUserCountAsync(long roleId)
            => _context.PlatformUsers.CountAsync(u => u.RoleId == roleId);

        public Task<int> CustomerRoleUserCountAsync(long roleId)
            => _context.CustomerUsers.CountAsync(u => u.RoleId == roleId);

        // ── Foydalanuvchi ─────────────────────────────────────────

        public Task<bool> CustomerUserHasActiveSessionAsync(long userId)
            => _context.Sessions
                .AnyAsync(s => s.UserId == userId && LiveSessionStatuses.Contains(s.Status));

        public Task<int> ActiveManageUserCountAsync(long? excludeUserId = null)
            => _context.PlatformUsers
                .CountAsync(u => u.Type == PlatformUserType.Manage
                              && !u.IsBlocked
                              && (excludeUserId == null || u.Id != excludeUserId));
    }
}
