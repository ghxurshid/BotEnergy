namespace Domain.Interfaces.Bank
{
    public enum CardPayoutFailureKind
    {
        None = 0,

        /// <summary>Bank so'rovni ko'rib chiqib rad etdi (karta yaroqsiz, bloklangan, limit).
        /// Qayta urinish foydasiz — bu yakuniy javob.</summary>
        Rejected = 1,

        HttpError = 2,
        Timeout = 3,
        Network = 4,
        Deserialization = 5
    }

    /// <summary>
    /// Bank chaqiruvining natijasi. <see cref="Payme.PaymeApiCall{T}"/> bilan bir xil ruh:
    /// klient HECH QACHON exception otmaydi, tarmoq xatosi ham shu wrapper ichida qaytadi —
    /// chunki chaqiruvchi har bir natijani sessiya holatiga yozadi.
    /// </summary>
    public class CardPayoutCall<T> where T : class
    {
        public bool IsSuccess { get; init; }
        public T? Result { get; init; }

        public CardPayoutFailureKind FailureKind { get; init; } = CardPayoutFailureKind.None;

        /// <summary>Bank tomonidagi xato kodi (rad etilganda).</summary>
        public string? ErrorCode { get; init; }

        public string? FailureMessage { get; init; }

        /// <summary>
        /// Watcher shu bayroqqa qarab qaror qiladi: transport xatolari qaytariladi,
        /// bankning rad javobi esa yakuniy — qayta urinilmaydi.
        /// </summary>
        public bool IsRetryable => FailureKind is
            CardPayoutFailureKind.HttpError or
            CardPayoutFailureKind.Timeout or
            CardPayoutFailureKind.Network;

        public static CardPayoutCall<T> Success(T result)
            => new() { IsSuccess = true, Result = result };

        public static CardPayoutCall<T> Rejected(string errorCode, string message)
            => new()
            {
                IsSuccess = false,
                FailureKind = CardPayoutFailureKind.Rejected,
                ErrorCode = errorCode,
                FailureMessage = message
            };

        public static CardPayoutCall<T> Transport(CardPayoutFailureKind kind, string message)
            => new()
            {
                IsSuccess = false,
                FailureKind = kind,
                FailureMessage = message
            };
    }
}
