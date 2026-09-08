using Domain.Dtos.Base;
using Domain.Dtos.Payment;

namespace Domain.Interfaces
{
    /// <summary>
    /// Saqlangan kartalar (Payme Subscribe API tokenlari) — Subscribe to'lov usuli shular
    /// bilan mijozdan pul ushlaydi, mijoz Payme ilovasiga o'tmaydi.
    ///
    /// Token MERCHANT KASSASIGA bog'langan: bir karta har bir merchant uchun alohida
    /// tokenlanadi, shuning uchun barcha amallar merchant kontekstida bajariladi.
    /// PAN/CVV serverda saqlanmaydi, token API javoblarida hech qachon qaytarilmaydi.
    /// </summary>
    public interface ICustomerCardService
    {
        /// <summary>cards.create + cards.get_verify_code — karta qo'shiladi va SMS kod yuboriladi.</summary>
        Task<GenericDto<AddCardResultDto>> AddAsync(AddCardDto dto, CancellationToken ct = default);

        /// <summary>cards.verify — SMS kod bilan tasdiqlash. Shundan keyin karta to'lovga yaroqli.</summary>
        Task<GenericDto<CardItemDto>> VerifyAsync(VerifyCardDto dto, CancellationToken ct = default);

        /// <summary>Tasdiqlash kodini qayta yuborish.</summary>
        Task<GenericDto<AddCardResultDto>> ResendCodeAsync(long cardId, long userId, CancellationToken ct = default);

        /// <summary>Foydalanuvchi kartalari (merchant bo'yicha filtr — mobil ilova stansiya merchantini beradi).</summary>
        Task<GenericDto<List<CardItemDto>>> GetMyAsync(long userId, long? merchantId = null);

        /// <summary>Shu merchant uchun asosiy kartani belgilash.</summary>
        Task<GenericDto<CardResultDto>> SetDefaultAsync(long cardId, long userId);

        /// <summary>cards.remove + soft delete. Provider xatosi bo'lsa ham lokal yozuv o'chiriladi.</summary>
        Task<GenericDto<CardResultDto>> DeleteAsync(long cardId, long userId, CancellationToken ct = default);
    }
}
