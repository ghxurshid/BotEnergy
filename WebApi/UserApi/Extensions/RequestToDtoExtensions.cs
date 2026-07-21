using Domain.Dtos;
using UserApi.Models.Requests;

namespace UserApi.Extensions
{
    /// <summary>
    /// UserApi (Customer-audience) corporate-admin so'rovlarini servis DTO'lariga o'giradi.
    /// OrganizationId har doim caller scope'idan uzatiladi — so'rov tanasidan emas.
    /// </summary>
    public static class RequestToDtoExtensions
    {
        public static CreateCorporateUserDto ToDto(this CreateOrgUserRequest request, long organizationId)
            => new CreateCorporateUserDto
            {
                PhoneId = request.PhoneId,
                Mail = request.Mail,
                PhoneNumber = request.PhoneNumber,
                RoleId = request.RoleId,
                OrganizationId = organizationId
            };

        public static SetPasswordAdminDto ToDto(this SetOrgUserPasswordRequest request, long userId)
            => new SetPasswordAdminDto
            {
                UserId = userId,
                Password = request.Password,
                CurrentPassword = request.CurrentPassword
            };

        public static ResetPasswordAdminDto ToDto(this ResetOrgUserPasswordRequest request, long userId)
            => new ResetPasswordAdminDto
            {
                UserId = userId,
                NewPassword = request.NewPassword,
                CurrentPassword = request.CurrentPassword
            };

        public static CreateRoleDto ToDto(this CreateOrgRoleRequest request)
            => new CreateRoleDto
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = request.IsActive ?? true,
                // OrganizationId servisda scope'dan olinadi (Corporate caller uchun majburiy emas).
                OrganizationId = null,
                PermissionIds = request.PermissionIds
            };

        public static UpdateRoleDto ToDto(this UpdateOrgRoleRequest request)
            => new UpdateRoleDto
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = request.IsActive,
                PermissionIds = request.PermissionIds
            };
    }
}
