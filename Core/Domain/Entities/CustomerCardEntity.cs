using Domain.Attributes;
using Domain.Entities.BaseEntity;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Mijozning saqlangan karta tokeni — Subscribe strategiyasi shu token bilan to'laydi
    /// (mijoz Payme ilovasiga o'tmaydi). Token provider tomonda yaratiladi va faqat
    /// tasdiqlangandan (SMS kod) keyin ishlatiladi.
    ///
    /// PAN va CVV hech qachon saqlanmaydi — faqat token va maskalangan raqam.
    ///
    /// MUHIM: Payme tokeni MERCHANT KASSASIGA bog'langan — bir karta har bir merchant uchun
    /// alohida tokenlanadi. Shuning uchun yozuv (user, merchant) juftligiga tegishli.
    /// </summary>
    public class CustomerCardEntity : Entity
    {
        public long UserId { get; set; }
        public CustomerUserEntity? User { get; set; }

        /// <summary>Token qaysi merchant kassasida yaratilgan — boshqa merchantda ishlamaydi.</summary>
        public long MerchantId { get; set; }
        public MerchantEntity? Merchant { get; set; }

        public PaymentProvider Provider { get; set; } = PaymentProvider.Payme;

        /// <summary>Provider karta tokeni — sirli qiymat, API javoblarida HECH QACHON qaytarilmaydi.</summary>
        [NotSearchable]
        public string Token { get; set; } = string.Empty;

        /// <summary>Maskalangan raqam (860600******1234).</summary>
        public string MaskedNumber { get; set; } = string.Empty;

        /// <summary>Amal muddati (MMYY) — provider qaytargan ko'rinishda.</summary>
        public string? Expire { get; set; }

        /// <summary>Karta turi/nomi (UZCARD, HUMO...) — provider qaytarsa.</summary>
        public string? CardType { get; set; }

        /// <summary>SMS kod bilan tasdiqlangunicha to'lovga ishlatilmaydi.</summary>
        public bool IsVerified { get; set; }

        public DateTime? VerifiedAt { get; set; }

        /// <summary>Shu merchant uchun asosiy karta — intent yaratishda karta ko'rsatilmasa shu ishlatiladi.</summary>
        public bool IsDefault { get; set; }

        /// <summary>Oxirgi muvaffaqiyatli to'lov vaqti (diagnostika uchun).</summary>
        public DateTime? LastUsedAt { get; set; }
    }
}
