using Domain.Dtos.Payment;
using Domain.Enums;

namespace Domain.Dtos.PaymentSession
{
    /// <summary>
    /// Mahsulot tanlangandan keyin ilova ko'rsatadigan TO'LOV OYNASI — bitta so'rovda
    /// hamma narsa: sessiyaning qotirilgan usuli, tanlangan mahsulot narxi, hozir qancha
    /// to'lash kerakligi, saqlangan kartalar va nima qilish kerakligi.
    ///
    /// Ilova bu javobga qarab UI chizadi va to'lov usuli nomiga qarab HECH QANDAY
    /// shart yozmaydi: <c>requiresCard</c> bo'lsa kartalar ro'yxati,
    /// <c>requiresPhone</c> bo'lsa raqam maydoni, <c>requiresCheckout</c> bo'lsa havola.
    /// </summary>
    public class PaymentCheckoutDto
    {
        public long SessionId { get; set; }
        public long PaymentSessionId { get; set; }
        public PaymentSessionStatus PaymentSessionStatus { get; set; }

        /// <summary>Sessiya ochilganda qotirilgan usul — mijoz uni almashtira olmaydi.</summary>
        public PaymentMethod Method { get; set; }

        /// <summary>Pul ushlanadimi (Hold) yoki darhol yechiladimi (Charge).</summary>
        public PaymentIntentKind Kind { get; set; }

        /// <summary>True — Hold: yopilishda faqat ishlatilgani yechiladi, qolgani bo'shaydi.</summary>
        public bool IsHold { get; set; }

        public long MerchantId { get; set; }
        public string? MerchantName { get; set; }

        /// <summary>Tanlangan mahsulot (so'rovda productId berilgan bo'lsa).</summary>
        public CheckoutProductDto? Product { get; set; }

        /// <summary>Mijoz so'ragan miqdor (litr/kWh/daqiqa...). Berilmasa 0.</summary>
        public decimal RequestedAmount { get; set; }

        /// <summary>Tanlangan miqdor uchun taxminiy narx (so'm). Miqdor berilmasa 0.</summary>
        public decimal EstimatedCostUzs { get; set; }

        // ── Sessiya balansi ────────────────────────────────

        public long FundedTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long AvailableTiyin { get; set; }
        public decimal AvailableUzs { get; set; }

        /// <summary>Tanlangan miqdorni berish uchun yana qancha to'lash kerak (so'm). 0 — yetadi.</summary>
        public decimal AmountToFundUzs { get; set; }

        /// <summary>Ilova to'lov maydoniga oldindan yozadigan summa (so'm).</summary>
        public decimal SuggestedAmountUzs { get; set; }

        public decimal MinAmountUzs { get; set; }

        /// <summary>Bir sessiyada ochiq turishi mumkin bo'lgan to'lovlar soni.</summary>
        public int MaxActiveIntents { get; set; }

        public int ActiveIntentCount { get; set; }

        /// <summary>Mavjud mablag' tanlangan miqdorga yetadimi (yetsa to'lovsiz boshlash mumkin).</summary>
        public bool CanStartNow { get; set; }

        // ── Usul talab qiladigan narsalar (strategiyadan) ───────

        /// <summary>Saqlangan karta bilan to'lanadi — ilova kartalar ro'yxatini ko'rsatadi.</summary>
        public bool RequiresCard { get; set; }

        /// <summary>Tasdiqlangan, to'lovga yaroqli karta bormi.</summary>
        public bool HasUsableCard { get; set; }

        /// <summary>Karta ko'rsatilmasa ishlatiladigan karta (asosiy).</summary>
        public long? SuggestedCardId { get; set; }

        /// <summary>Shu merchant kassasida saqlangan kartalar (token qaytarilmaydi).</summary>
        public List<CardItemDto> Cards { get; set; } = new();

        /// <summary>Chek telefonga yuboriladi — raqam kerak.</summary>
        public bool RequiresPhone { get; set; }

        /// <summary>Profildan aniqlangan raqam (bo'lsa) — ilova uni oldindan to'ldiradi.</summary>
        public string? Phone { get; set; }

        /// <summary>Mijoz tashqi havola/QR orqali to'lovni yakunlaydi.</summary>
        public bool RequiresCheckout { get; set; }

        /// <summary>Hozir to'lov yaratish mumkinmi (usulning barcha sharti bajarilgan).</summary>
        public bool CanCreateIntent { get; set; }

        /// <summary>CanCreateIntent=false bo'lsa — mijozga aytiladigan aniq sabab.</summary>
        public string? MissingRequirement { get; set; }

        /// <summary>Usul qanday ishlashini tushuntiruvchi qisqa matn (strategiya yozadi).</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>Sessiyaning barcha to'lovlari (FIFO tartibda).</summary>
        public List<PaymentIntentItemDto> Intents { get; set; } = new();
    }

    /// <summary>To'lov oynasidagi mahsulot snapshot'i.</summary>
    public class CheckoutProductDto
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerUnit { get; set; }

        /// <summary>Mavjud mablag'ga qancha berish mumkin (birlikda).</summary>
        public decimal MaxAmountByFunds { get; set; }
    }

    /// <summary>
    /// Sessiya snapshot'iga qo'shiladigan qisqa to'lov holati — <c>Bootstrap</c>/<c>Current</c>
    /// javobida keladi, ilova cold start'da to'lov holatini darhol ko'rsatadi.
    /// </summary>
    public class SessionPaymentSnapshotDto
    {
        public long PaymentSessionId { get; set; }
        public PaymentSessionStatus Status { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentIntentKind Kind { get; set; }
        public bool IsHold { get; set; }
        public long MerchantId { get; set; }

        public long FundedTiyin { get; set; }
        public long ConsumedTiyin { get; set; }
        public long AvailableTiyin { get; set; }
        public decimal AvailableUzs { get; set; }

        /// <summary>Terminal bo'lmagan to'lovlar soni (kutilayotgan/ishlatilayotgan).</summary>
        public int ActiveIntentCount { get; set; }

        /// <summary>Mijozdan amal kutayotgan to'lov bormi (tasdiqlash / havolani ochish).</summary>
        public bool RequiresUserAction { get; set; }

        /// <summary>Amal kutayotgan to'lovning havolasi (bo'lsa).</summary>
        public string? CheckoutUrl { get; set; }
    }
}
