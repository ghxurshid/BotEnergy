using Domain.Attributes;
using Domain.Entities.BaseEntity;
using Domain.Enums;
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

        // ── Payme Receipts/Subscribe API credential'lari (kassa) ──
        // Admin API orqali write-only: GET'da faqat masked ko'rinadi.

        public string? PaymeCashboxId { get; set; }

        [NotSearchable]
        public string? PaymeKey { get; set; }

        /// <summary>False bo'lsa bu merchant qurilmalarida to'lov niyati yaratish rad etiladi.</summary>
        public bool PaymeEnabled { get; set; }

        // ── Payme Merchant API credential'lari (provider BIZGA callback qiladi) ──
        // Receipts API kassasidan alohida: checkout link'dagi "m=" va Payme'ning
        // bizga Basic-auth bilan kirishida ishlatiladigan parol.

        /// <summary>Checkout havolasidagi merchant id (<c>m=</c>).</summary>
        public string? PaymeMerchantId { get; set; }

        /// <summary>Payme callback'larini autentifikatsiya qiluvchi kalit (Basic Paycom:&lt;key&gt;).</summary>
        [NotSearchable]
        public string? PaymeMerchantKey { get; set; }

        // ── To'lov strategiyasi (runtime'da almashadi) ──────────────
        // Merchant o'zi istalgan paytda o'zgartiradi; o'zgarish KEYINGI sessiyalarga
        // ta'sir qiladi — ochiq sessiya o'z usuli bilan yakunlanadi.

        /// <summary>Yangi sessiyalar uchun sukut bo'yicha usul.</summary>
        public PaymentMethod DefaultPaymentMethod { get; set; } = PaymentMethod.Subscribe;

        /// <summary>Mijoz tanlashi mumkin bo'lgan usullar (bitmask). Default'ni ham o'z ichiga olishi shart.</summary>
        public PaymentMethodFlags EnabledPaymentMethods { get; set; } = PaymentMethodFlags.Subscribe;

        /// <summary>
        /// Prepaid usullarda (Invoice/Merchant) sessiya yopilganda ishlatilmagan mablag'
        /// avtomatik qaytarilsinmi. False — mablag' merchantda qoladi (SettlementMode.None).
        /// Hold (Subscribe) uchun ahamiyatsiz — u yerda ishlatilmagan qism o'zi bo'shaydi.
        /// </summary>
        public bool RefundUnusedFunds { get; set; } = true;

        public ICollection<StationEntity>? Stations { get; set; }

        /// <summary>Shu merchantga tegishli (scoped) platform rollari.</summary>
        public ICollection<PlatformRoleEntity>? Roles { get; set; }

        /// <summary>Shu merchant operatorlari (PlatformUser/Merchant).</summary>
        public ICollection<PlatformUserEntity>? Users { get; set; }
    }
}
