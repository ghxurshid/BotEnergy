using Domain.Enums;

namespace Domain.Entities
{
    public class NaturalUserEntity : UserEntity
    {
        public override UserType UserType => UserType.NaturalPerson;

        public decimal Balance { get; set; } = 0;
    }
}
