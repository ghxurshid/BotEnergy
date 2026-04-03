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

        public static CreateProductDto ToDto(this CreateProductRequest request)
            => new CreateProductDto
            {
                Name = request.Name,
                Description = request.Description,
                ProductType = request.ProductType,
                Unit = request.Unit,
                Price = request.Price,
                DeviceId = request.DeviceId
            };

        public static CreateOrganizationDto ToDto(this CreateOrganizationRequest request)
            => new CreateOrganizationDto
            {
                Name = request.Name,
                Inn = request.Inn,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber
            };

        public static UpdateOrganizationDto ToDto(this UpdateOrganizationRequest request)
            => new UpdateOrganizationDto
            {
                Name = request.Name,
                Inn = request.Inn,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber
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
                FunctionCount = request.FunctionCount
            };

        public static UpdateDeviceDto ToDto(this UpdateDeviceRequest request)
            => new UpdateDeviceDto
            {
                Model = request.Model,
                FirmwareVersion = request.FirmwareVersion,
                IsActive = request.IsActive,
                StationId = request.StationId
            };

        public static CreateClientDto ToDto(this RegisterClientRequest request)
            => new CreateClientDto
            {
                PhoneNumber = request.PhoneNumber,
                Inn = request.Inn,
                BankAccount = request.BankAccount,
                CompanyName = request.CompanyName
            };

        public static UpdateClientDto ToDto(this UpdateClientRequest request)
            => new UpdateClientDto
            {
                PhoneNumber = request.PhoneNumber,
                BankAccount = request.BankAccount,
                CompanyName = request.CompanyName
            };

        public static TopUpBalanceDto ToDto(this TopUpBalanceRequest request)
            => new TopUpBalanceDto
            {
                UserId = request.UserId,
                Amount = request.Amount
            };
    }
}
