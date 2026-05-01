namespace Domain.Dtos.Process
{
    /// <summary>
    /// Process Stop/Pause/Resume uchun umumiy DTO.
    /// </summary>
    public class ProcessControlDto
    {
        public long ProcessId { get; set; }
        public long UserId { get; set; }
    }

    public class ProcessControlResultDto
    {
        public long ProcessId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ResultMessage { get; set; } = string.Empty;
    }
}
