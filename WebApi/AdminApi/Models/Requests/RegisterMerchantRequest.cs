namespace AdminApi.Models.Requests
{
    public class RegisterMerchantRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Inn { get; set; } = string.Empty;
        public string BankAccount { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
    }
}
