namespace Domain.Dtos
{
    public class CreateMerchantDto
    {
        public required string PhoneNumber { get; set; }
        public required string Inn { get; set; }
        public required string BankAccount { get; set; }
        public required string CompanyName { get; set; }
    }

    public class UpdateMerchantDto
    {
        public string? PhoneNumber { get; set; }
        public string? BankAccount { get; set; }
        public string? CompanyName { get; set; }
    }

    public class MerchantItemDto
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Inn { get; set; } = string.Empty;
        public string BankAccount { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MerchantResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
