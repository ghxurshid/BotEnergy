using Domain.Enums;

namespace Domain.Entities
{
    public class MerchantUserEntity : UserEntity
    {
        public override UserType UserType => UserType.MerchantPerson;

        public long StationId { get; set; }
        public StationEntity? Station { get; set; }
    }
}
