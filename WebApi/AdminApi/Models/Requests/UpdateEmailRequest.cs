namespace AdminApi.Models.Requests
{
    /// <summary>Joriy user o'z emailini yangilash uchun.</summary>
    public class UpdateEmailRequest
    {
        public string Mail { get; set; } = string.Empty;
    }
}
