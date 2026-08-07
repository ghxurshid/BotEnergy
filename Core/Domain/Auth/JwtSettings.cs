namespace Domain.Auth
{
    /// <summary>
    /// JWT imzolash sozlamalari. Bitta manba — token yaratish (TokenService) va
    /// tekshirish (AddJwtAuthentication) aynan shu qiymatlardan foydalanadi,
    /// shuning uchun config'dagi Jwt:Secret o'zgarsa ikkalasi birga o'zgaradi.
    /// Qiymat CommonConfiguration'da config'dan (Jwt:Secret, env: Jwt__Secret) o'qib DI'ga beriladi.
    /// </summary>
    public sealed class JwtSettings
    {
        public required string Secret { get; init; }
        public int AccessTokenMinutes { get; init; } = 15;

        /// <summary>
        /// Token'ga yoziladigan <c>iss</c> claim'i (config: Jwt:Issuer). Bo'sh bo'lsa yozilmaydi.
        /// Tekshirish tomoni (AddJwtAuthentication) Jwt:ValidateIssuer true bo'lsa aynan shu
        /// qiymatni kutadi — ikkalasi bitta konfiguratsiyadan oziqlanadi.
        /// </summary>
        public string? Issuer { get; init; }
    }
}
