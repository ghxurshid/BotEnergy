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
                IsActive = request.IsActive ?? true,
                PermissionIds = request.PermissionIds
            };

        public static UpdateRoleDto ToDto(this UpdateRoleRequest request)
            => new UpdateRoleDto
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = request.IsActive
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

        public static CreateProductDto ToDto(this CreateProductRequest request)
            => new CreateProductDto
            {
                Name = request.Name,
                Description = request.Description,
                ProductType = request.ProductType,
                Unit = request.Unit,
                Price = request.Price,
                DeviceId = request.DeviceId,
                IsActive = request.IsActive ?? true
            };

        public static UpdateProductDto ToDto(this UpdateProductRequest request)
            => new UpdateProductDto
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                IsActive = request.IsActive
            };

        public static CreateOrganizationDto ToDto(this CreateOrganizationRequest request)
            => new CreateOrganizationDto
            {
                Name = request.Name,
                Inn = request.Inn,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                Balance = request.Balance ?? 0,
                IsActive = request.IsActive ?? true
            };

        public static UpdateOrganizationDto ToDto(this UpdateOrganizationRequest request)
            => new UpdateOrganizationDto
            {
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                IsActive = request.IsActive
            };

        public static CreateStationDto ToDto(this CreateStationRequest request)
            => new CreateStationDto
            {
                Name = request.Name,
                Location = request.Location,
                OrganizationId = request.OrganizationId
            };

        public static UpdateStationDto ToDto(this UpdateStationRequest request)
            => new UpdateStationDto
            {
                Name = request.Name,
                Location = request.Location,
                IsActive = request.IsActive
            };

        public static RegisterDeviceDto ToDto(this RegisterDeviceRequest request)
            => new RegisterDeviceDto
            {
                SerialNumber = request.SerialNumber,
                DeviceType = request.DeviceType,
                StationId = request.StationId,
                Model = request.Model,
                FirmwareVersion = request.FirmwareVersion,
                IsOnline = request.IsOnline ?? false,
                IsActive = request.IsActive ?? true
            };

        public static UpdateDeviceDto ToDto(this UpdateDeviceRequest request)
            => new UpdateDeviceDto
            {
                Model = request.Model,
                FirmwareVersion = request.FirmwareVersion,
                IsOnline = request.IsOnline,
                IsActive = request.IsActive
            };

        public static CreateMerchantDto ToDto(this RegisterMerchantRequest request)
            => new CreateMerchantDto
            {
                PhoneNumber = request.PhoneNumber,
                Inn = request.Inn,
                BankAccount = request.BankAccount,
                CompanyName = request.CompanyName,
                IsActive = request.IsActive ?? true
            };

        public static UpdateMerchantDto ToDto(this UpdateMerchantRequest request)
            => new UpdateMerchantDto
            {
                PhoneNumber = request.PhoneNumber
            };

        public static CreateUserAdminDto ToDto(this CreateUserRequest request)
            => new CreateUserAdminDto
            {
                PhoneId = request.PhoneId,
                Mail = request.Mail,
                PhoneNumber = request.PhoneNumber,
                RoleId = request.RoleId,
                OrganizationId = request.OrganizationId,
                StationId = request.StationId
            };

        public static SetPasswordAdminDto ToDto(this SetPasswordRequest request, long userId)
            => new SetPasswordAdminDto
            {
                UserId = userId,
                Password = request.Password
            };

        public static ResetPasswordAdminDto ToDto(this ResetPasswordRequest request, long userId)
            => new ResetPasswordAdminDto
            {
                UserId = userId,
                NewPassword = request.NewPassword
            };

        public static TopUpBalanceDto ToDto(this TopUpBalanceRequest request)
            => new TopUpBalanceDto
            {
                UserId = request.UserId,
                Amount = request.Amount
            };
    }
}
