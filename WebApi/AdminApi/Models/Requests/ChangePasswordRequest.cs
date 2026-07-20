namespace AdminApi.Models.Requests
{
    /// <summary>Joriy user o'z parolini o'zgartirish uchun — joriy parol tasdiqlanadi.</summary>
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
