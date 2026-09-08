using Domain.Enums;

namespace Domain.Dtos
{
    public class CreateMerchantDto
    {
        public required string PhoneNumber { get; set; }
        public required string Inn { get; set; }
        public required string BankAccount { get; set; }
        public required string CompanyName { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateMerchantDto
    {
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    public class MerchantItemDto
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Inn { get; set; } = string.Empty;
        public string BankAccount { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        // Payme — kalitlar hech qachon ochiq qaytarilmaydi (faqat masked).
        public string? PaymeCashboxId { get; set; }
        public string? PaymeKeyMasked { get; set; }
        public bool PaymeEnabled { get; set; }

        /// <summary>Merchant API checkout identifikatori (sir emas — havolada ko'rinadi).</summary>
        public string? PaymeMerchantId { get; set; }
        public string? PaymeMerchantKeyMasked { get; set; }

        // ── To'lov strategiyasi (merchant runtime'da almashtiradi) ──

        public PaymentMethod DefaultPaymentMethod { get; set; }

        /// <summary>Mijoz tanlashi mumkin bo'lgan usullar.</summary>
        public List<PaymentMethod> EnabledPaymentMethods { get; set; } = new();

        /// <summary>Prepaid usullarda ishlatilmagan mablag' qaytariladimi.</summary>
        public bool RefundUnusedFunds { get; set; }

        /// <summary>Credential'lari to'liq bo'lgan (ya'ni hozir tanlash mumkin) usullar.</summary>
        public List<PaymentMethod> ConfigurablePaymentMethods { get; set; } = new();
    }

    /// <summary>
    /// Merchant to'lov strategiyasini o'zgartirish — RUNTIME sozlama, restart talab qilmaydi.
    /// O'zgarish KEYINGI sessiyalarga ta'sir qiladi; ochiq sessiyalar o'z usuli bilan yakunlanadi.
    /// </summary>
    public class SetPaymentMethodsDto
    {
        /// <summary>Yangi sessiyalar uchun sukut bo'yicha usul. <see cref="EnabledMethods"/> ichida bo'lishi shart.</summary>
        public PaymentMethod DefaultMethod { get; set; }

        /// <summary>Ruxsat etilgan usullar (kamida bittasi).</summary>
        public List<PaymentMethod> EnabledMethods { get; set; } = new();

        /// <summary>Prepaid usullarda ishlatilmagan mablag' avtomatik qaytarilsinmi.</summary>
        public bool RefundUnusedFunds { get; set; } = true;
    }

    /// <summary>Payme Merchant API credential'lari (kassa credential'laridan alohida).</summary>
    public class SetPaymeMerchantCredentialsDto
    {
        /// <summary>Checkout havolasidagi <c>m=</c> qiymati.</summary>
        public required string MerchantId { get; set; }

        /// <summary>Payme callback'larini autentifikatsiya qiluvchi kalit.</summary>
        public required string Key { get; set; }
    }

    public class SetPaymeCredentialsDto
    {
        public required string CashboxId { get; set; }
        public required string Key { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class MerchantResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
