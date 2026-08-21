using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Inkassatsiya dalolatnomasi — qurilmadan naqd pul olinganining audit yozuvi.
    ///
    /// <c>ExpectedAmount</c> (so'rov paytidagi server qoldig'i) va <c>CountedAmount</c>
    /// (inkassator sanagani) ikkalasi ham saqlanadi. Farq bo'lsa amal to'xtatilmaydi —
    /// ikkala qiymat yozib qo'yiladi va admin panelda ko'rinadi.
    ///
    /// Merchant/Station denormalizatsiya qilingan: audit ro'yxati va scope filtri
    /// qurilma o'chirilgan/ko'chirilgan bo'lsa ham to'g'ri ishlashi kerak.
    /// </summary>
    public class CashCollectionEntity : Entity
    {
        public long DeviceId { get; set; }
        public DeviceEntity? Device { get; set; }

        public required string SerialNumber { get; set; }

        public long MerchantId { get; set; }
        public long StationId { get; set; }

        /// <summary>Inkassator — <c>auth.platform_users</c> dagi foydalanuvchi.</summary>
        public long IncassatorUserId { get; set; }
        public PlatformUserEntity? IncassatorUser { get; set; }

        public CashCollectionStatus Status { get; set; } = CashCollectionStatus.Requested;

        /// <summary>Box ochish so'ralgan paytdagi server qoldig'i.</summary>
        public decimal ExpectedAmount { get; set; }

        /// <summary>Inkassator sanagan summa — tasdiqlangunicha null.</summary>
        public decimal? CountedAmount { get; set; }

        public string Currency { get; set; } = "UZS";

        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? BoxOpenedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        public string? Notes { get; set; }
    }
}
