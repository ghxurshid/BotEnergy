using Domain.Dtos.Process;
using Domain.Dtos.Session;
using SessionApi.Models.Requests;

namespace SessionApi.Extensions
{
    public static class RequestToDtoExtensions
    {
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
