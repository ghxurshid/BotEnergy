namespace Domain.Auth
{
    /// <summary>
    /// JWT audience qiymatlari — foydalanuvchi guruhi bo'yicha (API boshiga emas).
    /// Customer tokeni platform-only API'larda (AdminApi, BillingApi) o'tmaydi va aksincha.
    /// Bitta guruh bir nechta API'ga murojaat qilishi buzilmaydi (mobil ilova →
    /// UserApi/SessionApi/PaymentApi bitta Customer token bilan ishlayveradi).
    /// </summary>
    public static class JwtAudiences
    {
        public const string Customer = "botenergy:customer";
        public const string Platform = "botenergy:platform";

        public static readonly string[] All = { Customer, Platform };
    }
}
