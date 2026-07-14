namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Bitta Payme kassa credential'lari. Hold invoice oqimida har doim device egasi
    /// bo'lgan merchant'niki ishlatiladi; null berilsa global PaymeOptions (default kassa) qo'llanadi.
    /// </summary>
    public sealed record PaymeCredentials(string CashboxId, string Key);
}
