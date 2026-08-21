using Domain.Dtos.Base;
using Domain.Dtos.Process;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Sessiya ichidagi mahsulot berish jarayonlarini boshqaradi:
    /// start / stop / pause / resume + qurilmadan kelgan telemetry va finish hodisalari.
    /// </summary>
    public class ProcessService : IProcessService
    {
        private readonly IProductProcessRepository _processRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly IProductRepository _productRepo;
        private readonly IDeviceCommandPublisher _commandPublisher;
        private readonly IDeviceLockService _deviceLock;
        private readonly IProcessSettlementService _settlement;
        private readonly IHoldSettlementService _holdSettlement;
        private readonly ISessionNotifier _notifier;
        private readonly ITransactionRunner _tx;
        private readonly ILogger<ProcessService> _logger;

        /// <summary>
        /// Stop/pause buyrug'i yuborilgach qurilmadan tasdiq (yoki telemetry) shuncha vaqt kelmasa,
        /// watchdog jarayonni majburan yakunlaydi. Inersiya + tarmoq kechikishidan kattaroq bo'lishi kerak.
        /// </summary>
        private static readonly TimeSpan StalledTimeout = TimeSpan.FromSeconds(60);

        public ProcessService(
            IProductProcessRepository processRepo,
            ISessionRepository sessionRepo,
            IProductRepository productRepo,
            IDeviceCommandPublisher commandPublisher,
            IDeviceLockService deviceLock,
            IProcessSettlementService settlement,
            IHoldSettlementService holdSettlement,
            ISessionNotifier notifier,
            ITransactionRunner tx,
            ILogger<ProcessService> logger)
        {
            _processRepo = processRepo;
            _sessionRepo = sessionRepo;
            _productRepo = productRepo;
            _commandPublisher = commandPublisher;
            _deviceLock = deviceLock;
            _settlement = settlement;
            _holdSettlement = holdSettlement;
            _notifier = notifier;
            _tx = tx;
            _logger = logger;
        }

        public async Task<GenericDto<StartProcessResultDto>> StartAsync(StartProcessDto dto)
        {
            var foundSession = await _sessionRepo.GetByIdAsync(dto.SessionId);
            var foundProduct = await _productRepo.GetByIdAsync(dto.ProductId);

            // Funding FAQAT tasdiqlangan Hold balansidan. Internal balance biznes mantig'ida
            // ishlatilmaydi (faqat entity + GET). "No hold = no fuel": tasdiqlangan Hold bo'lmasa
            // jarayon boshlanmaydi — ichki balansga fallback yo'q.
            long holdTiyin = 0;
            decimal limit = 0;

            var stop = await StopFactorCheck.For(StopActions.ProcessStart)
                .StopIf(foundSession is null, StopFactors.Session.NotFound)
                .StopIf(() => foundSession!.UserId != dto.UserId, StopFactors.Session.NotOwned)
                .StopIf(() => foundSession!.User is { IsBlocked: true }, StopFactors.User.Blocked)
                .StopIf(() => foundSession!.Status == SessionStatus.Paused, StopFactors.Session.Paused)
                .StopIf(() => foundSession!.Status == SessionStatus.Settling, StopFactors.Session.Settling)
                .StopIf(() => foundSession!.Status is not (SessionStatus.Connected or SessionStatus.InProcess),
                        StopFactors.Session.NotConnected)
                .StopIf(() => foundSession!.Device is null || foundSession.DeviceId is null,
                        StopFactors.Device.NotAttachedToSession)
                .StopIf(() => !foundSession!.Device!.IsActive, StopFactors.Device.Inactive)
                // Buyruq brokerga ketadi, lekin oflayn qurilma uni olmaydi: jarayon "boshlandi"
                // deb yozilib, hech narsa berilmasdan watchdog uni yakunlagan bo'lardi.
                .StopIf(() => !foundSession!.Device!.IsReachable(),
                        () => StopFactors.Device.Offline(
                            foundSession!.Device!.SerialNumber, foundSession.Device.LastSeenAt))
                .StopIfAsync(() => _processRepo.HasActiveProcessAsync(foundSession!.Id),
                             StopFactors.Process.AlreadyActive)
                .StopIf(foundProduct is null, StopFactors.Product.NotFound)
                .StopIf(() => !foundProduct!.IsActive, StopFactors.Product.Inactive)
                .StopIf(() => foundProduct!.DeviceId != foundSession!.DeviceId, StopFactors.Product.DeviceMismatch)
                .StopIfAsync(async () =>
                {
                    holdTiyin = await _holdSettlement.GetAvailableHoldTiyinAsync(foundSession!.Id);
                    return holdTiyin <= 0;
                }, StopFactors.Process.NoFunding)
                .StopIf(() =>
                {
                    var maxAmount = foundProduct!.Price > 0 ? Money.ToUzs(holdTiyin) / foundProduct.Price : 0;
                    limit = dto.RequestedAmount.HasValue
                        ? Math.Min(dto.RequestedAmount.Value, maxAmount)
                        : maxAmount;
                    return limit <= 0;
                }, StopFactors.Process.FundingTooSmall)
                // Lock oxirgi to'siq: undan keyin darhol yozuv yaratiladi, ya'ni band qilingan
                // qurilma boshqa sababga ko'ra bo'sh qolib ketmaydi.
                .StopIfAsync(async () =>
                {
                    if (await _deviceLock.TryLockDeviceAsync(foundSession!.Device!.SerialNumber, dto.UserId))
                        return false;
                    var owner = await _deviceLock.GetLockOwnerAsync(foundSession.Device.SerialNumber);
                    return owner != dto.UserId;
                }, StopFactors.Device.LockedByOtherUser)
                .ResultAsync();

            if (stop is not null)
            {
                _logger.LogInformation(
                    "Jarayon boshlanmadi: sessionId={SessionId} userId={UserId} sabab={Reason}",
                    dto.SessionId, dto.UserId, stop.Code);
                return GenericDto<StartProcessResultDto>.Blocked(stop);
            }

            // Zanjir o'tdi ⇒ ikkalasi ham mavjud va qurilma buyruqni qabul qila oladi.
            var session = foundSession!;
            var product = foundProduct!;
            var device = session.Device!;
            var fundingSource = ProcessFundingSource.HoldBalance;

            var process = new ProductProcessEntity
            {
                SessionId = session.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                PricePerUnit = product.Price,
                Unit = product.Unit,
                RequestedAmount = limit,
                Status = ProcessStatus.Started,
                StartedAt = DateTime.Now,
                FundingSource = fundingSource
            };

            await _processRepo.CreateAsync(process);

            session.Status = SessionStatus.InProcess;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            await _commandPublisher.PublishStartAsync(device.SerialNumber, process.Id, product.Id, limit, product.Name, product.Unit.ToString(), product.Price);

            _logger.LogInformation(
                "Jarayon boshlandi: processId={ProcessId} sessionId={SessionId} productId={ProductId} limit={Limit}",
                process.Id, session.Id, product.Id, limit);

            await _notifier.NotifyProcessStartedAsync(session.SessionToken, new
            {
                process_id = process.Id,
                product_id = product.Id,
                product_name = product.Name,
                unit = product.Unit.ToString(),
                price_per_unit = product.Price,
                requested_amount = limit,
                started_at = process.StartedAt
            });

            return GenericDto<StartProcessResultDto>.Success(new StartProcessResultDto
            {
                ProcessId = process.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit.ToString(),
                PricePerUnit = product.Price,
                LimitAmount = limit,
                DeviceSerialNumber = device.SerialNumber,
                ResultMessage = "Jarayon boshlandi. Qurilmaga start buyrug'i yuborildi."
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> StopByUserAsync(ProcessControlDto dto)
        {
            var found = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);

            var stop = OwnershipCheck(StopActions.ProcessStop, found, dto.UserId)
                .StopIf(() => found!.Status == ProcessStatus.Ended, StopFactors.Process.AlreadyEnded)
                // Oflayn qurilmaga stop yuborib "yuborildi" deyish yolg'on bo'lardi. Foydalanuvchi
                // kutib qolmasligi uchun aniq aytamiz: jarayonni 60 soniyalik watchdog oxirgi
                // o'lchov bo'yicha o'zi yakunlaydi.
                .StopIf(() => DeviceOf(found) is null, StopFactors.Device.NotAttachedToSession)
                .StopIf(() => !DeviceOf(found)!.IsReachable(),
                        () => StopFactors.Process.DeviceOffline(
                            DeviceOf(found)!.SerialNumber, "to'xtatish",
                            "Jarayon oxirgi o'lchov bo'yicha avtomatik yakunlanadi."))
                .Result();

            if (stop is not null)
                return GenericDto<ProcessControlResultDto>.Blocked(stop);

            var process = found!;

            // Faqat qurilmaga stop yuboramiz — DB statusini O'ZGARTIRMAYMIZ.
            // Suyuqlik inersiya bilan to'xtaydi; qurilma to'liq yakunlab `process.finished` yuborgach
            // ReportDeviceFinishedAsync yakuniy miqdorni yozadi, balansni yechadi va lockni bo'shatadi.
            // Tasdiq kelmasa, watchdog (FinalizeStalledProcessesAsync) zaxira sifatida yakunlaydi.
            await _commandPublisher.PublishStopAsync(process.Session!.Device!.SerialNumber, process.Id);

            await TouchSessionAsync(process.Session);

            // Transient — klient tugmalarni disable qilib, ProcessEnded kelguncha kutadi.
            await _notifier.NotifyProcessStoppingAsync(process.Session!.SessionToken, new
            {
                process_id = process.Id,
                status = "Stopping"
            });

            return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
            {
                ProcessId = process.Id,
                Status = "Stopping",
                ResultMessage = "To'xtatish buyrug'i yuborildi. Qurilma yakunlashini kuting."
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> PauseAsync(ProcessControlDto dto)
        {
            var found = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);

            var stop = OwnershipCheck(StopActions.ProcessPause, found, dto.UserId)
                .StopIf(() => found!.Status is not (ProcessStatus.InProcess or ProcessStatus.Started),
                        StopFactors.Process.NotPausable)
                .StopIf(() => DeviceOf(found) is null, StopFactors.Device.NotAttachedToSession)
                .StopIf(() => !DeviceOf(found)!.IsReachable(),
                        () => StopFactors.Process.DeviceOffline(DeviceOf(found)!.SerialNumber, "pauza"))
                .Result();

            if (stop is not null)
                return GenericDto<ProcessControlResultDto>.Blocked(stop);

            var process = found!;

            // Faqat pause buyrug'ini yuboramiz — DB statusini O'ZGARTIRMAYMIZ.
            // Qurilma oqimni inersiya bilan to'xtatib, `process.paused` yuborgach
            // ReportDevicePausedAsync statusni Paused ga o'tkazadi.
            await _commandPublisher.PublishPauseAsync(process.Session!.Device!.SerialNumber, process.Id);

            await TouchSessionAsync(process.Session);

            // Transient — klient "pauza qilinmoqda" ko'rsatadi, ProcessPaused kelguncha kutadi.
            await _notifier.NotifyProcessPausingAsync(process.Session!.SessionToken, new
            {
                process_id = process.Id,
                status = "Pausing"
            });

            return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
            {
                ProcessId = process.Id,
                Status = "Pausing",
                ResultMessage = "Pauza buyrug'i yuborildi. Qurilma to'xtashini kuting."
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> ResumeAsync(ProcessControlDto dto)
        {
            var found = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);

            var stop = OwnershipCheck(StopActions.ProcessResume, found, dto.UserId)
                .StopIf(() => found!.Status != ProcessStatus.Paused, StopFactors.Process.NotPaused)
                .StopIf(() => DeviceOf(found) is null, StopFactors.Device.NotAttachedToSession)
                // Buyruq yetib bormasa status InProcess'ga o'tib ketardi va jarayon
                // "davom etmoqda" bo'lib ko'rinardi — aslida qurilma o'chgan.
                .StopIf(() => !DeviceOf(found)!.IsReachable(),
                        () => StopFactors.Process.DeviceOffline(DeviceOf(found)!.SerialNumber, "davom ettirish"))
                .Result();

            if (stop is not null)
                return GenericDto<ProcessControlResultDto>.Blocked(stop);

            var process = found!;

            await _commandPublisher.PublishResumeAsync(process.Session!.Device!.SerialNumber, process.Id);

            process.Status = ProcessStatus.InProcess;
            process.PausedAt = null;
            await _processRepo.UpdateAsync(process);

            await TouchSessionAsync(process.Session);

            await _notifier.NotifyProcessUpdatedAsync(process.Session!.SessionToken, new
            {
                process_id = process.Id,
                status = process.Status.ToString()
            });

            return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
            {
                ProcessId = process.Id,
                Status = process.Status.ToString(),
                ResultMessage = "Jarayon davom ettirildi."
            });
        }

        public async Task<GenericDto<ProcessTelemetryResultDto>> ReportTelemetryAsync(ProcessTelemetryDto dto)
        {
            if (dto.ProcessId <= 0 || dto.TotalGiven < 0)
                return GenericDto<ProcessTelemetryResultDto>.Error(400, "ProcessId musbat va TotalGiven manfiy bo'lmasligi shart.");

            var found = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);

            var stop = DeviceReportCheck(found, dto.SessionToken, dto.SerialNumber);
            if (stop is not null)
                return GenericDto<ProcessTelemetryResultDto>.Blocked(stop);

            var process = found!;
            var session = process.Session!;

            // Hot path — tracker SaveChanges chaqirilmaydi, hammasi atomic SQL bilan.
            // (ExecuteUpdateAsync xmin'ni siljitganidan keyin tracker'dagi entity stale bo'lib qoladi,
            //  shuning uchun in-memory mutatsiya qilmaymiz — keyingi SaveChanges 0 row affected qaytarib yiqilardi.)

            var affected = await _processRepo.SetGivenAmountAsync(process.Id, dto.TotalGiven, dto.Sequence);
            if (affected == 0)
                return GenericDto<ProcessTelemetryResultDto>.Success(new ProcessTelemetryResultDto
                {
                    ResultMessage = "Telemetry ignored (duplicate yoki jarayon aktiv emas)."
                });

            // Sessiya idle-timer'ini atomik yangilash (TouchAsync ExecuteUpdateAsync, SaveChanges chaqirmaydi).
            await _sessionRepo.TouchAsync(session.Id);

            var totalGiven = dto.TotalGiven;
            var currentCost = totalGiven * process.PricePerUnit;
            var sessionToken = session.SessionToken;
            var serial = session.Device?.SerialNumber;
            var userId = session.UserId;

            if (totalGiven >= process.RequestedAmount && process.RequestedAmount > 0)
            {
                var endedAt = DateTime.Now;

                // Yakunlash + balans yechish bitta tranzaksiyada — orada crash bo'lsa
                // ikkalasi birga rollback bo'ladi (Ended-lekin-yechilmagan holat qolmaydi).
                var (completed, deductedOnAutoComplete) = await _tx.RunAsync(async () =>
                {
                    var done = await _processRepo.CompleteProcessAsync(
                        process.Id, totalGiven, ProcessEndReason.Completed, endedAt);
                    if (done == 0)
                        return (0, 0m);

                    return (done, await _settlement.SettleAsync(process.Id));
                });

                if (completed > 0)
                {
                    if (!string.IsNullOrWhiteSpace(serial))
                    {
                        await _commandPublisher.PublishStopAsync(serial!, process.Id);
                        await _deviceLock.UnlockDeviceAsync(serial!, userId);
                    }

                    await _notifier.NotifyProcessEndedAsync(sessionToken, new
                    {
                        process_id = process.Id,
                        end_reason = nameof(ProcessEndReason.Completed),
                        total_given = totalGiven,
                        total_cost = deductedOnAutoComplete,
                        ended_at = endedAt
                    });

                    return GenericDto<ProcessTelemetryResultDto>.Success(new ProcessTelemetryResultDto
                    {
                        ResultMessage = "Telemetry qabul qilindi va jarayon avtomatik yakunlandi."
                    });
                }
            }

            await _notifier.NotifyProcessUpdatedAsync(sessionToken, new
            {
                process_id = process.Id,
                total_given = totalGiven,
                current_cost = currentCost,
                product_id = process.ProductId,
                unit = process.Unit.ToString(),
                price_per_unit = process.PricePerUnit
            });

            return GenericDto<ProcessTelemetryResultDto>.Success(new ProcessTelemetryResultDto
            {
                ResultMessage = "Telemetry qabul qilindi."
            });
        }

        public async Task<GenericDto<DeviceProcessReportResultDto>> ReportDeviceFinishedAsync(DeviceProcessReportDto dto)
        {
            var found = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);

            var stop = DeviceReportCheck(found, dto.SessionToken, dto.SerialNumber);
            if (stop is not null)
                return GenericDto<DeviceProcessReportResultDto>.Blocked(stop);

            var process = found!;
            var session = process.Session!;

            // Idempotency — agar jarayon allaqachon yakunlangan bo'lsa, balansni qayta yechmaymiz.
            if (process.Status == ProcessStatus.Ended)
                return GenericDto<DeviceProcessReportResultDto>.Success(new DeviceProcessReportResultDto
                {
                    ResultMessage = "Jarayon allaqachon yakunlangan.",
                    TotalDelivered = process.GivenAmount,
                    TotalCost = process.GivenAmount * process.PricePerUnit
                });

            if (dto.TotalGiven > process.GivenAmount)
                process.GivenAmount = dto.TotalGiven;

            process.Status = ProcessStatus.Ended;
            process.EndReason = dto.EndReason;
            process.EndedAt = DateTime.Now;

            // Yakunlash + balans yechish — bitta tranzaksiya (crash-safe).
            var deducted = await _tx.RunAsync(async () =>
            {
                await _processRepo.UpdateAsync(process);
                return await _settlement.SettleAsync(process.Id);
            });

            var serial = session.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                await _deviceLock.UnlockDeviceAsync(serial!, session.UserId);

            await TouchSessionAsync(session);

            await _notifier.NotifyProcessEndedAsync(session.SessionToken, new
            {
                process_id = process.Id,
                end_reason = dto.EndReason.ToString(),
                total_given = process.GivenAmount,
                total_cost = deducted,
                ended_at = process.EndedAt
            });

            _logger.LogInformation(
                "Jarayon yakunlandi (device): processId={ProcessId} given={Given} deducted={Deducted} reason={Reason}",
                process.Id, process.GivenAmount, deducted, dto.EndReason);

            return GenericDto<DeviceProcessReportResultDto>.Success(new DeviceProcessReportResultDto
            {
                ResultMessage = "Jarayon yakunlandi.",
                TotalDelivered = process.GivenAmount,
                TotalCost = deducted
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> ReportDevicePausedAsync(DeviceProcessPausedDto dto)
        {
            var found = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);

            var stop = DeviceReportCheck(found, dto.SessionToken, dto.SerialNumber);
            if (stop is not null)
                return GenericDto<ProcessControlResultDto>.Blocked(stop);

            var process = found!;
            var session = process.Session!;

            // Idempotent — allaqachon yakunlangan yoki pauza qilingan bo'lsa qayta o'zgartirmaymiz.
            if (process.Status == ProcessStatus.Ended)
                return GenericDto<ProcessControlResultDto>.Blocked(StopFactors.Process.AlreadyEnded);

            if (process.Status == ProcessStatus.Paused)
                return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
                {
                    ProcessId = process.Id,
                    Status = process.Status.ToString(),
                    ResultMessage = "Jarayon allaqachon pauzada."
                });

            // Inersiya bilan birga yakuniy miqdorni yozamiz (kamaymasligi kerak).
            if (dto.TotalGiven > process.GivenAmount)
                process.GivenAmount = dto.TotalGiven;

            process.Status = ProcessStatus.Paused;
            process.PausedAt = DateTime.Now;
            await _processRepo.UpdateAsync(process);

            await TouchSessionAsync(session);

            // Balans yechilmaydi — process tugamadi, resume qilinishi mumkin.
            await _notifier.NotifyProcessUpdatedAsync(session.SessionToken, new
            {
                process_id = process.Id,
                status = process.Status.ToString(),
                total_given = process.GivenAmount,
                paused_at = process.PausedAt
            });

            return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
            {
                ProcessId = process.Id,
                Status = process.Status.ToString(),
                ResultMessage = "Jarayon pauza qilindi."
            });
        }

        public async Task FinalizeStalledProcessesAsync()
        {
            var staleBefore = DateTime.Now.Subtract(StalledTimeout);
            var stalled = await _processRepo.GetStalledProcessesAsync(staleBefore);

            foreach (var process in stalled)
            {
                var endedAt = DateTime.Now;

                // Atomic — boshqa thread (kech kelgan process.finished) yutib bo'lgan bo'lsa 0 qaytaradi.
                // Yakunlash + balans yechish bitta tranzaksiyada (crash-safe).
                var (completed, deducted) = await _tx.RunAsync(async () =>
                {
                    var done = await _processRepo.CompleteProcessAsync(
                        process.Id, process.GivenAmount, ProcessEndReason.DeviceError, endedAt);
                    if (done == 0)
                        return (0, 0m);

                    return (done, await _settlement.SettleAsync(process.Id));
                });
                if (completed == 0)
                    continue;

                _logger.LogWarning(
                    "Watchdog jarayonni majburan yakunladi: processId={ProcessId} given={Given} deducted={Deducted}",
                    process.Id, process.GivenAmount, deducted);

                var serial = process.Session?.Device?.SerialNumber;
                if (!string.IsNullOrWhiteSpace(serial))
                {
                    // Qurilma jonli bo'lsa (lekin tasdiq yubormagan bo'lsa) — yana bir bor stop.
                    await _commandPublisher.PublishStopAsync(serial!, process.Id);
                    await _deviceLock.UnlockDeviceAsync(serial!, process.Session!.UserId);
                }

                if (process.Session is not null)
                    await _notifier.NotifyProcessEndedAsync(process.Session.SessionToken, new
                    {
                        process_id = process.Id,
                        end_reason = nameof(ProcessEndReason.DeviceError),
                        total_delivered = process.GivenAmount,
                        total_cost = deducted,
                        ended_at = endedAt
                    });
            }
        }

        // ── Yordamchi ─────────────────────────────────────────────────

        /// <summary>Har uchala boshqaruv amali uchun umumiy birinchi ikki to'siq (mavjudlik + egalik).</summary>
        private static StopFactorCheck OwnershipCheck(string action, ProductProcessEntity? process, long userId)
            => StopFactorCheck.For(action)
                .StopIf(process is null, StopFactors.Process.NotFound)
                .StopIf(() => process!.Session is null || process.Session.UserId != userId,
                        StopFactors.Process.NotOwned);

        /// <summary>Qurilmadan kelgan hisobotlar uchun umumiy to'siqlar (mavjudlik + token + serial mosligi).</summary>
        private static StopFactor? DeviceReportCheck(
            ProductProcessEntity? process, string? sessionToken, string? serialNumber)
            => StopFactorCheck.For("Process.DeviceReport")
                .StopIf(process is null, StopFactors.Process.NotFound)
                .StopIf(() => process!.Session?.SessionToken != sessionToken, StopFactors.Session.TokenMismatch)
                .StopIf(() => process!.Session!.Device?.SerialNumber != serialNumber,
                        StopFactors.Device.NotBoundToProcess)
                .Result();

        /// <summary>Jarayon bog'langan qurilma (yo'q bo'lishi mumkin).</summary>
        private static DeviceEntity? DeviceOf(ProductProcessEntity? process) => process?.Session?.Device;

        private async Task TouchSessionAsync(SessionEntity? session)
        {
            if (session is null) return;
            // Atomic UPDATE — SaveChanges chaqirmaydi, shuning uchun tracker'da modified bo'lib
            // turgan boshqa entitylarni yozishga urinmaydi (race-safe va concurrency-safe).
            await _sessionRepo.TouchAsync(session.Id);
        }
    }
}
