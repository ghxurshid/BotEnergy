using Domain.Dtos;
using Domain.Dtos.Session;
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

        public static CreateSessionDto ToDto(this CreateSessionRequest request, long userId)
            => new CreateSessionDto
            {
                UserId = userId
            };

        public static SetQuantityDto ToDto(this SetQuantityRequest request, long userId)
            => new SetQuantityDto
            {
                SessionId = request.SessionId,
                UserId = userId,
                RequestedQuantity = request.RequestedQuantity
            };

        public static CloseSessionDto ToDto(this CloseSessionRequest request, long userId)
            => new CloseSessionDto
            {
                SessionId = request.SessionId,
                UserId = userId
            };
    }
}
