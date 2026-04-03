using AdminApi.Models.Requests;
using CommonConfiguration.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters
{
    public class RegisterClientValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as RegisterClientRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (!PhoneValidator.IsValid(request.PhoneNumber))
            { context.Result = new BadRequestObjectResult(new { message = PhoneValidator.ErrorMessage }); return; }

            if (string.IsNullOrWhiteSpace(request.Inn))
            { context.Result = new BadRequestObjectResult(new { message = "INN kiritilishi shart." }); return; }

            if (string.IsNullOrWhiteSpace(request.BankAccount))
            { context.Result = new BadRequestObjectResult(new { message = "Bank hisobi kiritilishi shart." }); return; }

            if (string.IsNullOrWhiteSpace(request.CompanyName))
            { context.Result = new BadRequestObjectResult(new { message = "Kompaniya nomi kiritilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
