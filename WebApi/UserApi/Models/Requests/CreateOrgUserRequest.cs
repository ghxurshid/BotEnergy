namespace UserApi.Models.Requests
{
    /// <summary>
    /// Corporate admin o'z tashkilotiga yangi foydalanuvchi qo'shadi.
    /// OrganizationId so'rovda emas — token scope'idan olinadi.
    /// </summary>
    public class CreateOrgUserRequest
    {
        public string PhoneId { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public long RoleId { get; set; }
    }
}
