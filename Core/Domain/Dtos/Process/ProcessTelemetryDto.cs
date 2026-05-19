namespace Domain.Dtos.Process
{
    /// <summary>
    /// Qurilmadan kelgan telemetry — har 5 sekundda jarayon davomida.
    /// <see cref="TotalGiven"/> — qurilma jarayon boshidan beri **jami bergan miqdor (cumulative)**.
    /// Server bu qiymatni `GivenAmount` ga to'g'ridan-to'g'ri o'rnatadi (delta emas).
    /// </summary>
    public class ProcessTelemetryDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public decimal TotalGiven { get; set; }
        public long Sequence { get; set; }
    }

    public class ProcessTelemetryResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
    }
}
