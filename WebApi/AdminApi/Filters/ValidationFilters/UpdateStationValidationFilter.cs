using AdminApi.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AdminApi.Filters.ValidationFilters
{
    /// <summary>
    /// Update partial — faqat yuborilgan maydonlar tekshiriladi.
    /// Koordinata majburiy bo'lgani uchun tozalab bo'lmaydi: agar yangilansa,
    /// kenglik va uzunlik BIRGA yuborilishi va diapazonda bo'lishi shart.
    /// </summary>
    public class UpdateStationValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.ActionArguments["request"] as UpdateStationRequest;
            if (request is null) { context.Result = new BadRequestObjectResult(new { message = "So'rov ma'lumotlari noto'g'ri." }); return; }

            if (request.Address is not null && string.IsNullOrWhiteSpace(request.Address))
            { context.Result = new BadRequestObjectResult(new { message = "Stansiya manzili bo'sh bo'lishi mumkin emas." }); return; }

            var hasLat = request.Latitude.HasValue;
            var hasLng = request.Longitude.HasValue;

            if (hasLat != hasLng)
            { context.Result = new BadRequestObjectResult(new { message = "Koordinatani yangilash uchun kenglik va uzunlik birga yuborilishi kerak." }); return; }

            if (hasLat && hasLng)
            {
                var error = StationCoordinateValidation.Validate(request.Latitude!.Value, request.Longitude!.Value);
                if (error is not null)
                { context.Result = new BadRequestObjectResult(new { message = error }); return; }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
