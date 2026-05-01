using Domain.Enums;

namespace Domain.Dtos.Process
{
    /// <summary>
    /// Qurilma o'zining tomonidan jarayonni tugatishi yoki to'xtatish javobi.
    /// MQTT da `device/{serial}/event` (stopped/error/out_of_resource) yoki
    /// `device/{serial}/response` (Stop/Pause/Resume buyrug'iga ack) topiclari orqali keladi.
    /// </summary>
    public class DeviceProcessReportDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public decimal FinalQuantity { get; set; }
        public ProcessEndReason EndReason { get; set; }
    }

    public class DeviceProcessReportResultDto
    {
        public string ResultMessage { get; set; } = string.Empty;
        public decimal TotalDelivered { get; set; }
        public decimal TotalCost { get; set; }
    }
}
