namespace SessionApi.Mqtt.Abstractions
{
    /// <summary>
    /// Umumiy result kodlari — har handler o'ziga xos kodlarni o'z constants class'ida e'lon qiladi,
    /// lekin pipeline middleware'lari shu yerdagi standartdan foydalanadi.
    /// </summary>
    public static class MqttResultCodes
    {
        public const string Success = "SUCCESS";
        public const string InvalidPayload = "INVALID_PAYLOAD";
        public const string HmacInvalid = "HMAC_INVALID";
        public const string TimestampSkew = "TIMESTAMP_SKEW";
        public const string ReplayRejected = "REPLAY_REJECTED";
        public const string DeviceUnknown = "DEVICE_UNKNOWN";
        public const string UnknownType = "UNKNOWN_TYPE";
        public const string InternalError = "INTERNAL_ERROR";
    }
}
