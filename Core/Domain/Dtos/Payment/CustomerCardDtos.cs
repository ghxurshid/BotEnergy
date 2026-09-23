namespace Domain.Dtos.Payment
{
    /// <summary>
    /// Karta qo'shish. PAN va amal muddati serverda SAQLANMAYDI — faqat provider'ga
    /// uzatiladi va javobdagi token saqlanadi.
    /// </summary>
    public class AddCardDto
    {
        public long UserId { get; set; }

        /// <summary>Token qaysi merchant kassasida yaratilsin (token o'sha kassaga bog'lanadi).</summary>
        public long MerchantId { get; set; }

        /// <summary>16 xonali karta raqami (bo'shliqsiz).</summary>
        public string Number { get; set; } = string.Empty;

        /// <summary>Amal muddati MMYY (masalan 0329).</summary>
        public string Expire { get; set; } = string.Empty;
    }

    public class VerifyCardDto
    {
        public long UserId { get; set; }
        public long CardId { get; set; }

        /// <summary>Karta egasining telefoniga kelgan SMS kod.</summary>
        public string Code { get; set; } = string.Empty;
    }

    public class CardItemDto
    {
        public long CardId { get; set; }
        public long MerchantId { get; set; }

        /// <summary>Maskalangan raqam — to'liq PAN hech qachon qaytarilmaydi.</summary>
        public string MaskedNumber { get; set; } = string.Empty;

        public string? Expire { get; set; }
        public bool IsVerified { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    /// <summary>Karta qo'shilgandan keyingi javob — tasdiqlash kodi holati bilan.</summary>
    public class AddCardResultDto
    {
        public CardItemDto Card { get; set; } = new();

        /// <summary>SMS kod yuborildimi. false bo'lsa ResendCode bilan qayta so'rash mumkin.</summary>
        public bool VerificationSent { get; set; }

        /// <summary>Kod yuborilgan telefon (provider maskalab qaytaradi).</summary>
        public string? VerificationPhone { get; set; }

        public string ResultMessage { get; set; } = string.Empty;
    }

    public class CardResultDto
    {
        public long CardId { get; set; }
        public string ResultMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Mijozga karta qo'shish uchun tanlanadigan merchant (kassa). Faqat
    /// Payme sozlangan, faol merchantlar qaytariladi — mobil ilova sessiyasiz
    /// ham (masalan asosiy ekrandan) karta qo'sha olishi uchun.
    /// Mijozga faqat identifikator va nom ko'rsatiladi, sirlar hech qachon emas.
    /// </summary>
    public class CardMerchantDto
    {
        public long MerchantId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }
}
