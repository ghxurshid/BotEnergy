using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Payments
{
    /// <summary>
    /// Payme Merchant API (provider BIZGA qo'ng'iroq qiladi) JSON-RPC so'rovi.
    /// Payme <c>jsonrpc</c> maydonini talab qilmaydi — <c>method</c> + <c>params</c> yetarli.
    /// </summary>
    public sealed class PaymeRpcRequest
    {
        /// <summary>So'rov identifikatori — javobda o'zgarishsiz qaytariladi (son yoki matn).</summary>
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement Params { get; set; }
    }

    /// <summary>Payme xato matni uch tilda bo'lishi shart.</summary>
    public sealed record PaymeRpcMessage(
        [property: JsonPropertyName("ru")] string Ru,
        [property: JsonPropertyName("uz")] string Uz,
        [property: JsonPropertyName("en")] string En);

    public sealed record PaymeRpcError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] PaymeRpcMessage Message,
        [property: JsonPropertyName("data")] string? Data = null);

    /// <summary>
    /// JSON-RPC javobi: <c>result</c> yoki <c>error</c> — ikkalasi birga bo'lmaydi.
    /// Serializatsiya PropertyNamingPolicy=null bilan bajariladi (Payme snake_case kalitlarni kutadi).
    /// </summary>
    public sealed class PaymeRpcResponse
    {
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PaymeRpcError? Error { get; set; }

        public static PaymeRpcResponse Ok(JsonElement id, object result)
            => new() { Id = NormalizeId(id), Result = result };

        public static PaymeRpcResponse Fail(JsonElement id, PaymeRpcError error)
            => new() { Id = NormalizeId(id), Error = error };

        /// <summary>
        /// So'rovda <c>id</c> bo'lmasa JsonElement "Undefined" bo'ladi va uni yozishga urinish
        /// istisno tashlaydi — buzuq so'rov butun javobni yiqitmasligi uchun null'ga aylantiramiz.
        /// </summary>
        private static JsonElement NormalizeId(JsonElement id)
            => id.ValueKind == JsonValueKind.Undefined ? NullId : id;

        private static readonly JsonElement NullId = JsonDocument.Parse("null").RootElement.Clone();
    }

    /// <summary>
    /// Payme Merchant API xato kodlari va uch tilli matnlari.
    /// Kodlar Payme spetsifikatsiyasidan — o'zgartirilmaydi.
    /// </summary>
    public static class PaymeRpcErrors
    {
        /// <summary>Autentifikatsiya/privilegiya xatosi.</summary>
        public static PaymeRpcError NotAllowed() => new(-32504, new PaymeRpcMessage(
            "Недостаточно привилегий", "Ruxsat yetarli emas", "Insufficient privileges"));

        public static PaymeRpcError MethodNotFound(string method) => new(-32601, new PaymeRpcMessage(
            "Метод не найден", "Metod topilmadi", "Method not found"), method);

        public static PaymeRpcError InvalidRequest() => new(-32600, new PaymeRpcMessage(
            "Неверный запрос", "So'rov noto'g'ri", "Invalid request"));

        /// <summary>Summa noto'g'ri.</summary>
        public static PaymeRpcError InvalidAmount() => new(-31001, new PaymeRpcMessage(
            "Неверная сумма", "Summa noto'g'ri", "Invalid amount"));

        /// <summary>Tranzaksiya topilmadi.</summary>
        public static PaymeRpcError TransactionNotFound() => new(-31003, new PaymeRpcMessage(
            "Транзакция не найдена", "Tranzaksiya topilmadi", "Transaction not found"));

        /// <summary>Buyurtma bajarilgan — bekor qilib bo'lmaydi.</summary>
        public static PaymeRpcError CannotCancel() => new(-31007, new PaymeRpcMessage(
            "Заказ выполнен, отмена невозможна", "Buyurtma bajarilgan, bekor qilib bo'lmaydi",
            "Order is delivered, cannot be cancelled"));

        /// <summary>Amalni bajarib bo'lmaydi (holat mos emas / muddat o'tgan).</summary>
        public static PaymeRpcError CannotPerform() => new(-31008, new PaymeRpcMessage(
            "Невозможно выполнить операцию", "Amalni bajarib bo'lmaydi", "Unable to perform operation"));

        /// <summary>Buyurtma topilmadi (account xatolari uchun ajratilgan -31050..-31099 oralig'i).</summary>
        public static PaymeRpcError OrderNotFound() => new(-31050, new PaymeRpcMessage(
            "Заказ не найден", "Buyurtma topilmadi", "Order not found"), "order_id");

        /// <summary>Buyurtma to'lovga yaroqli emas (allaqachon to'langan yoki yopilgan).</summary>
        public static PaymeRpcError OrderNotPayable() => new(-31051, new PaymeRpcMessage(
            "Заказ недоступен для оплаты", "Buyurtma to'lovga yaroqli emas", "Order is not payable"), "order_id");
    }

    /// <summary>Payme tranzaksiya holatlari (Merchant API).</summary>
    public static class PaymeTransactionStates
    {
        /// <summary>Yaratildi, to'lov kutilmoqda.</summary>
        public const int Created = 1;

        /// <summary>Bajarildi — pul yechildi.</summary>
        public const int Performed = 2;

        /// <summary>To'lovgacha bekor qilindi.</summary>
        public const int CancelledBeforePerform = -1;

        /// <summary>To'lovdan keyin bekor qilindi (pul qaytarildi).</summary>
        public const int CancelledAfterPerform = -2;
    }

    /// <summary>
    /// Payme Merchant API bo'yicha tranzaksiya shuncha vaqt ichida yakunlanishi kerak;
    /// oshsa merchant uni bekor qiladi (reason=4).
    /// </summary>
    public static class PaymeMerchantLimits
    {
        public static readonly TimeSpan TransactionTimeout = TimeSpan.FromHours(12);

        /// <summary>Muddat o'tgani sababli bekor qilish kodi.</summary>
        public const int TimeoutCancelReason = 4;
    }
}
