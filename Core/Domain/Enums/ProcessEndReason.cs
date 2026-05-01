namespace Domain.Enums
{
    /// <summary>
    /// Mahsulot berish jarayonining tugash sababi.
    /// </summary>
    public enum ProcessEndReason
    {
        Completed = 0,
        UserStopped = 1,
        DeviceError = 2,
        OutOfResource = 3
    }
}
