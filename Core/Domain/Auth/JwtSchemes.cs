namespace Domain.Auth
{
    /// <summary>
    /// Nomlangan autentifikatsiya sxemalari.
    ///
    /// Default <c>Bearer</c> sxemasi har API'da faqat o'sha API qabul qiladigan
    /// audience'ni tekshiradi (<see cref="JwtAudiences"/>). Ayrim endpointlar esa
    /// ikkala guruhga ham ochiq bo'lishi kerak — masalan SessionApi'ning SignalR hub'i:
    /// REST sirti customer-only bo'lib qoladi, lekin qurilma statusini kuzatish uchun
    /// admin/inkassator ilovalari (Platform tokeni) ham ulanadi. Shu holat uchun
    /// qo'shimcha sxema ro'yxatdan o'tkaziladi va faqat kerakli endpointda
    /// <c>[Authorize(AuthenticationSchemes = "Bearer,PlatformBearer")]</c> bilan yoqiladi.
    /// </summary>
    public static class JwtSchemes
    {
        /// <summary>Platform (Manage/Merchant) tokenini qabul qiluvchi qo'shimcha sxema.</summary>
        public const string Platform = "PlatformBearer";
    }
}
