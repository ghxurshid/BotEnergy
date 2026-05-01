namespace Domain.Enums
{
    /// <summary>
    /// Sessiya yopilish sababi.
    /// </summary>
    public enum SessionCloseReason
    {
        UserClosed = 0,
        Timeout = 1,
        DeviceLost = 2
    }
}
