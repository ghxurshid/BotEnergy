using System.Text;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// Payme Merchant API — mijoz checkout havolasi/QR orqali to'laydi, Payme esa BIZGA
    /// callback qiladi (<c>PaymeMerchantGateway</c>). Hold YO'Q: pul <c>PerformTransaction</c>
    /// paytida yechiladi (<see cref="PaymentIntentKind.Charge"/>).
    ///
    /// Bu strategiya Payme'ga CHIQUVCHI chaqiruv qilmaydi — shuning uchun:
    ///  - intent yaratish = checkout havolasini yasash (tarmoqqa chiqmaydi);
    ///  - polling yo'q: holatni callback o'zgartiradi, watcher faqat TTL uchun aylanadi;
    ///  - qaytarish ham provider tomonda boshlanadi (biz <c>CancelTransaction</c> ni qabul qilamiz),
    ///    shuning uchun <see cref="PaymentCapabilities.Refund"/> YO'Q va sessiya yopilganda
    ///    ishlatilmagan mablag' avtomatik qaytarilmaydi (<see cref="SettlementMode.None"/>).
    /// </summary>
    public class MerchantPaymentStrategy : PaymentStrategyBase
    {
        private readonly IMerchantRepository _merchants;

        public MerchantPaymentStrategy(
            PaymentStrategyDependencies deps,
            IMerchantRepository merchants,
            ILogger<MerchantPaymentStrategy> logger)
            : base(deps, logger)
        {
            _merchants = merchants;
        }

        public override PaymentStrategyProfile Profile { get; } = new(
            PaymentMethod.Merchant,
            PaymentIntentKind.Charge,
            PaymentCapabilities.ProviderCallback | PaymentCapabilities.Checkout,
            SettlementMode.None);

        protected override string CreatedMessage(PaymentIntentEntity intent)
            => "To'lov havolasi tayyor — Payme orqali to'lovni yakunlang.";

        protected override string CustomerHint =>
            "To'lov havolasi (QR) ochiladi va to'lovni Payme'da yakunlaysiz. Pul darhol yechiladi; "
            + "ishlatilmagan mablag' avtomatik qaytarilmaydi — kerakli summani ajrating.";

        /// <summary>Havola intent yaratilganda tayyor bo'ladi — oldindan hech narsa talab qilinmaydi.</summary>
        public override Task<PaymentPrerequisites> GetPrerequisitesAsync(PaymentSessionEntity ps, long userId)
            => Task.FromResult(new PaymentPrerequisites(RequiresCheckout: true, Hint: CustomerHint));

        /// <summary>Bu usulda provider chek identifikatori yo'q — receipt_id yetarli.</summary>
        protected override bool IsProcessable(PaymentIntentEntity intent)
            => !string.IsNullOrEmpty(intent.ProviderOrderId);

        protected override string NotProcessableReason => "receipt_id yo'q — checkout havolasi yaratilmagan.";

        /// <summary>
        /// Bu usulda tick faqat TTL uchun — holatni callback keltiradi. 3 soniyalik polling
        /// bekorga DB yuki bo'lardi, shuning uchun daqiqada bir marta yetarli.
        /// </summary>
        protected override int PollSeconds => 60;

        // ── To'siqlar ───────────────────────────────────────────────

        protected override async Task<StopFactor?> ValidateCreateAsync(
            PaymentSessionEntity ps, CreatePaymentIntentDto dto)
        {
            var merchant = await _merchants.GetByIdAsync(ps.MerchantId);
            return string.IsNullOrWhiteSpace(merchant?.PaymeMerchantId)
                ? StopFactors.Merchant.PaymeMerchantNotConfigured
                : null;
        }

        // ── "Provider" chaqiruvlari (aslida tarmoqqa chiqmaydi) ─────

        protected override async Task<ProviderFunding> RequestFundingAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CreatePaymentIntentDto dto, CancellationToken ct)
        {
            var merchant = await _merchants.GetByIdAsync(ps.MerchantId);
            if (string.IsNullOrWhiteSpace(merchant?.PaymeMerchantId))
                return new ProviderFunding(new ProviderCall(
                    ProviderOutcome.Permanent, Message: "Merchant API sozlanmagan (PaymeMerchantId yo'q)."));

            var url = BuildCheckoutUrl(merchant!.PaymeMerchantId!, intent.ProviderOrderId, intent.AmountTiyin);

            return new ProviderFunding(
                ProviderCall.Ok("Checkout havolasi yaratildi (chiquvchi chaqiruv yo'q)."),
                CheckoutUrl: url);
        }

        /// <summary>
        /// Payme <c>m=...;ac.receipt_id=...;a=...</c> parametrlarini base64 qilib checkout havolasiga qo'yadi.
        /// </summary>
        private string BuildCheckoutUrl(string paymeMerchantId, string orderId, long amountTiyin)
        {
            var payload = $"m={paymeMerchantId};ac.receipt_id={orderId};a={amountTiyin}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
            return $"{Options.CheckoutBaseUrl.TrimEnd('/')}/{encoded}";
        }

        /// <summary>
        /// Polling yo'q — holatni Payme callback'i o'zgartiradi. Watcher faqat TTL tugaganini
        /// aniqlash uchun aylanadi (to'lanmagan havola abadiy ochiq qolmasin).
        /// </summary>
        protected override Task<ProviderPoll> PollFundingAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
            => Task.FromResult(new ProviderPoll(
                ProviderCall.NoOp("Merchant API: holat callback orqali keladi."),
                FundingPollState.Pending,
                intent.ProviderState));

        /// <summary>Pul PerformTransaction paytida yechilgan — capture chaqiruvi yo'q.</summary>
        protected override Task<ProviderCall> CaptureAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, long amountTiyin, CancellationToken ct)
            => Task.FromResult(ProviderCall.NoOp(
                $"Prepaid (Merchant API): {amountTiyin} tiyin allaqachon yechilgan."));

        /// <summary>
        /// Merchant API'da qaytarishni FAQAT Payme boshlaydi. To'lanmagan intent uchun bu no-op;
        /// to'langani uchun esa operator aralashuvi kerak — avtomatik "qaytardik" deb yozmaymiz.
        /// </summary>
        protected override Task<ProviderCall> RefundAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
            => Task.FromResult(intent.ProviderPerformedAt is null
                ? ProviderCall.NoOp("To'lanmagan tranzaksiya — qaytariladigan mablag' yo'q.")
                : new ProviderCall(ProviderOutcome.Permanent,
                    Message: "Merchant API: qaytarishni Payme boshlaydi (CancelTransaction). Operator aralashuvi kerak."));

        /// <summary>To'lanmagan checkout havolasi — provider chaqiruvisiz yopiladi.</summary>
        protected override Task<ProviderCall> CancelUnpaidAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
            => Task.FromResult(ProviderCall.NoOp("To'lanmagan checkout havolasi bekor qilindi."));
    }
}
