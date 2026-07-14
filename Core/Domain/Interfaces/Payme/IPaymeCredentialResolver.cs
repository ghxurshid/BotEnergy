namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Merchant'ning Payme credential'larini topadi. Hold invoice oqimida har doim
    /// device egasi bo'lgan merchant'niki ishlatiladi — global PaymeOptions'ga fallback YO'Q
    /// (PaymeEnabled=false yoki credential to'liq bo'lmasa null qaytadi va amal rad etiladi).
    /// </summary>
    public interface IPaymeCredentialResolver
    {
        Task<PaymeCredentials?> ForMerchantAsync(long merchantId);
    }
}
