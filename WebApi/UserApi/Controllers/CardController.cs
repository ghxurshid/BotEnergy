using CommonConfiguration.Attributes;
using CommonConfiguration.Extensions;
using Domain.Dtos.Payment;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Permissions = Domain.Constants.Permissions;

namespace UserApi.Controllers
{
    /// <summary>
    /// Saqlangan kartalar — `Subscribe` to'lov usuli shular bilan ishlaydi: server kartadan
    /// pul ushlaydi (hold) va mijoz Payme ilovasiga umuman o'tmaydi.
    ///
    /// **Oqim:** `Add` (karta raqami + muddati → provider token qaytaradi, SMS kod ketadi)
    /// → `Verify` (kod) → karta to'lovga tayyor. Birinchi tasdiqlangan karta avtomatik asosiy bo'ladi.
    ///
    /// **MUHIM:** token merchant kassasiga bog'langan — kartani har bir merchant uchun alohida
    /// qo'shish kerak. `merchantId` ni mobil ilova sessiya/stansiya ma'lumotidan oladi.
    /// PAN va CVV serverda saqlanmaydi; javoblarda faqat maskalangan raqam qaytadi.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class CardController : ControllerBase
    {
        private readonly ICustomerCardService _cards;

        public CardController(ICustomerCardService cards)
            => _cards = cards;

        /// <summary>Karta qo'shish — provider token yaratadi va tasdiqlash kodini yuboradi.</summary>
        /// <response code="200">Karta qo'shildi (kod yuborilgan bo'lishi mumkin)</response>
        /// <response code="400">Karta raqami yoki amal muddati noto'g'ri</response>
        /// <response code="402">Provider kartani rad etdi</response>
        /// <response code="409">Merchant Payme kassasi sozlanmagan</response>
        /// <response code="502">Provider bilan bog'lanishda xatolik</response>
        [HttpPost]
        [RequirePermission(Permissions.PaymentCardAdd)]
        [ProducesResponseType(typeof(AddCardResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Add([FromBody] AddCardRequest request, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _cards.AddAsync(new AddCardDto
            {
                UserId = userId,
                MerchantId = request.MerchantId,
                Number = request.Number,
                Expire = request.Expire
            }, ct);

            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>SMS kod bilan kartani tasdiqlash — shundan keyin to'lovga ishlatiladi.</summary>
        [HttpPost]
        [RequirePermission(Permissions.PaymentCardVerify)]
        [ProducesResponseType(typeof(CardItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Verify([FromBody] VerifyCardRequest request, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _cards.VerifyAsync(new VerifyCardDto
            {
                UserId = userId,
                CardId = request.CardId,
                Code = request.Code
            }, ct);

            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Tasdiqlash kodini qayta yuborish.</summary>
        [HttpPost("{cardId}")]
        [RequirePermission(Permissions.PaymentCardVerify)]
        [ProducesResponseType(typeof(AddCardResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResendCode(long cardId, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _cards.ResendCodeAsync(cardId, userId, ct);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Kartalarim. `merchantId` berilsa — faqat o'sha merchant uchun saqlanganlari.</summary>
        [HttpGet]
        [RequirePermission(Permissions.PaymentCardList)]
        [ProducesResponseType(typeof(List<CardItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> My([FromQuery] long? merchantId = null)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _cards.GetMyAsync(userId, merchantId);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>
        /// Karta qo'shish mumkin bo'lgan merchantlar (kassalar) ro'yxati.
        /// Ilova sessiya ochmasdan turib ham karta qo'sha olishi uchun kerak:
        /// ro'yxatda bitta merchant bo'lsa ilova uni avtomatik tanlaydi.
        /// </summary>
        /// <response code="200">Payme kassasi sozlangan faol merchantlar</response>
        [HttpGet]
        [RequirePermission(Permissions.PaymentCardList)]
        [ProducesResponseType(typeof(List<CardMerchantDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Merchants()
        {
            var result = await _cards.GetMerchantsAsync();
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Shu merchant uchun asosiy kartani belgilash.</summary>
        [HttpPost("{cardId}")]
        [RequirePermission(Permissions.PaymentCardSetDefault)]
        [ProducesResponseType(typeof(CardResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> SetDefault(long cardId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _cards.SetDefaultAsync(cardId, userId);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        /// <summary>Kartani o'chirish — token provider tomonda ham bekor qilinadi.</summary>
        [HttpDelete("{cardId}")]
        [RequirePermission(Permissions.PaymentCardDelete)]
        [ProducesResponseType(typeof(CardResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(long cardId, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var result = await _cards.DeleteAsync(cardId, userId, ct);
            return result.IsSuccess ? Ok(result.Result) : result.ToErrorResponse();
        }

        private bool TryGetUserId(out long userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out userId);
        }
    }

    /// <summary>Karta qo'shish so'rovi. Raqam va muddat serverda SAQLANMAYDI.</summary>
    public class AddCardRequest
    {
        /// <summary>Token qaysi merchant kassasida yaratilsin (stansiya egasining merchanti).</summary>
        public long MerchantId { get; set; }

        /// <summary>16 xonali karta raqami.</summary>
        public string Number { get; set; } = string.Empty;

        /// <summary>Amal muddati MMYY.</summary>
        public string Expire { get; set; } = string.Empty;
    }

    public class VerifyCardRequest
    {
        public long CardId { get; set; }

        /// <summary>Telefonga kelgan SMS kod.</summary>
        public string Code { get; set; } = string.Empty;
    }
}
