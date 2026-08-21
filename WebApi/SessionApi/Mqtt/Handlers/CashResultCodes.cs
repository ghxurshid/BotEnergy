using Domain.Dtos.Base;

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

        /// <summary>Box inkassatsiya uchun ochilgan — naqd qabul qilinmaydi.</summary>
        public const string BoxOpen = "BOX_OPEN";

        /// <summary>Qurilma yoki uning stansiyasi biznes jihatdan ishlamayapti.</summary>
        public const string DeviceUnavailable = "DEVICE_UNAVAILABLE";

        /// <summary>Payout yiqildi, qayta urinilmoqda — yakuniy natija keyin push qilinadi.</summary>
        public const string PayoutPending = "PAYOUT_PENDING";

        /// <summary>Payout yakuniy yiqildi — operator aralashuvi kerak.</summary>
        public const string PayoutFailed = "PAYOUT_FAILED";

        public const string InternalError = "INTERNAL_ERROR";

        /// <summary>
        /// Servis natijasidan qurilma tushunadigan kodni oladi.
        ///
        /// Birinchi navbatda <see cref="Error.Reason"/> — to'sqinlik omilining barqaror kodi
        /// (<c>DEVICE_OFFLINE</c>, <c>CASH_BOX_OPEN</c>, ...) ishlatiladi. Ilgari kod HTTP
        /// statusdan taxmin qilinardi ("409 bo'lsa demak sessiya bor"), bu esa yangi to'siq
        /// qo'shilganda noto'g'ri ekran ko'rsatardi. HTTP status endi faqat zaxira yo'l —
        /// katalogga kirmagan eski xatolar uchun.
        /// </summary>
        public static string FromResult<T>(GenericDto<T> result)
        {
            var error = result.ErrorObj;

            return error?.Reason switch
            {
                "CASH_SESSION_NOT_FOUND" => SessionNotFound,

                "CASH_SESSION_NOT_ACCEPTING" or "CASH_SESSION_FINISHED"
                    or "CASH_SESSION_HAS_MONEY" or "CASH_RETRY_NOT_NEEDED" => InvalidState,

                "DEVICE_HAS_OPEN_CASH_SESSION" => SessionExists,
                "CASH_BOX_OPEN" or "DEVICE_HAS_OPEN_COLLECTION" => BoxOpen,

                "CASH_DENOMINATION_REJECTED" or "CASH_LIMIT_EXCEEDED"
                    or "CASH_INVALID_BILL_SEQ" => BillRejected,

                "CASH_SESSION_EMPTY" => InvalidPayload,

                "CASH_CARD_INVALID" or "CASH_CARD_REJECTED" => CardRejected,
                "BANK_UNAVAILABLE" => BankUnavailable,

                "DEVICE_NOT_FOUND" or "DEVICE_INACTIVE" or "DEVICE_OFFLINE"
                    or "STATION_INACTIVE" or "MERCHANT_INACTIVE" => DeviceUnavailable,

                _ => FromHttpCode(error?.Code ?? 500)
            };
        }

        /// <summary>
        /// Zaxira yo'l: sabab kodi bo'lmagan (katalogga hali ko'chirilmagan) xatolar uchun
        /// HTTP-uslubidagi statusdan taxminiy MQTT kodi.
        /// </summary>
        public static string FromHttpCode(int code) => code switch
        {
            400 => InvalidPayload,
            404 => SessionNotFound,
            409 => InvalidState,
            422 => CardRejected,
            503 => DeviceUnavailable,
            _ => InternalError
        };
    }
}
