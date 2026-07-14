namespace Domain.Enums
{
    /// <summary>
    /// Sessiyaga bog'langan to'lov konteksti holati.
    /// Active → Settling (yopilish so'raldi, moliyaviy yakunlash ketmoqda) → Settled.
    /// </summary>
    public enum PaymentSessionStatus
    {
        Active = 0,
        Settling = 1,
        Settled = 2
    }
}
