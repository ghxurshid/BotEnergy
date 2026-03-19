using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models.Requests
{
    public class RegisterRequest
    {
        public required string PhoneId { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Mail { get; set; }
        public required string Password { get; set; }
    }
}
