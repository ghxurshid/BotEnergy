using CommonConfiguration.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace PaymentApi.Controllers
{
    /// <summary>
    /// Payme to'lov tizimi integratsiyasi (webhook).
    /// Payme serveridan keladigan so'rovlarni qabul qilish uchun.
    ///
    /// **Hozirgi holat:** Implementatsiya qilinmagan.
    /// Kelajakda Payme Merchant API (CheckPerformTransaction, CreateTransaction, PerformTransaction, CancelTransaction) qo'shiladi.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [SkipPermissionCheck]
    public class PaymeController : ControllerBase
    {
    }
}
