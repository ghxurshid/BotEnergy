namespace UserApi.Models.Responses
{
    public class CloseSessionResponse
    {
        public string ResultMessage { get; set; } = string.Empty;
        public decimal TotalDelivered { get; set; }
    }
}
