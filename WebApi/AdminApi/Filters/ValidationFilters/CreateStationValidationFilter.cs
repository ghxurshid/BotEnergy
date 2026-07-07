using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    public class CreateStationValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as CreateStationRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (string.IsNullOrWhiteSpace(request.Name))
            { context.Result = new BadRequestObjectResult(new { message = "Stansiya nomi kiritilishi shart." }); return; }

            if (string.IsNullOrWhiteSpace(request.Address))
            { context.Result = new BadRequestObjectResult(new { message = "Stansiya manzili kiritilishi shart." }); return; }

            if (request.MerchantId <= 0)
            { context.Result = new BadRequestObjectResult(new { message = "Merchant ID kiritilishi shart." }); return; }

            if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            { context.Result = new BadRequestObjectResult(new { message = "Koordinata (kenglik va uzunlik) kiritilishi shart." }); return; }

            var error = StationCoordinateValidation.Validate(request.Latitude.Value, request.Longitude.Value);
            if (error is not null)
            { context.Result = new BadRequestObjectResult(new { message = error }); return; }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
