namespace AdminApi.Models.Requests
{
    public class RegisterClientRequest
    {
        public string PhoneNumber { get; set; }
        public string Inn { get; set; }
        public string BankAccount { get; set; }
    }
}
