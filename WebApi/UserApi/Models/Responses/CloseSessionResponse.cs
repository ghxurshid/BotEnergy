namespace UserApi.Models.Responses
{
    public class CloseSessionResponse
    {
        public string Message { get; set; } = string.Empty;
        public decimal TotalDelivered { get; set; }
        public decimal TotalCost { get; set; }
    }
}
