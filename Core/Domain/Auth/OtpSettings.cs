namespace Domain.Auth
{
    /// <summary>
    /// OTP sozlamalari. Config: Otp:AllowTestCode, Otp:TtlMinutes, Otp:MaxAttempts.
    /// AllowTestCode faqat Development'da true bo'lishi kerak — "123456" universal kodini yoqadi.
    /// </summary>
    public sealed class OtpSettings
    {
        public bool AllowTestCode { get; init; }
        public int TtlMinutes { get; init; } = 3;
        public int MaxAttempts { get; init; } = 5;
    }
}
