using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Inkassatsiya oqimi.
    ///
    /// <b>Faqat platforma xodimi (Platform/Manage).</b> Inkassator merchant tomonining
    /// odami bo'la olmaydi: pulni olib ketayotgan va o'sha pulning egasi bir tomon bo'lib
    /// qolsa, hisob-kitobni tekshiradigan mustaqil tomon qolmaydi. Shu cheklov ikki
    /// qatlamda: <c>PermissionScopes.ManageOnly</c> permissionni Merchant rolga
    /// biriktirishga yo'l qo'ymaydi, bu yerdagi tekshiruvlar esa permission xato
    /// biriktirilib qolgan holatda ham amalni bajarmaydi.
    ///
    /// Qoldiq faqat <see cref="ConfirmAsync"/> da nolga tushadi — boxning ochilgani
    /// pulning olinganini bildirmaydi (inkassator boxni ochib, pulni olmasdan ketishi mumkin).
    ///
    /// Qurilmaga buyruq <see cref="IDeviceCommandSender"/> orqali yuboriladi: bu servis
    /// AdminApi'da ham ishlaydi, u yerda esa MQTT ulanishi yo'q.
    /// </summary>
    public class IncassationService : IIncassationService
    {
        private const string NotManageMessage =
            "Inkassatsiya faqat platforma xodimlari uchun — merchant tomoni o'z qurilmasidan pul yig'a olmaydi.";

        private readonly ICashCollectionRepository _collectionRepo;
        private readonly ICashSessionRepository _cashSessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IDeviceCommandSender _commandSender;
        private readonly ITransactionRunner _transaction;
        private readonly ILogger<IncassationService> _logger;

        public IncassationService(
            ICashCollectionRepository collectionRepo,
            ICashSessionRepository cashSessionRepo,
            IDeviceRepository deviceRepo,
            IDeviceCommandSender commandSender,
            ITransactionRunner transaction,
            ILogger<IncassationService> logger)
        {
            _collectionRepo = collectionRepo;
            _cashSessionRepo = cashSessionRepo;
            _deviceRepo = deviceRepo;
            _commandSender = commandSender;
            _transaction = transaction;
            _logger = logger;
        }

        public async Task<GenericDto<List<IncassationDeviceDto>>> GetDevicesAsync(AccessScope scope)
        {
            if (!scope.IsManage)
                return GenericDto<List<IncassationDeviceDto>>.Error(403, NotManageMessage);

            // Manage — scope cheklovi yo'q: inkassator barcha merchantlarning qurilmalarini ko'radi.
            var devices = await _deviceRepo.GetCashDevicesAsync(merchantId: null);

            var items = new List<IncassationDeviceDto>(devices.Count);
            foreach (var device in devices)
            {
                var open = await _collectionRepo.GetOpenByDeviceAsync(device.Id);
                items.Add(ToDeviceDto(device, open));
            }

            return GenericDto<List<IncassationDeviceDto>>.Success(items);
        }

        public async Task<GenericDto<CashCollectionDto>> RequestOpenAsync(AccessScope scope, long deviceId)
        {
            if (!scope.IsManage)
                return GenericDto<CashCollectionDto>.Error(403, NotManageMessage);

            var device = await _deviceRepo.GetByIdAsync(deviceId);
            if (device is null || device.Station is null)
                return GenericDto<CashCollectionDto>.Error(404, "Qurilma topilmadi.");

            // Ikkinchi inkassator ayni qurilmani parallel ochib yuborishining oldini olamiz.
            var existing = await _collectionRepo.GetOpenByDeviceAsync(deviceId);
            if (existing is not null)
            {
                return GenericDto<CashCollectionDto>.Error(409,
                    "Bu qurilmada tugallanmagan inkassatsiya bor. Avval uni yakunlang yoki bekor qiling.");
            }

            // Kutilgan summa so'rov paytida muzlatiladi: box ochilgunicha yangi naqd
            // tushishi mumkin, lekin dalolatnoma aynan shu qiymat bilan solishtiriladi.
            var collection = await _collectionRepo.CreateAsync(new CashCollectionEntity
            {
                DeviceId = device.Id,
                SerialNumber = device.SerialNumber,
                MerchantId = device.Station.MerchantId,
                StationId = device.StationId,
                IncassatorUserId = scope.UserId,
                Status = CashCollectionStatus.Requested,
                ExpectedAmount = device.CashBalance,
                RequestedAt = DateTime.Now
            });

            var delivered = await _commandSender.SendCashBoxOpenAsync(device.SerialNumber, collection.Id);

            if (!delivered)
            {
                collection.Status = CashCollectionStatus.Failed;
                collection.Notes = "Qurilmaga buyruq yetkazilmadi.";
                await _collectionRepo.UpdateAsync(collection);

                _logger.LogError(
                    "[INCASS] Box ochish buyrug'i yetkazilmadi serial={Serial} collectionId={CollectionId}",
                    device.SerialNumber, collection.Id);

                return GenericDto<CashCollectionDto>.Error(503,
                    "Qurilmaga buyruq yuborilmadi. Qurilma onlayn ekanini tekshiring.");
            }

            _logger.LogInformation(
                "[INCASS] Box ochish so'raldi serial={Serial} collectionId={CollectionId} kutilgan={Expected} inkassator={UserId}",
                device.SerialNumber, collection.Id, collection.ExpectedAmount, scope.UserId);

            return GenericDto<CashCollectionDto>.Success(ToDto(collection));
        }

        public async Task MarkBoxOpenedAsync(string serialNumber, long collectionId, CancellationToken ct = default)
        {
            var collection = await _collectionRepo.GetByIdAsync(collectionId);

            if (collection is null || collection.SerialNumber != serialNumber)
            {
                _logger.LogWarning(
                    "[INCASS] Noma'lum box tasdig'i serial={Serial} collectionId={CollectionId}",
                    serialNumber, collectionId);
                return;
            }

            if (collection.Status != CashCollectionStatus.Requested)
                return;

            collection.Status = CashCollectionStatus.BoxOpened;
            collection.BoxOpenedAt = DateTime.Now;
            await _collectionRepo.UpdateAsync(collection);

            _logger.LogInformation(
                "[INCASS] Box ochildi serial={Serial} collectionId={CollectionId}", serialNumber, collectionId);
        }

        public async Task<GenericDto<CashCollectionDto>> ConfirmAsync(
            AccessScope scope, long collectionId, decimal countedAmount, string? notes)
        {
            if (!scope.IsManage)
                return GenericDto<CashCollectionDto>.Error(403, NotManageMessage);

            if (countedAmount < 0)
                return GenericDto<CashCollectionDto>.Error(400, "Sanalgan summa manfiy bo'lishi mumkin emas.");

            var collection = await _collectionRepo.GetByIdAsync(collectionId);
            if (collection is null)
                return GenericDto<CashCollectionDto>.Error(404, "Inkassatsiya topilmadi.");

            if (collection.Status is not (CashCollectionStatus.Requested or CashCollectionStatus.BoxOpened))
                return GenericDto<CashCollectionDto>.Error(409, "Inkassatsiya allaqachon yakunlangan.");

            decimal collected = 0m;

            // Qoldiqni nolga tushirish va dalolatnomani yopish — bitta tranzaksiyada:
            // biri bajarilib ikkinchisi bajarilmasa pul hisobi buziladi.
            await _transaction.RunAsync(async () =>
            {
                collected = await _deviceRepo.CollectCashAsync(collection.DeviceId);

                collection.Status = CashCollectionStatus.Confirmed;
                collection.CountedAmount = countedAmount;
                collection.ConfirmedAt = DateTime.Now;
                collection.Notes = notes;
                await _collectionRepo.UpdateAsync(collection);
            });

            // Farq amalni to'xtatmaydi — ikkala qiymat ham saqlanadi va ko'rinadi.
            if (countedAmount != collected)
            {
                _logger.LogWarning(
                    "[INCASS] FARQ collectionId={CollectionId} serial={Serial} serverda={Server} sanalgan={Counted}",
                    collection.Id, collection.SerialNumber, collected, countedAmount);
            }

            _logger.LogInformation(
                "[INCASS] Tasdiqlandi collectionId={CollectionId} serial={Serial} olingan={Collected} inkassator={UserId}",
                collection.Id, collection.SerialNumber, collected, scope.UserId);

            return GenericDto<CashCollectionDto>.Success(ToDto(collection));
        }

        public async Task<GenericDto<CashCollectionDto>> CancelAsync(
            AccessScope scope, long collectionId, string? notes)
        {
            if (!scope.IsManage)
                return GenericDto<CashCollectionDto>.Error(403, NotManageMessage);

            var collection = await _collectionRepo.GetByIdAsync(collectionId);
            if (collection is null)
                return GenericDto<CashCollectionDto>.Error(404, "Inkassatsiya topilmadi.");

            if (collection.Status is not (CashCollectionStatus.Requested or CashCollectionStatus.BoxOpened))
                return GenericDto<CashCollectionDto>.Error(409, "Inkassatsiya allaqachon yakunlangan.");

            collection.Status = CashCollectionStatus.Cancelled;
            collection.Notes = notes;
            await _collectionRepo.UpdateAsync(collection);

            _logger.LogInformation(
                "[INCASS] Bekor qilindi collectionId={CollectionId} inkassator={UserId}", collection.Id, scope.UserId);

            return GenericDto<CashCollectionDto>.Success(ToDto(collection));
        }

        public async Task<GenericDto<PagedResult<CashCollectionDto>>> GetHistoryAsync(
            AccessScope scope, PaginationParams param, long? deviceId)
        {
            if (!scope.IsManage)
                return GenericDto<PagedResult<CashCollectionDto>>.Error(403, NotManageMessage);

            var page = await _collectionRepo.GetAllAsync(param, merchantId: null, deviceId);

            return GenericDto<PagedResult<CashCollectionDto>>.Success(page.Map(ToDto));
        }

        public async Task<GenericDto<PagedResult<CashSessionListDto>>> GetCashSessionsAsync(
            AccessScope scope, PaginationParams param, CashSessionStatus? status)
        {
            if (!scope.IsManage && scope.MerchantId is null)
                return GenericDto<PagedResult<CashSessionListDto>>.Success(PagedResult<CashSessionListDto>.Empty(param));

            var page = await _cashSessionRepo.GetAllAsync(
                param, scope.IsManage ? null : scope.MerchantId, status);

            return GenericDto<PagedResult<CashSessionListDto>>.Success(page.Map(ToCashSessionDto));
        }

        private static CashSessionListDto ToCashSessionDto(CashSessionEntity s)
            => new()
            {
                Id = s.Id,
                DeviceId = s.DeviceId,
                SerialNumber = s.SerialNumber,
                // Karta tokeni ataylab chiqarilmaydi — faqat maska.
                CardMasked = s.CardMasked,
                Status = s.Status,
                AcceptedAmount = s.AcceptedAmount,
                BillCount = s.BillCount,
                PayoutReference = s.PayoutReference,
                FailureReason = s.FailureReason,
                AttemptCount = s.AttemptCount,
                NextAttemptAt = s.NextAttemptAt,
                CreatedDate = s.CreatedDate,
                CompletedAt = s.CompletedAt
            };

        private static IncassationDeviceDto ToDeviceDto(DeviceEntity device, CashCollectionEntity? open)
            => new()
            {
                DeviceId = device.Id,
                SerialNumber = device.SerialNumber,
                Model = device.Model,
                StationId = device.StationId,
                StationName = device.Station?.Name ?? string.Empty,
                Address = device.Station?.Address ?? string.Empty,
                // PostGIS Point: X = uzunlik (longitude), Y = kenglik (latitude).
                Latitude = device.Station?.Coordinates?.Y ?? 0,
                Longitude = device.Station?.Coordinates?.X ?? 0,
                CashBalance = device.CashBalance,
                CashLastCollectedAt = device.CashLastCollectedAt,
                IsOnline = device.IsOnline,
                LastSeenAt = device.LastSeenAt,
                HasOpenCollection = open is not null,
                OpenCollectionId = open?.Id
            };

        private static CashCollectionDto ToDto(CashCollectionEntity c)
            => new()
            {
                Id = c.Id,
                DeviceId = c.DeviceId,
                SerialNumber = c.SerialNumber,
                Status = c.Status,
                ExpectedAmount = c.ExpectedAmount,
                CountedAmount = c.CountedAmount,
                IncassatorUserId = c.IncassatorUserId,
                RequestedAt = c.RequestedAt,
                BoxOpenedAt = c.BoxOpenedAt,
                ConfirmedAt = c.ConfirmedAt,
                Notes = c.Notes
            };
    }
}
