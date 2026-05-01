namespace Domain.Enums
{
    /// <summary>
    /// Foydalanuvchi sessiyasining hayot davri.
    /// Created → Connected → InProcess → Closed
    /// </summary>
    public enum SessionStatus
    {
        Created = 0,
        Connected = 1,
        InProcess = 2,
        Closed = 3
    }
}
