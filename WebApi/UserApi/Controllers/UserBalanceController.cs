using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserApi.Controllers
{
    /// <summary>
    /// Foydalanuvchi balansi (kelajakda implementatsiya qilinadi).
    /// Hozircha endpointlar qo'shilmagan — balans operatsiyalari BillingApi orqali amalga oshiriladi.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserBalanceController : ControllerBase
    {
    }
}
