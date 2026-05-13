namespace DeviceApi.Services
{
    /// <summary>
    /// Connect oqimining barcha mumkin bo'lgan natijalari uchun kodlar (machine-readable).
    /// Qurilma displeyida UI tanlash uchun ishlatiladi (har koddan ma'lum lokalizatsiya).
    /// </summary>
    public static class ConnectResultCodes
    {
        public const string Success = "SUCCESS";
        public const string InvalidPayload = "INVALID_PAYLOAD";
        public const string NoPendingSession = "NO_PENDING_SESSION";
        public const string TokenMismatch = "TOKEN_MISMATCH";
        public const string DeviceUnknown = "DEVICE_UNKNOWN";
        public const string ActiveSessionExists = "ACTIVE_SESSION_EXISTS";
        public const string PendingServiceUnavailable = "PENDING_SERVICE_UNAVAILABLE";
        public const string InternalError = "INTERNAL_ERROR";
    }

    public static class ConnectResultMessages
    {
        public static string For(string code) => code switch
        {
            ConnectResultCodes.Success => "Sessiya muvaffaqiyatli yaratildi va qurilmaga biriktirildi.",
            ConnectResultCodes.InvalidPayload => "Payload yaroqsiz: user_id yoki session_token bo'sh.",
            ConnectResultCodes.NoPendingSession => "Bu foydalanuvchi uchun pending sessiya topilmadi (yaratilmagan yoki TTL tugagan).",
            ConnectResultCodes.TokenMismatch => "QR tokeni mos kelmadi.",
            ConnectResultCodes.DeviceUnknown => "Qurilma serial raqami tizimda topilmadi yoki faol emas.",
            ConnectResultCodes.ActiveSessionExists => "Foydalanuvchida allaqachon faol sessiya bor — avval uni yopish kerak.",
            ConnectResultCodes.PendingServiceUnavailable => "UserApi gRPC servisi javob bermayapti.",
            ConnectResultCodes.InternalError => "Server ichki xatosi.",
            _ => "Noma'lum xatolik."
        };
    }
}
