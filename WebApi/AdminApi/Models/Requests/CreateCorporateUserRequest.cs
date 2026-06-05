namespace AdminApi.Models.Requests
{
    /// <summary>Corporate (tashkilot) foydalanuvchi yaratish so'rovi.</summary>
    public class CreateCorporateUserRequest
    {
        public string PhoneId { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public long RoleId { get; set; }
        public long OrganizationId { get; set; }
    }
}
