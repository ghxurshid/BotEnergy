using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    public class CreateProductValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as CreateProductRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.Name))
            { context.Result = new BadRequestObjectResult(new { message = "Mahsulot nomi kiritilishi shart." }); return; }

            if (request.Price <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Narx 0 dan katta bo'lishi kerak." }); return; }

            if (request.DeviceId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Qurilma ID kiritilishi shart." }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
