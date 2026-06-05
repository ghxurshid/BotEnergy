using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Platforma orqali xizmat oluvchi foydalanuvchi (jadval: auth.customer_users).
    /// <see cref="CustomerUserType.Natural"/> — jismoniy shaxs, o'z <see cref="Balance"/>idan to'laydi.
    /// <see cref="CustomerUserType.Corporate"/> — tashkilot xodimi, tashkilot balansidan to'laydi.
    /// </summary>
    public class CustomerUserEntity : UserBase
    {
        public CustomerUserType Type { get; set; } = CustomerUserType.Natural;

        /// <summary>Faqat <see cref="CustomerUserType.Natural"/> uchun ishlatiladi.</summary>
        public decimal Balance { get; set; } = 0;

        /// <summary>Faqat <see cref="CustomerUserType.Corporate"/> uchun to'ldiriladi; balans
        /// shu tashkilotning <see cref="OrganizationEntity.Balance"/>idan olinadi.</summary>
        public long? OrganizationId { get; set; }
        public OrganizationEntity? Organization { get; set; }

        public CustomerRoleEntity? Role { get; set; }

        public ICollection<SessionEntity>? Sessions { get; set; }
    }
}
