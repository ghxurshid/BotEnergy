using Domain.Enums;

namespace Domain.Helpers
{
    /// <summary>
    /// Payment intent statuslari uchun YAGONA ruxsat jadvali — uchala strategiya (Merchant/
    /// Invoice/Subscribe) shu jadvaldan foydalanadi. Har qanday status o'zgarishi faqat
    /// <c>IPaymentIntentRepository.TryTransitionAsync</c> orqali va shu jadvalga muvofiq bo'ladi —
    /// Refunded→Funded, Settled→Funded kabi noto'g'ri o'tishlar hech qachon mumkin emas.
    /// </summary>
    public static class PaymentIntentStateMachine
    {
        private static readonly IReadOnlyDictionary<PaymentIntentStatus, PaymentIntentStatus[]> Allowed =
            new Dictionary<PaymentIntentStatus, PaymentIntentStatus[]>
            {
                [PaymentIntentStatus.Created] = new[]
                {
                    PaymentIntentStatus.WaitingForConfirmation,
                    PaymentIntentStatus.Cancelled,
                    PaymentIntentStatus.Expired,
                    PaymentIntentStatus.Failed
                },
                [PaymentIntentStatus.WaitingForConfirmation] = new[]
                {
                    PaymentIntentStatus.Funded,
                    // Settlement paytida to'lanmagan yoki poyga holatidagi receiptni watcher
                    // receipts.cancel bilan bekor qiladi (paid bo'lib qolgan bo'lsa pul qaytadi).
                    PaymentIntentStatus.RefundPending,
                    PaymentIntentStatus.Cancelled,
                    PaymentIntentStatus.Expired,
                    PaymentIntentStatus.Failed
                },
                [PaymentIntentStatus.Funded] = new[]
                {
                    PaymentIntentStatus.PartiallyConsumed,
                    PaymentIntentStatus.FullyConsumed,
                    // Prepaid strategiyalar (Invoice/Merchant): pul allaqachon yechilgan, shuning uchun
                    // ishlatilmagan Funded ham to'g'ridan yakuniy hisob-kitobga tushishi mumkin.
                    PaymentIntentStatus.SettlePending,
                    PaymentIntentStatus.RefundPending,
                    PaymentIntentStatus.Cancelled,
                    PaymentIntentStatus.Failed
                },
                [PaymentIntentStatus.PartiallyConsumed] = new[]
                {
                    PaymentIntentStatus.FullyConsumed,
                    PaymentIntentStatus.SettlePending,
                    PaymentIntentStatus.Failed
                },
                [PaymentIntentStatus.FullyConsumed] = new[]
                {
                    PaymentIntentStatus.SettlePending,
                    PaymentIntentStatus.Failed
                },
                [PaymentIntentStatus.SettlePending] = new[]
                {
                    PaymentIntentStatus.Settled,
                    PaymentIntentStatus.Failed
                },
                [PaymentIntentStatus.RefundPending] = new[]
                {
                    PaymentIntentStatus.Refunded,
                    PaymentIntentStatus.Failed,
                    // Auto-correct: refund navbatida turganda consumed>0 aniqlansa
                    // (cancel/consume poygasi) watcher capture'ga o'tkazadi — mablag' yo'qolmaydi.
                    PaymentIntentStatus.SettlePending
                },
                // Failed'dan chiqish faqat operator retry orqali (maqsad holatga qaytarish).
                [PaymentIntentStatus.Failed] = new[]
                {
                    PaymentIntentStatus.SettlePending,
                    PaymentIntentStatus.RefundPending
                },
                // Terminal holatlar
                [PaymentIntentStatus.Settled] = Array.Empty<PaymentIntentStatus>(),
                [PaymentIntentStatus.Refunded] = Array.Empty<PaymentIntentStatus>(),
                [PaymentIntentStatus.Cancelled] = Array.Empty<PaymentIntentStatus>(),
                [PaymentIntentStatus.Expired] = Array.Empty<PaymentIntentStatus>()
            };

        public static bool CanTransition(PaymentIntentStatus from, PaymentIntentStatus to)
            => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

        /// <summary>Berilgan maqsad holatga o'tish mumkin bo'lgan manba holatlar ro'yxati.</summary>
        public static PaymentIntentStatus[] SourcesFor(PaymentIntentStatus to)
            => Allowed.Where(kv => kv.Value.Contains(to)).Select(kv => kv.Key).ToArray();

        public static bool IsTerminal(PaymentIntentStatus status)
            => status is PaymentIntentStatus.Settled
                or PaymentIntentStatus.Refunded
                or PaymentIntentStatus.Cancelled
                or PaymentIntentStatus.Expired;
    }
}
