namespace Domain.Dtos.Process
{
    /// <summary>
    /// Qurilma pauza buyrug'ini bajarib, oqimni to'liq to'xtatganini tasdiqlaydi.
    /// MQTT da `device/{serial}/event`, `type=process.paused` orqali keladi.
    /// <see cref="TotalGiven"/> — pauzagacha jami berilgan miqdor (inersiya bilan birga, cumulative).
    /// Process tugamaydi — foydalanuvchi keyin resume qilishi mumkin, shuning uchun balans yechilmaydi.
    /// </summary>
    public class DeviceProcessPausedDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public long ProcessId { get; set; }
        public decimal TotalGiven { get; set; }
    }
}
