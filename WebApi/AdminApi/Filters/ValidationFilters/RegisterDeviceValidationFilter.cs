using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    public class RegisterDeviceValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as RegisterDeviceRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.SerialNumber))
            { context.Result = new BadRequestObjectResult(new { message = "Seriya raqam kiritilishi shart." }); return; }

            if (request.SerialNumber.Length > 100)
            { context.Result = new BadRequestObjectResult(new { message = "Seriya raqam 100 ta belgidan oshmasligi kerak." }); return; }

            if (request.StationId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Stansiya ID kiritilishi shart." }); return; }

            if (request.FunctionCount < 1)
            { context.Result = new BadRequestObjectResult(new { message = "Funksiya soni kamida 1 bo'lishi kerak." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
