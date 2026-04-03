using BillingApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BillingApi.Filters.ValidationFilters
{
    public class TopUpBalanceValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as TopUpBalanceRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (request.UserId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Foydalanuvchi ID kiritilishi shart." }); return; }

            if (request.Amount <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "To'ldirish miqdori 0 dan katta bo'lishi kerak." }); return; }

            if (request.Amount > 100_000_000)
            { context.Result = new BadRequestObjectResult(new { message = "Bir vaqtda to'ldirish miqdori 100,000,000 UZSdan oshmasligi kerak." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
