using AdminApi.Models.Requests;
using Domain.Dtos;

namespace AdminApi.Extensions
{
    public static class RequestToDtoExtensions
    {
        public static CreateRoleDto ToDto(this CreateRoleRequest request)
            => new CreateRoleDto
            {
                Name = request.Name,
                Description = request.Description,
                OrganizationId = request.OrganizationId
            };

        public static AddPermissionDto ToDto(this AddPermissionRequest request)
            => new AddPermissionDto
            {
                RoleId = request.RoleId,
                Permission = request.Permission
            };

        public static RemovePermissionDto ToDto(this RemovePermissionRequest request)
            => new RemovePermissionDto
            {
                RoleId = request.RoleId,
                Permission = request.Permission
            };

        public static AssignRoleDto ToDto(this AssignRoleRequest request)
            => new AssignRoleDto
            {
                PhoneNumber = request.PhoneNumber,
                RoleId = request.RoleId
            };
    }
}
