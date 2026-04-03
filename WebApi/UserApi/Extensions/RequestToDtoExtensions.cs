using Domain.Dtos;
using UserApi.Models.Requests;

namespace UserApi.Extensions
{
    public static class RequestToDtoExtensions
    {
        public static UpdateUserDto ToDto(this UpdateMeRequest request)
            => new UpdateUserDto
            {
                Mail = request.Mail,
                PhoneId = request.PhoneId
            };
    }
}
