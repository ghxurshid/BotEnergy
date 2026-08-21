using Domain.Guards;

namespace Domain.Dtos.Base
{
    public class Error
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public int Code { get; set; }

        /// <summary>
        /// To'sqinlik omilining mashina o'qiydigan kodi (<see cref="StopFactor.Code"/>) —
        /// masalan <c>DEVICE_OFFLINE</c>. Mobil ilova va qurilma matnga emas, shu kodga qarab
        /// o'z ekranini tanlaydi. Eski (kodsiz) xatolarda <c>null</c>.
        /// </summary>
        public string? Reason { get; set; }
    }

    public class GenericDto<TResult>
    {
        public bool IsSuccess { get; set; }
        public TResult? Result { get; set; }
        public Error? ErrorObj { get; set; }

        public static GenericDto<TResult> Success(TResult? result)
        {
            return new GenericDto<TResult> { IsSuccess = true, Result = result };
        }

        public static GenericDto<TResult> Error(int errorCode, string errorMessage)
        {
            return new GenericDto<TResult> { IsSuccess = false, ErrorObj = new Error { Code = errorCode, ErrorMessage = errorMessage } };
        }

        /// <summary>
        /// Amal to'sqinlik omili tufayli bajarilmadi. Status ham, matn ham, kod ham
        /// <see cref="StopFactor"/> dan olinadi — chaqiruvchi joyda takrorlanmaydi.
        /// </summary>
        public static GenericDto<TResult> Blocked(StopFactor factor)
        {
            return new GenericDto<TResult>
            {
                IsSuccess = false,
                ErrorObj = new Error
                {
                    Code = factor.HttpStatus,
                    ErrorMessage = factor.Message,
                    Reason = factor.Code
                }
            };
        }

        /// <summary>Boshqa <c>GenericDto</c> xatosini turini o'zgartirib uzatish (kodni yo'qotmasdan).</summary>
        public static GenericDto<TResult> FromError<TOther>(GenericDto<TOther> other)
        {
            return new GenericDto<TResult>
            {
                IsSuccess = false,
                ErrorObj = new Error
                {
                    Code = other.ErrorObj?.Code ?? 500,
                    ErrorMessage = other.ErrorObj?.ErrorMessage ?? "Noma'lum xatolik.",
                    Reason = other.ErrorObj?.Reason
                }
            };
        }
    }
}
