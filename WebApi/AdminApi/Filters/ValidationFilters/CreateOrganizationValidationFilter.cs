using AdminApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    public class CreateOrganizationValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as CreateOrganizationRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.Name))
            { context.Result = new BadRequestObjectResult(new { message = "Tashkilot nomi kiritilishi shart." }); return; }

            if (request.Name.Length > 200)
            { context.Result = new BadRequestObjectResult(new { message = "Tashkilot nomi 200 ta belgidan oshmasligi kerak." }); return; }

            if (!string.IsNullOrEmpty(request.PhoneNumber) && !PhoneValidator.IsValid(request.PhoneNumber))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
