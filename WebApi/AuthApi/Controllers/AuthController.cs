using AuthApi.Extensions;
using AuthApi.Filters.ValidationFilters;
using AuthApi.Models.Requests;
using CommonConfiguration.Attributes;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace UserApi.Controllers
{
    /// <summary>
    /// Autentifikatsiya va avtorizatsiya endpointlari.
    /// Ro'yxatdan o'tish, OTP tasdiqlash, parol o'rnatish, login va parol tiklash jarayonlarini boshqaradi.
    ///
    /// **Jarayon ketma-ketligi (yangi foydalanuvchi):**
    /// 1. Register → telefon raqam bilan ro'yxatdan o'tish, OTP kodi SMS orqali yuboriladi
    /// 2. Verify → OTP kodni tasdiqlash
    /// 3. SetPassword → parolni o'rnatish
    /// 4. Login → tizimga kirish (JWT token qaytariladi)
    ///
    /// **Parol tiklash jarayoni:**
    /// 1. ResetPasswordRequest → telefon raqamga OTP yuboriladi
    /// 2. ResetPasswordVerify → OTP kodni tasdiqlash
    /// 3. ResetPasswordSet → yangi parolni o'rnatish
    ///
    /// **Cheklovlar:**
    /// - OTP kodi 5 daqiqa amal qiladi
    /// - Login paytida noto'g'ri parol kiritilsa 401 qaytadi
    /// - Ro'yxatdan o'tgan lekin tasdiqlanmagan raqam qayta ro'yxatdan o'ta olmaydi
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [SkipPermissionCheck]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Yangi foydalanuvchi ro'yxatdan o'tkazish.
        /// Telefon raqamga OTP kod yuboriladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/Register
        ///     {
        ///         "phoneId": "device-uuid-12345",
        ///         "phoneNumber": "998901234567",
        ///         "mail": "user@example.com"
        ///     }
        ///
        /// **Xatoliklar:**
        /// - 409: Bu telefon raqam allaqachon ro'yxatdan o'tgan
        /// - 400: Telefon raqam formati noto'g'ri
        /// </remarks>
        /// <param name="request">Ro'yxatdan o'tish ma'lumotlari</param>
        /// <response code="200">Muvaffaqiyatli. OTP kod yuborildi</response>
        /// <response code="409">Telefon raqam allaqachon mavjud</response>
        [HttpPost]
        [TypeFilter(typeof(RegisterValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// OTP kodni tasdiqlash.
        /// Register yoki ResetPasswordRequest dan keyin yuborilgan SMS kodini tasdiqlaydi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/Verify
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "otpCode": "123456"
        ///     }
        ///
        /// **Xatoliklar:**
        /// - 400: OTP kod noto'g'ri yoki muddati o'tgan
        /// - 404: Foydalanuvchi topilmadi
        /// </remarks>
        /// <param name="request">Telefon raqam va OTP kod</param>
        /// <response code="200">OTP tasdiqlandi</response>
        /// <response code="400">Noto'g'ri yoki eskirgan OTP</response>
        [HttpPost]
        [TypeFilter(typeof(VerifyValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
        {
            var result = await _authService.VerifyAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Parol o'rnatish (birinchi marta).
        /// OTP tasdiqlangandan keyin chaqiriladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/SetPassword
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "password": "MyStr0ngP@ss"
        ///     }
        ///
        /// **Cheklovlar:**
        /// - OTP avval tasdiqlanishi shart
        /// - Parol kamida 6 belgidan iborat bo'lishi kerak
        /// </remarks>
        /// <param name="request">Telefon raqam va yangi parol</param>
        /// <response code="200">Parol muvaffaqiyatli o'rnatildi</response>
        /// <response code="400">OTP tasdiqlanmagan yoki parol talablarga mos emas</response>
        [HttpPost]
        [TypeFilter(typeof(SetPasswordValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            var result = await _authService.SetPasswordAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Tizimga kirish.
        /// Muvaffaqiyatli bo'lsa JWT access_token va refresh_token qaytariladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/Login
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "password": "MyStr0ngP@ss"
        ///     }
        ///
        /// **Javobda qaytadi:**
        /// - `access_token` — API so'rovlar uchun (muddati cheklangan)
        /// - `refresh_token` — tokenni yangilash uchun
        ///
        /// **Xatoliklar:**
        /// - 401: Noto'g'ri parol
        /// - 404: Foydalanuvchi topilmadi
        /// - 403: Foydalanuvchi bloklangan
        /// </remarks>
        /// <param name="request">Telefon raqam va parol</param>
        /// <response code="200">JWT tokenlar qaytarildi</response>
        /// <response code="401">Noto'g'ri parol</response>
        [HttpPost]
        [TypeFilter(typeof(LoginValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Access tokenni yangilash.
        /// Access token muddati tugaganda, refresh_token orqali yangi token olish.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/RefreshToken
        ///     {
        ///         "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
        ///     }
        ///
        /// **Xatoliklar:**
        /// - 401: Refresh token yaroqsiz yoki muddati o'tgan
        /// </remarks>
        /// <param name="request">Joriy refresh token</param>
        /// <response code="200">Yangi access_token va refresh_token qaytarildi</response>
        /// <response code="401">Yaroqsiz refresh token</response>
        [HttpPost]
        [TypeFilter(typeof(RefreshTokenValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Parol tiklash so'rovi.
        /// Telefon raqamga OTP kod yuboriladi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/ResetPasswordRequest
        ///     {
        ///         "phoneNumber": "998901234567"
        ///     }
        ///
        /// Keyingi qadam: ResetPasswordVerify → ResetPasswordSet
        /// </remarks>
        /// <param name="request">Telefon raqam</param>
        /// <response code="200">OTP kod yuborildi</response>
        /// <response code="404">Foydalanuvchi topilmadi</response>
        [HttpPost]
        [TypeFilter(typeof(ResetPasswordRequestValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetPasswordRequest([FromBody] ResetPasswordRequestRequest request)
        {
            var result = await _authService.ResetPasswordRequestAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Parol tiklash — OTP tasdiqlash.
        /// ResetPasswordRequest dan keyin kelgan kodni tasdiqlaydi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/ResetPasswordVerify
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "otpCode": "123456"
        ///     }
        /// </remarks>
        /// <param name="request">Telefon raqam va OTP kod</param>
        /// <response code="200">OTP tasdiqlandi, endi ResetPasswordSet chaqiring</response>
        /// <response code="400">Noto'g'ri OTP</response>
        [HttpPost]
        [TypeFilter(typeof(ResetPasswordVerifyValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPasswordVerify([FromBody] ResetPasswordVerifyRequest request)
        {
            var result = await _authService.ResetPasswordVerifyAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }

        /// <summary>
        /// Parol tiklash — yangi parol o'rnatish.
        /// OTP tasdiqlangandan keyin yangi parolni o'rnatadi.
        /// </summary>
        /// <remarks>
        /// Namuna so'rov:
        ///
        ///     POST /api/Auth/ResetPasswordSet
        ///     {
        ///         "phoneNumber": "998901234567",
        ///         "newPassword": "MyNewStr0ngP@ss"
        ///     }
        /// </remarks>
        /// <param name="request">Telefon raqam va yangi parol</param>
        /// <response code="200">Parol muvaffaqiyatli o'zgartirildi</response>
        /// <response code="400">OTP tasdiqlanmagan</response>
        [HttpPost]
        [TypeFilter(typeof(ResetPasswordSetValidationFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPasswordSet([FromBody] ResetPasswordSetRequest request)
        {
            var result = await _authService.ResetPasswordSetAsync(request.ToDto());
            if (!result.IsSuccess)
                return StatusCode(result.ErrorObj!.Code, new { message = result.ErrorObj.ErrorMessage });

            return Ok(result.Result!.ToResponse());
        }
    }
}
