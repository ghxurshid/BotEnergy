using Domain.Constants;
using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _repo;
        private readonly IStationRepository _stationRepo;
        private readonly IUserRepository _userRepo;

        public DeviceService(IDeviceRepository repo, IStationRepository stationRepo, IUserRepository userRepo)
        {
            _repo = repo;
            _stationRepo = stationRepo;
            _userRepo = userRepo;
        }

        public async Task<GenericDto<DeviceResultDto>> RegisterAsync(RegisterDeviceDto dto, long callerId, HashSet<string> callerPermissions)
        {
            var station = await _stationRepo.GetByIdAsync(dto.StationId);
            if (station is null)
                return GenericDto<DeviceResultDto>.Error(404, "Stansiya topilmadi.");

            var accessCheck = await CheckStationAccessAsync(callerId, callerPermissions, station);
            if (accessCheck is not null)
                return accessCheck;

            var existing = await _repo.GetBySerialNumberAsync(dto.SerialNumber);
            if (existing is not null)
                return GenericDto<DeviceResultDto>.Error(409, $"'{dto.SerialNumber}' seriya raqamli qurilma allaqachon mavjud.");

            var device = new DeviceEntity
            {
                SerialNumber = dto.SerialNumber,
                DeviceType = dto.DeviceType,
                StationId = dto.StationId,
                Model = dto.Model,
                FirmwareVersion = dto.FirmwareVersion,
                IsOnline = dto.IsOnline,
                IsActive = dto.IsActive
            };

            var created = await _repo.CreateAsync(device);

            return GenericDto<DeviceResultDto>.Success(new DeviceResultDto
            {
                Id = created.Id,
                ResultMessage = "Qurilma muvaffaqiyatli ro'yxatdan o'tkazildi."
            });
        }

        public async Task<GenericDto<List<DeviceItemDto>>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return GenericDto<List<DeviceItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<List<DeviceItemDto>>> GetByStationAsync(long stationId)
        {
            var list = await _repo.GetByStationIdAsync(stationId);
            return GenericDto<List<DeviceItemDto>>.Success(list.Select(ToItem).ToList());
        }

        public async Task<GenericDto<DeviceItemDto>> GetByIdAsync(long id)
        {
            var device = await _repo.GetByIdAsync(id);
            if (device is null)
                return GenericDto<DeviceItemDto>.Error(404, "Qurilma topilmadi.");

            return GenericDto<DeviceItemDto>.Success(ToItem(device));
        }

        public async Task<GenericDto<DeviceResultDto>> UpdateAsync(long id, UpdateDeviceDto dto)
        {
            var device = await _repo.GetByIdAsync(id);
            if (device is null)
                return GenericDto<DeviceResultDto>.Error(404, "Qurilma topilmadi.");

            if (!string.IsNullOrWhiteSpace(dto.Model)) device.Model = dto.Model;
            if (!string.IsNullOrWhiteSpace(dto.FirmwareVersion)) device.FirmwareVersion = dto.FirmwareVersion;
            if (dto.IsOnline.HasValue) device.IsOnline = dto.IsOnline.Value;
            if (dto.IsActive.HasValue) device.IsActive = dto.IsActive.Value;

            await _repo.UpdateAsync(device);

            return GenericDto<DeviceResultDto>.Success(new DeviceResultDto
            {
                Id = device.Id,
                ResultMessage = "Qurilma ma'lumotlari yangilandi."
            });
        }

        public async Task<GenericDto<DeviceResultDto>> DeleteAsync(long id)
        {
            var device = await _repo.GetByIdAsync(id);
            if (device is null)
                return GenericDto<DeviceResultDto>.Error(404, "Qurilma topilmadi.");

            await _repo.DeleteAsync(id);

            return GenericDto<DeviceResultDto>.Success(new DeviceResultDto
            {
                Id = id,
                ResultMessage = "Qurilma o'chirildi."
            });
        }

        private async Task<GenericDto<DeviceResultDto>?> CheckStationAccessAsync(
            long callerId, HashSet<string> callerPermissions, StationEntity station)
        {
            if (callerPermissions.Contains(Permissions.MerchantAdminRegister))
                return null;

            var caller = await _userRepo.GetByIdAsync(callerId);
            if (caller is null)
                return GenericDto<DeviceResultDto>.Error(403, "Foydalanuvchi topilmadi.");

            if (caller is MerchantUserEntity merchantUser)
            {
                if (station.MerchantId is null)
                    return GenericDto<DeviceResultDto>.Error(403, "Bu stansiya merchantga tegishli emas.");

                var callerStation = await _stationRepo.GetByIdAsync(merchantUser.StationId);
                if (callerStation?.MerchantId != station.MerchantId)
                    return GenericDto<DeviceResultDto>.Error(403, "Bu stansiya sizning merchantingizga tegishli emas.");
            }
            else if (caller is LegalUserEntity legalUser)
            {
                if (legalUser.OrganizationId != station.OrganizationId)
                    return GenericDto<DeviceResultDto>.Error(403, "Bu stansiya sizning tashkilotingizga tegishli emas.");
            }

            return null;
        }

        private static DeviceItemDto ToItem(DeviceEntity d) => new()
        {
            Id = d.Id,
            SerialNumber = d.SerialNumber,
            DeviceType = d.DeviceType,
            Model = d.Model,
            FirmwareVersion = d.FirmwareVersion,
            StationId = d.StationId,
            StationName = d.Station?.Name ?? string.Empty,
            IsOnline = d.IsOnline,
            IsActive = d.IsActive,
            CreatedDate = d.CreatedDate
        };
    }
}
