namespace Domain.Enums
{
    /// <summary>
    /// Mahsulot berish jarayonining hayot davri (sessiya ichida).
    /// Started → InProcess → (Paused ↔ InProcess)* → Ended
    /// </summary>
    public enum ProcessStatus
    {
        Started = 0,
        InProcess = 1,
        Paused = 2,
        Ended = 3
    }
}
