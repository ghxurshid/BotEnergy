using Domain.Dtos.Session;
using UserApi.Models.Requests;

namespace UserApi.Extensions
{
    public static class SessionRequestToDtoExtensions
    {
        public static CloseSessionDto ToDto(this CloseSessionRequest request, long userId)
        {
            return new CloseSessionDto
            {
                SessionId = request.SessionId,
                UserId = userId
            };
        }
    }
}
