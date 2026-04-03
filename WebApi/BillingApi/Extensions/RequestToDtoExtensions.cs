using BillingApi.Models.Requests;
using Domain.Dtos;

namespace BillingApi.Extensions
{
    public static class RequestToDtoExtensions
    {
        public static TopUpBalanceDto ToDto(this TopUpBalanceRequest request)
            => new TopUpBalanceDto
            {
                UserId = request.UserId,
                Amount = request.Amount
            };
    }
}
