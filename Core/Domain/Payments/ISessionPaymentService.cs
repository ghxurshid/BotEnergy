using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Payments
{
    /// <summary>
    /// Sessiya va jarayon servislari uchun YAGONA to'lov kirish nuqtasi. Strategiyani o'zi
    /// tanlaydi (sessiyada qotirilgan usul bo'yicha) — chaqiruvchi usulni bilmaydi.
    /// SessionApi-only.
    /// </summary>
    public interface ISessionPaymentService
    {
        Task<long> GetAvailableTiyinAsync(long sessionId);
        Task<decimal> ConsumeForProcessAsync(long processId);
        Task<bool> BeginSessionSettlementAsync(long sessionId, CancellationToken ct = default);
        Task PublishSessionPaymentStateAsync(long sessionId, string reason, long? intentId = null);

        /// <summary>Mobil: yangi to'lov niyati. Usul sessiyada qotirilganidan olinadi.</summary>
        Task<GenericDto<PaymentIntentResultDto>> CreateIntentAsync(CreatePaymentIntentDto dto, CancellationToken ct = default);

        Task<GenericDto<PaymentIntentResultDto>> CancelIntentAsync(long intentId, long userId, CancellationToken ct = default);

        /// <summary>Sessiya to'lov konteksti: balans + intent ro'yxati (mobil uchun).</summary>
        Task<GenericDto<PaymentSessionDto>> GetForSessionAsync(long sessionId, long userId);

        /// <summary>
        /// Mahsulot tanlangandan keyingi TO'LOV OYNASI: usul, narx, qancha to'lash kerakligi,
        /// saqlangan kartalar va usul talab qiladigan narsalar — bitta javobda.
        /// </summary>
        Task<GenericDto<PaymentCheckoutDto>> GetCheckoutAsync(PaymentCheckoutQueryDto query);

        /// <summary>
        /// Sessiya snapshot'iga qo'shiladigan qisqa to'lov holati. To'lov konteksti bo'lmasa null
        /// — chaqiruvchi (sessiya servisi) buni xato deb hisoblamaydi.
        /// </summary>
        Task<SessionPaymentSnapshotDto?> GetSnapshotAsync(long sessionId);

        /// <summary>Sessiyaning barcha intent'lari (FIFO tartibda).</summary>
        Task<GenericDto<List<PaymentIntentItemDto>>> GetIntentsForSessionAsync(long sessionId, long userId);
    }

    /// <summary>To'lov oynasi so'rovi — sessiya + (ixtiyoriy) tanlangan mahsulot va miqdor.</summary>
    public sealed class PaymentCheckoutQueryDto
    {
        public long SessionId { get; set; }
        public long UserId { get; set; }

        /// <summary>Mijoz tanlagan mahsulot. Berilmasa oyna faqat balans holatini qaytaradi.</summary>
        public long? ProductId { get; set; }

        /// <summary>Mijoz so'ragan miqdor (litr/kWh...). Berilmasa mavjud mablag' bo'yicha hisoblanadi.</summary>
        public decimal? RequestedAmount { get; set; }
    }

    /// <summary>
    /// Sessiya/merchant uchun qaysi strategiya ishlashini aniqlaydi.
    /// Tanlash zanjiri: PaymentSession.Method (qotirilgan) → merchant sozlamasi (runtime)
    /// → global config default.
    /// </summary>
    public interface ISessionPaymentStrategyResolver
    {
        /// <summary>Ro'yxatga olingan barcha strategiyalar (watcher shular bo'ylab aylanadi).</summary>
        IReadOnlyList<ISessionPaymentStrategy> All { get; }

        /// <summary>Usul bo'yicha strategiya. Ro'yxatda bo'lmasa null.</summary>
        ISessionPaymentStrategy? ForMethod(PaymentMethod method);

        /// <summary>Sessiyada qotirilgan usul bo'yicha. Payment session hali yo'q bo'lsa null.</summary>
        Task<ISessionPaymentStrategy?> ForSessionAsync(long sessionId);

        /// <summary>Jarayon → sessiya → strategiya.</summary>
        Task<ISessionPaymentStrategy?> ForProcessAsync(long processId);

        /// <summary>
        /// Merchant sozlamasidan (runtime) yangi sessiya uchun usulni tanlaydi:
        /// merchant default'i yoqilgan bo'lsa o'sha, aks holda global config default.
        /// <paramref name="requested"/> berilsa — merchant ruxsat etgan bo'lsagina.
        /// </summary>
        Task<PaymentMethod> ResolveForMerchantAsync(MerchantEntity merchant, PaymentMethod? requested = null);
    }
}
