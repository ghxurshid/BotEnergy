using Domain.Enums;

namespace Domain.Entities
{
    public class MerchantRoleEntity : RoleEntity
    {
        public override RoleType RoleType => RoleType.MerchantRole;

        public long MerchantId { get; set; }
        public MerchantEntity? Merchant { get; set; }
    }
}
