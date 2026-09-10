using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Payments
{
    /// <summary>
    /// Sessiya to'lovining YAGONA almashtiriladigan qismi. Har bir to'lov usuli (Merchant /
    /// Invoice / Subscribe) shu kontraktni bajaradi; sessiya va jarayon servislari
    /// konkret usulni HECH QACHON bilmaydi — ular faqat <see cref="ISessionPaymentService"/>
    /// fasadi orqali gaplashadi.
    ///
    /// Umumiy yadro (intent yozuvi, FIFO consume, audit step, lease/backoff, balans eventi)
    /// <c>PaymentStrategyBase</c> da — strategiya faqat provider bilan gaplashish qismini yozadi.
    /// SessionApi-only: implementatsiyalar ISessionNotifier / IDeviceCommandPublisher'ga bog'liq.
    /// </summary>
    public interface ISessionPaymentStrategy
    {
        PaymentStrategyProfile Profile { get; }

        PaymentMethod Method => Profile.Method;

        // ── To'lov sirti (payment sheet) ───────────────────

        /// <summary>
        /// Mijoz shu usul bilan to'lay olishi uchun nima kerakligi: karta/telefon/checkout.
        /// Ilova to'lov oynasini AYNAN shu javob bo'yicha chizadi — usul nomiga qarab
        /// <c>if</c> yozmaydi.
        /// </summary>
        Task<PaymentPrerequisites> GetPrerequisitesAsync(PaymentSessionEntity ps, long userId);

        // ── Mobil oqim: mablag' ajratish ────────────────────────────

        /// <summary>Yangi to'lov niyati (hold yoki charge — strategiyaga qarab).</summary>
        Task<GenericDto<PaymentIntentResultDto>> CreateIntentAsync(CreatePaymentIntentDto dto, CancellationToken ct = default);

        /// <summary>Umuman ishlatilmagan intent'ni bekor qilish (to'langan bo'lsa qaytariladi).</summary>
        Task<GenericDto<PaymentIntentResultDto>> CancelIntentAsync(long intentId, long userId, CancellationToken ct = default);

        // ── Funding + yakuniy hisob-kitob ───────────────────────────

        /// <summary>Sessiyada ishlatish mumkin bo'lgan mablag' (tiyin). 0 — funding yo'q.</summary>
        Task<long> GetAvailableTiyinAsync(long sessionId);

        /// <summary>Jarayon narxini intent'lardan FIFO tartibda yeydi. Qaytadi: yechilgan summa (UZS).</summary>
        Task<decimal> ConsumeForProcessAsync(long processId);

        /// <summary>
        /// Sessiya yopilishida intent'larga maqsad holat qo'yadi va payment session'ni Settling qiladi.
        /// true — kutish kerak (watcher yakunlaydi), false — darhol yopish mumkin.
        /// </summary>
        Task<bool> BeginSessionSettlementAsync(long sessionId, CancellationToken ct = default);

        // ── Watcher tick ────────────────────────────────────────────

        /// <summary>Navbatdagi intent'larni lease bilan olib provider amallarini bajaradi.</summary>
        Task ProcessDueAsync(string ownerId, CancellationToken ct = default);

        /// <summary>Barcha intent'lari terminal bo'lgan Settling sessiyalarni yopadi.</summary>
        Task FinalizeSettledAsync(CancellationToken ct = default);

        /// <summary>Sessiya to'lov holati o'zgargani haqida yagona event (SignalR + MQTT).</summary>
        Task PublishSessionPaymentStateAsync(long sessionId, string reason, long? intentId = null);

        // ── Provider callback'i (faqat PaymentCapabilities.ProviderCallback usullari) ──
        // Bu yerda polling YO'Q: provider bizga "to'landi"/"qaytarildi" deb xabar beradi,
        // biz esa o'sha yagona yo'l (MarkFunded) orqali balansni yangilaymiz.

        /// <summary>Provider to'lov o'tganini bildirdi. false — holat mos kelmadi (poyga yoki takror).</summary>
        Task<bool> ConfirmFundingFromProviderAsync(long intentId, string? note = null);

        /// <summary>Provider to'lovni qaytarganini bildirdi. false — holat mos kelmadi.</summary>
        Task<bool> ReverseFundingFromProviderAsync(long intentId, string? note = null);
    }
}
