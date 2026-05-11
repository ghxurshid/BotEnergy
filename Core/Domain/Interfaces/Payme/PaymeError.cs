namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// JSON-RPC "error" obyekti: Payme API tomonidan qaytarilgan xatolik.
    /// </summary>
    public class PaymeError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Data { get; set; }
    }
}
