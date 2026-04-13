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
    }
}
