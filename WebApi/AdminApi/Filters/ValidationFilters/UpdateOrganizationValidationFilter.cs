using AdminApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    /// <summary>Update'da telefon ixtiyoriy — faqat berilgan bo'lsa normalizatsiya + format tekshiruvi.</summary>
    public class UpdateOrganizationValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as UpdateOrganizationRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                if (!PhoneValidator.TryNormalize(request.PhoneNumber, out var phone))
                { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }
                request.PhoneNumber = phone;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
