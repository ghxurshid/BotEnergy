namespace Domain.Dtos.Process
{
    /// <summary>
    /// Qurilmadan kelgan telemetry — har 5 sekundda jarayon davomida.
    /// </summary>
    public class ProcessTelemetryDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public decimal Quantity { get; set; }
        public long Sequence { get; set; }
    }

    public class ProcessTelemetryResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
    }
}
