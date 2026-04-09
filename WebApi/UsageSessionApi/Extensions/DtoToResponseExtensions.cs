using Domain.Dtos.Session;
using UsageSessionApi.Models.Responses;

namespace UsageSessionApi.Extensions
{
    public static class DtoToResponseExtensions
    {
        public static CreateSessionResponse ToResponse(this CreateSessionResultDto dto)
            => new CreateSessionResponse
            {
                SessionId = dto.SessionId,
                SessionToken = dto.SessionToken,
                ExpiresAt = dto.ExpiresAt,
                Message = dto.ResultMessage
            };

        public static DeviceConnectResponse ToResponse(this DeviceConnectedResultDto dto)
            => new DeviceConnectResponse
            {
                SessionId = dto.SessionId,
                ProductId = dto.ProductId,
                ProductName = dto.ProductName,
                Unit = dto.Unit,
                PricePerUnit = dto.PricePerUnit,
                DeviceSerialNumber = dto.DeviceSerialNumber,
                Message = dto.ResultMessage
            };

        public static SetQuantityResponse ToResponse(this SetQuantityResultDto dto)
            => new SetQuantityResponse
            {
                LimitQuantity = dto.LimitQuantity,
                ProductName = dto.ProductName,
                Unit = dto.Unit,
                PricePerUnit = dto.PricePerUnit,
                Message = dto.ResultMessage
            };

        public static CloseSessionResponse ToResponse(this CloseSessionResultDto dto)
            => new CloseSessionResponse
            {
                Message = dto.ResultMessage,
                TotalDelivered = dto.TotalDelivered
            };

        public static DeviceProgressResponse ToResponse(this SessionProgressResultDto dto)
            => new DeviceProgressResponse { Message = dto.ResultMessage };

        public static DeviceFinishResponse ToResponse(this DeviceFinishResultDto dto)
            => new DeviceFinishResponse
            {
                TotalDelivered = dto.TotalDelivered,
                Message = dto.ResultMessage
            };
    }
}
