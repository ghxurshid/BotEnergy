using Domain.Entities.BaseEntity;

namespace Domain.Entities
{
    public class UserRoleEntity : Entity
    {
        public required long UserId { get; set; }
        public UserEntity? User { get; set; }
        public required long RoleId { get; set; }
        public RoleEntity? Role { get; set; }  
    }
}