namespace SessionApi.Mqtt.Handlers
{
    /// <summary>
    /// Naqd → karta oqimining natija kodlari (machine-readable).
    /// Qurilma shu kodga qarab ekranda mos xabarni ko'rsatadi —
    /// matn emas, kod bo'yicha tanlaydi.
    /// </summary>
    public static class CashResultCodes
    {
        public const string Success = "SUCCESS";
        public const string InvalidPayload = "INVALID_PAYLOAD";

        /// <summary>Karta raqami yaroqsiz yoki bank rad etdi.</summary>
        public const string CardRejected = "CARD_REJECTED";

        /// <summary>Bank javob bermadi — mijoz keyinroq urinib ko'rishi mumkin.</summary>
        public const string BankUnavailable = "BANK_UNAVAILABLE";

        public const string SessionNotFound = "SESSION_NOT_FOUND";

        /// <summary>Sessiya bu amal uchun mos holatda emas (masalan allaqachon yakunlangan).</summary>
        public const string InvalidState = "INVALID_STATE";

        /// <summary>Qurilmada tugallanmagan naqd sessiya bor.</summary>
        public const string SessionExists = "SESSION_EXISTS";

        /// <summary>Kupyura nominali qabul qilinmaydi yoki limit oshdi.</summary>
        public const string BillRejected = "BILL_REJECTED";

        /// <summary>Payout yiqildi, qayta urinilmoqda — yakuniy natija keyin push qilinadi.</summary>
        public const string PayoutPending = "PAYOUT_PENDING";

        /// <summary>Payout yakuniy yiqildi — operator aralashuvi kerak.</summary>
        public const string PayoutFailed = "PAYOUT_FAILED";

        public const string InternalError = "INTERNAL_ERROR";

        /// <summary>
        /// Servis qatlamidan kelgan HTTP-uslubidagi kodni MQTT kodiga o'giradi.
        /// Servis <c>GenericDto</c> bilan ishlaydi (REST bilan umumiy), qurilma esa
        /// matn emas, kod kutadi.
        /// </summary>
        public static string FromHttpCode(int code) => code switch
        {
            400 => InvalidPayload,
            404 => SessionNotFound,
            409 => InvalidState,
            422 => CardRejected,
            _ => InternalError
        };
    }
}
