namespace Domain.Interfaces.Payme
{
    /// <summary>Payme chaqiruv natijasining retry-siyosati bo'yicha tasnifi.</summary>
    public enum PaymeCallOutcome
    {
        Success = 0,

        /// <summary>Vaqtinchalik xato (tarmoq/timeout/5xx) — backoff bilan qayta uriniladi.</summary>
        Transient = 1,

        /// <summary>Qaytarilmas xato — retry foydasiz, Failed + operator.</summary>
        Permanent = 2,

        /// <summary>
        /// "Allaqachon bajarilgan" (masalan, allaqachon confirm/cancel qilingan chek holati) —
        /// retry-safety uchun muvaffaqiyat deb qabul qilinadi (idempotent success).
        /// </summary>
        AlreadyDone = 3
    }

    /// <summary>
    /// PaymeApiCall natijasini retry qarori uchun tasniflaydi. Watcher shu asosda
    /// backoff/Failed/idempotent-success tanlaydi.
    /// </summary>
    public static class PaymeErrorClassifier
    {
        /// <param name="expectedAlreadyDoneState">
        /// ReceiptStateMismatch(-31630) kelganda receipts.check bilan tekshiriladigan holat —
        /// masalan capture uchun Paid(4), refund uchun Cancelled(50). Chek shu holatda bo'lsa AlreadyDone.
        /// Bu tekshiruvni chaqiruvchi bajaradi; bu metod faqat StateMismatch'ni alohida ajratadi.
        /// </param>
        public static PaymeCallOutcome Classify<T>(PaymeApiCall<T> call) where T : class
        {
            if (call.IsSuccess)
                return PaymeCallOutcome.Success;

            return call.FailureKind switch
            {
                PaymeFailureKind.Network => PaymeCallOutcome.Transient,
                PaymeFailureKind.Timeout => PaymeCallOutcome.Transient,
                PaymeFailureKind.HttpError => PaymeCallOutcome.Transient,
                PaymeFailureKind.Deserialization => PaymeCallOutcome.Transient,
                PaymeFailureKind.PaymeError => ClassifyPaymeError(call.Error?.Code ?? 0),
                _ => PaymeCallOutcome.Permanent
            };
        }

        private static PaymeCallOutcome ClassifyPaymeError(int code) => code switch
        {
            // Holat mos emas — chek allaqachon confirm/cancel qilingan bo'lishi mumkin.
            // Chaqiruvchi receipts.check bilan aniqlaydi: mos terminal holat bo'lsa AlreadyDone.
            PaymeErrorCodes.ReceiptStateMismatch => PaymeCallOutcome.AlreadyDone,

            PaymeErrorCodes.NotEnoughPrivileges => PaymeCallOutcome.Permanent,
            PaymeErrorCodes.ReceiptNotFound => PaymeCallOutcome.Permanent,
            PaymeErrorCodes.AccountNotFound => PaymeCallOutcome.Permanent,
            PaymeErrorCodes.AmountOutOfRange => PaymeCallOutcome.Permanent,

            // JSON-RPC standart server xatolari (-32xxx oralig'i) — vaqtinchalik deb qaraymiz.
            <= -32000 and > -33000 => PaymeCallOutcome.Transient,

            _ => PaymeCallOutcome.Permanent
        };
    }
}
