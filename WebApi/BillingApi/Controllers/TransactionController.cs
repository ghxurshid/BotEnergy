using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BillingApi.Controllers
{
    /// <summary>
    /// Tranzaksiyalar tarixi (kelajakda implementatsiya qilinadi).
    /// Foydalanuvchi va admin uchun to'lov va yechim tranzaksiyalari ro'yxati.
    ///
    /// **Rejalashtirilgan imkoniyatlar:**
    /// - Foydalanuvchi o'z tranzaksiyalar tarixini ko'rish
    /// - Admin barcha tranzaksiyalarni ko'rish, filtrlash
    /// - Tranzaksiya turlari: balans to'ldirish, sessiya uchun yechim, qaytarish
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionController : ControllerBase
    {
    }
}
