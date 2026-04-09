using Domain.Dtos.Session;
using UsageSessionApi.Models.Requests;

namespace UsageSessionApi.Extensions
{
    public static class RequestToDtoExtensions
    {
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

        public static DeviceConnectedDto ToDto(this DeviceConnectRequest request)
            => new DeviceConnectedDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber
            };

        public static SessionProgressDto ToDto(this DeviceProgressRequest request)
            => new SessionProgressDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                Quantity = request.Quantity
            };

        public static DeviceFinishDto ToDto(this DeviceFinishRequest request)
            => new DeviceFinishDto
            {
                SessionToken = request.SessionToken,
                SerialNumber = request.SerialNumber,
                FinalQuantity = request.FinalQuantity,
                EndReason = request.EndReason
            };
    }
}
