using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    /// <summary>
    /// Rol uchun umumiy maydonlar. Mapped bo'lmagan baza — har konkret rol entity
    /// (<see cref="PlatformRoleEntity"/>, <see cref="CustomerRoleEntity"/>) o'z jadvaliga map qilinadi.
    /// </summary>
    public abstract class RoleBase : Entity
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
