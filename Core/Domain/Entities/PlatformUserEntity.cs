using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Platforma darajasidagi foydalanuvchi — eng yuqori prioritet.
    /// Scope cheklovi yo'q: permissioni bo'lsa, barcha merchant/organization/station
    /// ma'lumotlari ustida ishlaydi. Mobil <see cref="NaturalUserEntity"/> dan farqli ravishda
    /// bu admin/operator turidir.
    /// </summary>
    public class PlatformUserEntity : UserEntity
    {
        public override UserType UserType => UserType.Platform;
    }
}
