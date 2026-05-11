namespace Domain.Enums
{
    public enum PaymentStepType
    {
        Initiated = 0,
        Validated = 1,
        ReceiptCreateRequested = 2,
        ReceiptCreated = 3,
        PayRequested = 4,
        PayResponded = 5,
        BalanceCredited = 6,
        NotifiedClient = 7,
        Failed = 8,
        Reversed = 9
    }
}
