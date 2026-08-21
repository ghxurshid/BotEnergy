using Domain.Auth;
using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
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
        private readonly ICashCollectionRepository _collectionRepo;
        private readonly ICashSessionRepository _cashSessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IDeviceCommandSender _commandSender;
        private readonly IUsageProbeRepository _usageProbe;
        private readonly ITransactionRunner _transaction;
        private readonly ILogger<IncassationService> _logger;

        public IncassationService(
            ICashCollectionRepository collectionRepo,
            ICashSessionRepository cashSessionRepo,
            IDeviceRepository deviceRepo,
            IDeviceCommandSender commandSender,
            IUsageProbeRepository usageProbe,
            ITransactionRunner transaction,
            ILogger<IncassationService> logger)
        {
            _collectionRepo = collectionRepo;
            _cashSessionRepo = cashSessionRepo;
            _deviceRepo = deviceRepo;
            _commandSender = commandSender;
            _usageProbe = usageProbe;
            _transaction = transaction;
            _logger = logger;
        }

        public async Task<GenericDto<List<IncassationDeviceDto>>> GetDevicesAsync(AccessScope scope)
        {
            if (!scope.IsManage)
                return GenericDto<List<IncassationDeviceDto>>.Blocked(StopFactors.Incassation.NotManage);

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
            var device = await _deviceRepo.GetByIdAsync(deviceId);

            // Barcha to'siqlar dalolatnoma YARATILISHIDAN OLDIN tekshiriladi: oflayn qurilmaga
            // buyruq yuborib bo'lmaydi, shuning uchun "Requested" yozuvi ham yaratilmaydi —
            // aks holda bazada hech qachon ochilmaydigan dalolatnomalar to'planib qoladi.
            var stop = await StopFactorCheck.For(StopActions.CashBoxOpen)
                .StopIf(!scope.IsManage, StopFactors.Incassation.NotManage)
                .StopIf(device is null, StopFactors.Device.NotFound)
                .StopIf(() => device!.Station is null, StopFactors.Device.NoStation)
                .StopIf(() => !device!.IsActive, StopFactors.Device.Inactive)
                // Publish brokerga ketadi-yu, o'chgan qurilma uni olmaydi — shuning uchun
                // buyruqni umuman yubormaymiz (kassirga "yuborildi" deb aldamaymiz).
                .StopIf(() => !device!.IsReachable(),
                        () => StopFactors.Device.Offline(device!.SerialNumber, device.LastSeenAt))
                // Ikkinchi inkassator ayni qurilmani parallel ochib yuborishining oldini olamiz.
                .StopIfAsync(() => _usageProbe.DeviceHasOpenCollectionAsync(deviceId),
                             StopFactors.Device.HasOpenCollection)
                // Mijoz ayni damda pul solayotgan bo'lsa, box ochilishi hisobni buzadi:
                // kutilgan summa muzlatilgandan keyin tushgan kupyura yo'qolgan bo'lib ko'rinadi.
                .StopIfAsync(() => _usageProbe.DeviceHasOpenCashSessionAsync(deviceId),
                             StopFactors.Device.HasOpenCashSession)
                .ResultAsync();

            if (stop is not null)
            {
                _logger.LogInformation(
                    "[INCASS] Box ochish rad etildi deviceId={DeviceId} sabab={Reason} inkassator={UserId}",
                    deviceId, stop.Code, scope.UserId);
                return GenericDto<CashCollectionDto>.Blocked(stop);
            }

            // Zanjir o'tdi ⇒ qurilma ham, stansiyasi ham mavjud.
            var target = device!;
            var station = target.Station!;

            // Kutilgan summa so'rov paytida muzlatiladi: box ochilgunicha yangi naqd
            // tushishi mumkin, lekin dalolatnoma aynan shu qiymat bilan solishtiriladi.
            var collection = await _collectionRepo.CreateAsync(new CashCollectionEntity
            {
                DeviceId = target.Id,
                SerialNumber = target.SerialNumber,
                MerchantId = station.MerchantId,
                StationId = target.StationId,
                IncassatorUserId = scope.UserId,
                Status = CashCollectionStatus.Requested,
                ExpectedAmount = target.CashBalance,
                RequestedAt = DateTime.Now
            });

            var delivered = await _commandSender.SendCashBoxOpenAsync(target.SerialNumber, collection.Id);

            if (!delivered)
            {
                collection.Status = CashCollectionStatus.Failed;
                collection.Notes = "Qurilmaga buyruq yetkazilmadi.";
                await _collectionRepo.UpdateAsync(collection);

                // Qurilma onlayn ko'ringan edi, lekin transport (broker/SessionApi) yiqildi —
                // bu infratuzilma nosozligi, shuning uchun dalolatnoma Failed bo'lib qoladi.
                _logger.LogError(
                    "[INCASS] Box ochish buyrug'i yetkazilmadi serial={Serial} collectionId={CollectionId}",
                    target.SerialNumber, collection.Id);

                return GenericDto<CashCollectionDto>.Blocked(StopFactors.Incassation.CommandUndelivered);
            }

            _logger.LogInformation(
                "[INCASS] Box ochish so'raldi serial={Serial} collectionId={CollectionId} kutilgan={Expected} inkassator={UserId}",
                target.SerialNumber, collection.Id, collection.ExpectedAmount, scope.UserId);

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
            var collection = await _collectionRepo.GetByIdAsync(collectionId);

            var stop = StopFactorCheck.For(StopActions.CashCollectionConfirm)
                .StopIf(!scope.IsManage, StopFactors.Incassation.NotManage)
                .StopIf(countedAmount < 0, StopFactors.Incassation.NegativeAmount)
                .StopIf(collection is null, StopFactors.Incassation.NotFound)
                .StopIf(() => collection!.Status is not (CashCollectionStatus.Requested or CashCollectionStatus.BoxOpened),
                        StopFactors.Incassation.AlreadyFinished)
                .Result();

            if (stop is not null)
                return GenericDto<CashCollectionDto>.Blocked(stop);

            var target = collection!;
            decimal collected = 0m;

            // Qoldiqni nolga tushirish va dalolatnomani yopish — bitta tranzaksiyada:
            // biri bajarilib ikkinchisi bajarilmasa pul hisobi buziladi.
            await _transaction.RunAsync(async () =>
            {
                collected = await _deviceRepo.CollectCashAsync(target.DeviceId);

                target.Status = CashCollectionStatus.Confirmed;
                target.CountedAmount = countedAmount;
                target.ConfirmedAt = DateTime.Now;
                target.Notes = notes;
                await _collectionRepo.UpdateAsync(target);
            });

            // Farq amalni to'xtatmaydi — ikkala qiymat ham saqlanadi va ko'rinadi.
            if (countedAmount != collected)
            {
                _logger.LogWarning(
                    "[INCASS] FARQ collectionId={CollectionId} serial={Serial} serverda={Server} sanalgan={Counted}",
                    target.Id, target.SerialNumber, collected, countedAmount);
            }

            _logger.LogInformation(
                "[INCASS] Tasdiqlandi collectionId={CollectionId} serial={Serial} olingan={Collected} inkassator={UserId}",
                target.Id, target.SerialNumber, collected, scope.UserId);

            return GenericDto<CashCollectionDto>.Success(ToDto(target));
        }

        public async Task<GenericDto<CashCollectionDto>> CancelAsync(
            AccessScope scope, long collectionId, string? notes)
        {
            var collection = await _collectionRepo.GetByIdAsync(collectionId);

            var stop = StopFactorCheck.For(StopActions.CashCollectionCancel)
                .StopIf(!scope.IsManage, StopFactors.Incassation.NotManage)
                .StopIf(collection is null, StopFactors.Incassation.NotFound)
                .StopIf(() => collection!.Status is not (CashCollectionStatus.Requested or CashCollectionStatus.BoxOpened),
                        StopFactors.Incassation.AlreadyFinished)
                .Result();

            if (stop is not null)
                return GenericDto<CashCollectionDto>.Blocked(stop);

            var target = collection!;
            target.Status = CashCollectionStatus.Cancelled;
            target.Notes = notes;
            await _collectionRepo.UpdateAsync(target);

            _logger.LogInformation(
                "[INCASS] Bekor qilindi collectionId={CollectionId} inkassator={UserId}", target.Id, scope.UserId);

            return GenericDto<CashCollectionDto>.Success(ToDto(target));
        }

        public async Task<GenericDto<PagedResult<CashCollectionDto>>> GetHistoryAsync(
            AccessScope scope, PaginationParams param, long? deviceId)
        {
            if (!scope.IsManage)
                return GenericDto<PagedResult<CashCollectionDto>>.Blocked(StopFactors.Incassation.NotManage);

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
