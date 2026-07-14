namespace Domain.Enums
{
    /// <summary>
    /// Foydalanuvchi sessiyasining hayot davri.
    /// Created → Connected → InProcess → Closed.
    /// Paused — device offline (sessiya yopilmaydi, qayta ulanishda avto-resume).
    /// Settling — yopilish so'raldi, hold invoice'lar moliyaviy yakunlanmoqda; tugagach Closed.
    /// </summary>
    public enum SessionStatus
    {
        Created = 0,
        Connected = 1,
        InProcess = 2,
        Closed = 3,
        Paused = 4,
        Settling = 5
    }
}
