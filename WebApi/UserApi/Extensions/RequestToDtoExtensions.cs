using Domain.Dtos;
using Domain.Dtos.Process;
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

        public static CloseSessionDto ToDto(this CloseSessionRequest request, long userId)
            => new CloseSessionDto
            {
                SessionId = request.SessionId,
                UserId = userId
            };

        public static StartProcessDto ToDto(this StartProcessRequest request, long userId)
            => new StartProcessDto
            {
                SessionId = request.SessionId,
                UserId = userId,
                ProductId = request.ProductId,
                RequestedAmount = request.RequestedAmount
            };

        public static ProcessControlDto ToControlDto(long processId, long userId)
            => new ProcessControlDto
            {
                ProcessId = processId,
                UserId = userId
            };
    }
}
