using Domain.Entities;

namespace Domain.Repositories
{
    /// <summary>
    /// Saqlangan karta tokenlari. Token merchant kassasiga bog'langan — barcha o'qishlar
    /// (user, merchant) juftligi bo'yicha.
    /// </summary>
    public interface ICustomerCardRepository
    {
        Task<CustomerCardEntity> CreateAsync(CustomerCardEntity card);

        Task<CustomerCardEntity?> GetByIdAsync(long id);

        /// <summary>Foydalanuvchining shu merchantdagi kartalari (asosiysi birinchi).</summary>
        Task<List<CustomerCardEntity>> GetForUserAsync(long userId, long? merchantId = null);

        /// <summary>Intent yaratishda ishlatiladigan karta: tasdiqlangan va asosiy (yoki eng yangisi).</summary>
        Task<CustomerCardEntity?> GetUsableAsync(long userId, long merchantId);

        /// <summary>Takroriy tokenlashda mavjud yozuvni topadi (Payme bir karta uchun bir token qaytaradi).</summary>
        Task<CustomerCardEntity?> GetByTokenAsync(long merchantId, string token);

        /// <summary>Bittasini asosiy qiladi, qolganlarini bekor qiladi — bitta SQL tranzaksiyada.</summary>
        Task<bool> SetDefaultAsync(long userId, long merchantId, long cardId);

        Task UpdateAsync(CustomerCardEntity card);

        /// <summary>Soft delete.</summary>
        Task<bool> DeleteAsync(long id);
    }
}
