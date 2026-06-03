using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Platforma (global) roli — scope FK'siz. Barcha permissionlarni o'z ichiga olishi mumkin.
    /// Faqat <see cref="PlatformUserEntity"/> ga biriktiriladi.
    /// </summary>
    public class PlatformRoleEntity : RoleEntity
    {
        public override RoleType RoleType => RoleType.PlatformRole;
    }
}
