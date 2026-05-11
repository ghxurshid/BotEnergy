namespace Domain.Interfaces.Payme
{
    public enum PaymeFailureKind
    {
        None = 0,
        PaymeError = 1,
        HttpError = 2,
        Timeout = 3,
        Network = 4,
        Deserialization = 5
    }

    /// <summary>
    /// Payme API chaqiruvining natijasi — barcha holatda audit uchun raw payload'lar bilan.
    /// PaymentService har bir chaqiruv uchun shu wrapper'dan PaymentTransactionStep yozadi.
    /// </summary>
    public class PaymeApiCall<T> where T : class
    {
        public bool IsSuccess { get; init; }
        public T? Result { get; init; }
        public PaymeError? Error { get; init; }
        public PaymeFailureKind FailureKind { get; init; } = PaymeFailureKind.None;

        /// <summary>Audit'da saqlash uchun — Payme'ga yuborilgan request body (auth header'larsiz, faqat JSON-RPC tanasi).</summary>
        public string RequestBody { get; init; } = string.Empty;

        /// <summary>Audit'da saqlash uchun — Payme'dan kelgan xom javob.</summary>
        public string ResponseBody { get; init; } = string.Empty;

        /// <summary>Tarmoq xatosi yoki HTTP-status xabari (Payme'ning JSON error'idan tashqari).</summary>
        public string? FailureMessage { get; init; }

        public static PaymeApiCall<T> Success(T result, string requestBody, string responseBody) =>
            new() { IsSuccess = true, Result = result, RequestBody = requestBody, ResponseBody = responseBody };

        public static PaymeApiCall<T> FromPaymeError(PaymeError error, string requestBody, string responseBody) =>
            new()
            {
                IsSuccess = false,
                Error = error,
                FailureKind = PaymeFailureKind.PaymeError,
                FailureMessage = error.Message,
                RequestBody = requestBody,
                ResponseBody = responseBody
            };

        public static PaymeApiCall<T> Failure(
            PaymeFailureKind kind,
            string failureMessage,
            string requestBody,
            string responseBody) =>
            new()
            {
                IsSuccess = false,
                FailureKind = kind,
                FailureMessage = failureMessage,
                RequestBody = requestBody,
                ResponseBody = responseBody
            };
    }
}
