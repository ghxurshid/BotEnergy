namespace Domain.Enums
{
    /// <summary>
    /// Hold invoice audit trail qadamlari (append-only, <c>hold_invoice_steps</c>).
    /// </summary>
    public enum HoldInvoiceStepType
    {
        Initiated = 0,
        Validated = 1,
        ReceiptCreateRequested = 2,
        ReceiptCreated = 3,
        SendRequested = 4,
        CheckPolled = 5,
        Held = 6,
        ConsumeApplied = 7,
        SettlementTargetAssigned = 8,
        CaptureRequested = 9,
        CaptureResponded = 10,
        RefundRequested = 11,
        RefundResponded = 12,
        Cancelled = 13,
        Expired = 14,
        Failed = 15,
        OperatorAction = 16,
        Retry = 17
    }
}
