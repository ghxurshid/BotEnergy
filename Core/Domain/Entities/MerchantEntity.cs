using Domain.Attributes;
using Domain.Entities.BaseEntity;
using Domain.Interfaces;

namespace Domain.Entities
{
    public class MerchantEntity : Entity, IHasPhoneNumber
    {
        public required string PhoneNumber { get; set; }

        public required string Inn { get; set; }

        public required string BankAccount { get; set; }

        public required string CompanyName { get; set; }

        public bool IsActive { get; set; } = true;

        // ── Payme credential'lari (hold invoice'lar shu kassa nomidan yaratiladi) ──
        // Admin API orqali write-only: GET'da faqat masked ko'rinadi.

        public string? PaymeCashboxId { get; set; }

        [NotSearchable]
        public string? PaymeKey { get; set; }

        /// <summary>False bo'lsa bu merchant qurilmalarida hold invoice yaratish rad etiladi.</summary>
        public bool PaymeEnabled { get; set; }

        public ICollection<StationEntity>? Stations { get; set; }

        /// <summary>Shu merchantga tegishli (scoped) platform rollari.</summary>
        public ICollection<PlatformRoleEntity>? Roles { get; set; }

        /// <summary>Shu merchant operatorlari (PlatformUser/Merchant).</summary>
        public ICollection<PlatformUserEntity>? Users { get; set; }
    }
}
