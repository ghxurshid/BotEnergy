namespace AdminApi.Models.Requests
{
    public class ReversePaymentRequest
    {
        /// <summary>Reverse sababi — audit step'iga yoziladi.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
