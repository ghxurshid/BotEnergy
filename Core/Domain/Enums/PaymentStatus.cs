namespace Domain.Enums
{
    public enum PaymentStatus
    {
        Pending = 0,
        ReceiptCreated = 1,
        Paying = 2,
        Succeeded = 3,
        Failed = 4,
        Cancelled = 5,
        Reversed = 6
    }
}
