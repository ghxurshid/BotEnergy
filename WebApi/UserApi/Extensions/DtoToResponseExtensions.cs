using Domain.Dtos.Process;
using Domain.Dtos.Session;
using UserApi.Models.Responses;

namespace UserApi.Extensions
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

        public static CloseSessionResponse ToResponse(this CloseSessionResultDto dto)
            => new CloseSessionResponse
            {
                Message = dto.ResultMessage,
                TotalDelivered = dto.TotalDelivered,
                TotalCost = dto.TotalCost
            };

        public static StartProcessResponse ToResponse(this StartProcessResultDto dto)
            => new StartProcessResponse
            {
                ProcessId = dto.ProcessId,
                ProductId = dto.ProductId,
                ProductName = dto.ProductName,
                Unit = dto.Unit,
                PricePerUnit = dto.PricePerUnit,
                LimitAmount = dto.LimitAmount,
                DeviceSerialNumber = dto.DeviceSerialNumber,
                Message = dto.ResultMessage
            };

        public static ProcessControlResponse ToResponse(this ProcessControlResultDto dto)
            => new ProcessControlResponse
            {
                ProcessId = dto.ProcessId,
                Status = dto.Status,
                Message = dto.ResultMessage
            };
    }
}
