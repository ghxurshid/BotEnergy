using Domain.Enums;

namespace Domain.Helpers
{
    /// <summary>
    /// Hold invoice statuslari uchun YAGONA ruxsat jadvali. Har qanday status o'zgarishi
    /// faqat <c>IHoldInvoiceRepository.TryTransitionAsync</c> orqali va shu jadvalga muvofiq bo'ladi —
    /// Refunded→Hold, Captured→Hold kabi noto'g'ri o'tishlar hech qachon mumkin emas.
    /// </summary>
    public static class HoldInvoiceStateMachine
    {
        private static readonly IReadOnlyDictionary<HoldInvoiceStatus, HoldInvoiceStatus[]> Allowed =
            new Dictionary<HoldInvoiceStatus, HoldInvoiceStatus[]>
            {
                [HoldInvoiceStatus.Created] = new[]
                {
                    HoldInvoiceStatus.WaitingForConfirmation,
                    HoldInvoiceStatus.Cancelled,
                    HoldInvoiceStatus.Expired,
                    HoldInvoiceStatus.Failed
                },
                [HoldInvoiceStatus.WaitingForConfirmation] = new[]
                {
                    HoldInvoiceStatus.Hold,
                    // Settlement paytida to'lanmagan yoki poyga holatidagi receiptni watcher
                    // receipts.cancel bilan bekor qiladi (paid bo'lib qolgan bo'lsa pul qaytadi).
                    HoldInvoiceStatus.RefundPending,
                    HoldInvoiceStatus.Cancelled,
                    HoldInvoiceStatus.Expired,
                    HoldInvoiceStatus.Failed
                },
                [HoldInvoiceStatus.Hold] = new[]
                {
                    HoldInvoiceStatus.PartiallyConsumed,
                    HoldInvoiceStatus.FullyConsumed,
                    HoldInvoiceStatus.RefundPending,
                    HoldInvoiceStatus.Cancelled,
                    HoldInvoiceStatus.Failed
                },
                [HoldInvoiceStatus.PartiallyConsumed] = new[]
                {
                    HoldInvoiceStatus.FullyConsumed,
                    HoldInvoiceStatus.CapturePending,
                    HoldInvoiceStatus.Failed
                },
                [HoldInvoiceStatus.FullyConsumed] = new[]
                {
                    HoldInvoiceStatus.CapturePending,
                    HoldInvoiceStatus.Failed
                },
                [HoldInvoiceStatus.CapturePending] = new[]
                {
                    HoldInvoiceStatus.Captured,
                    HoldInvoiceStatus.Failed
                },
                [HoldInvoiceStatus.RefundPending] = new[]
                {
                    HoldInvoiceStatus.Refunded,
                    HoldInvoiceStatus.Failed,
                    // Auto-correct: refund navbatida turganda consumed>0 aniqlansa
                    // (cancel/consume poygasi) watcher capture'ga o'tkazadi — mablag' yo'qolmaydi.
                    HoldInvoiceStatus.CapturePending
                },
                // Failed'dan chiqish faqat operator retry orqali (maqsad holatga qaytarish).
                [HoldInvoiceStatus.Failed] = new[]
                {
                    HoldInvoiceStatus.CapturePending,
                    HoldInvoiceStatus.RefundPending
                },
                // Terminal holatlar
                [HoldInvoiceStatus.Captured] = Array.Empty<HoldInvoiceStatus>(),
                [HoldInvoiceStatus.Refunded] = Array.Empty<HoldInvoiceStatus>(),
                [HoldInvoiceStatus.Cancelled] = Array.Empty<HoldInvoiceStatus>(),
                [HoldInvoiceStatus.Expired] = Array.Empty<HoldInvoiceStatus>()
            };

        public static bool CanTransition(HoldInvoiceStatus from, HoldInvoiceStatus to)
            => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

        /// <summary>Berilgan maqsad holatga o'tish mumkin bo'lgan manba holatlar ro'yxati.</summary>
        public static HoldInvoiceStatus[] SourcesFor(HoldInvoiceStatus to)
            => Allowed.Where(kv => kv.Value.Contains(to)).Select(kv => kv.Key).ToArray();

        public static bool IsTerminal(HoldInvoiceStatus status)
            => status is HoldInvoiceStatus.Captured
                or HoldInvoiceStatus.Refunded
                or HoldInvoiceStatus.Cancelled
                or HoldInvoiceStatus.Expired;
    }
}
