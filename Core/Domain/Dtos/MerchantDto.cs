namespace Domain.Dtos
{
    public class CreateMerchantDto
    {
        public required string PhoneNumber { get; set; }
        public required string Inn { get; set; }
        public required string BankAccount { get; set; }
        public required string CompanyName { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateMerchantDto
    {
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
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

        // Payme — kalit hech qachon ochiq qaytarilmaydi (faqat masked).
        public string? PaymeCashboxId { get; set; }
        public string? PaymeKeyMasked { get; set; }
        public bool PaymeEnabled { get; set; }
    }

    public class SetPaymeCredentialsDto
    {
        public required string CashboxId { get; set; }
        public required string Key { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class MerchantResultDto
    {
        public long Id { get; set; }
        public required string ResultMessage { get; set; }
    }
}
