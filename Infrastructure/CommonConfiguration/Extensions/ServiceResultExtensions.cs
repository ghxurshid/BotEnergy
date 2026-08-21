using Domain.Dtos.Base;
using Microsoft.AspNetCore.Mvc;

namespace CommonConfiguration.Extensions
{
    /// <summary>
    /// Servis natijasini HTTP javobga o'girishning yagona nuqtasi.
    ///
    /// Nega kerak: to'sqinlik omilining <c>reason</c> kodi (masalan <c>DEVICE_OFFLINE</c>)
    /// javobga tushishi shart — mobil ilova va admin panel matnni tahlil qilmasdan, kod
    /// bo'yicha to'g'ri ekranni ko'rsatadi. Har controllerda qo'lda <c>new { message = ... }</c>
    /// yozilsa, kod ba'zi joyda tushib qolardi.
    ///
    /// Javob shakli barcha API'da bir xil:
    /// <code>{ "success": false, "message": "...", "reason": "DEVICE_OFFLINE" }</code>
    /// <c>reason</c> katalogga kirmagan eski xatolarda <c>null</c> bo'ladi.
    /// </summary>
    public static class ServiceResultExtensions
    {
        /// <summary>Muvaffaqiyatsiz natijani status kodi + sabab kodi bilan javobga o'giradi.</summary>
        public static IActionResult ToErrorResponse<T>(this GenericDto<T> result)
        {
            var error = result.ErrorObj;

            return new ObjectResult(new
            {
                success = false,
                message = error?.ErrorMessage ?? "Noma'lum xatolik.",
                reason = error?.Reason
            })
            {
                StatusCode = error?.Code ?? 500
            };
        }

        /// <summary>
        /// Odatiy "muvaffaqiyat bo'lsa 200, aks holda to'siq javobi" naqshi —
        /// controller'da bitta qatorga siqiladi.
        /// </summary>
        public static IActionResult ToActionResult<T>(this GenericDto<T> result)
            => result.IsSuccess
                ? new OkObjectResult(result.Result)
                : result.ToErrorResponse();
    }
}
