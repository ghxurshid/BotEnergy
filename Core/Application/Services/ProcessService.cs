using Domain.Dtos.Base;
using Domain.Dtos.Process;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

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
        private readonly IBillingService _billing;
        private readonly ISessionNotifier _notifier;

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
            IBillingService billing,
            ISessionNotifier notifier)
        {
            _processRepo = processRepo;
            _sessionRepo = sessionRepo;
            _productRepo = productRepo;
            _commandPublisher = commandPublisher;
            _deviceLock = deviceLock;
            _billing = billing;
            _notifier = notifier;
        }

        public async Task<GenericDto<StartProcessResultDto>> StartAsync(StartProcessDto dto)
        {
            var session = await _sessionRepo.GetByIdAsync(dto.SessionId);
            if (session is null)
                return GenericDto<StartProcessResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.UserId != dto.UserId)
                return GenericDto<StartProcessResultDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            if (session.Status != SessionStatus.Connected && session.Status != SessionStatus.InProcess)
                return GenericDto<StartProcessResultDto>.Error(400, "Sessiya ulanmagan yoki yopilgan.");

            if (session.Device is null || session.DeviceId is null)
                return GenericDto<StartProcessResultDto>.Error(400, "Qurilma sessiyaga ulanmagan.");

            if (await _processRepo.HasActiveProcessAsync(session.Id))
                return GenericDto<StartProcessResultDto>.Error(409, "Sessiyada hali tugamagan jarayon mavjud.");

            var product = await _productRepo.GetByIdAsync(dto.ProductId);
            if (product is null || !product.IsActive)
                return GenericDto<StartProcessResultDto>.Error(404, "Mahsulot topilmadi yoki faol emas.");

            if (product.DeviceId != session.DeviceId)
                return GenericDto<StartProcessResultDto>.Error(400, "Mahsulot ushbu qurilmaga tegishli emas.");

            var availableBalance = await _billing.GetAvailableBalanceAsync(dto.UserId);
            var maxAmount = product.Price > 0 ? availableBalance / product.Price : 0;

            var limit = dto.RequestedAmount.HasValue
                ? Math.Min(dto.RequestedAmount.Value, maxAmount)
                : maxAmount;

            if (limit <= 0)
                return GenericDto<StartProcessResultDto>.Error(400, "Balans yetarli emas.");

            var lockTaken = await _deviceLock.TryLockDeviceAsync(session.Device.SerialNumber, dto.UserId);
            if (!lockTaken)
            {
                var owner = await _deviceLock.GetLockOwnerAsync(session.Device.SerialNumber);
                if (owner != dto.UserId)
                    return GenericDto<StartProcessResultDto>.Error(409, "Qurilma boshqa foydalanuvchi tomonidan band qilingan.");
            }

            var process = new ProductProcessEntity
            {
                SessionId = session.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                PricePerUnit = product.Price,
                Unit = product.Unit,
                RequestedAmount = limit,
                Status = ProcessStatus.Started,
                StartedAt = DateTime.Now
            };

            await _processRepo.CreateAsync(process);

            session.Status = SessionStatus.InProcess;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            await _commandPublisher.PublishStartAsync(session.Device.SerialNumber, process.Id, product.Id, limit, product.Name, product.Unit.ToString(), product.Price);

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
                DeviceSerialNumber = session.Device.SerialNumber,
                ResultMessage = "Jarayon boshlandi. Qurilmaga start buyrug'i yuborildi."
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> StopByUserAsync(ProcessControlDto dto)
        {
            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            var validation = ValidateProcessOwner(process, dto.UserId);
            if (validation is not null) return validation;

            if (process!.Status == ProcessStatus.Ended)
                return GenericDto<ProcessControlResultDto>.Error(400, "Jarayon allaqachon yakunlangan.");

            // Faqat qurilmaga stop yuboramiz — DB statusini O'ZGARTIRMAYMIZ.
            // Suyuqlik inersiya bilan to'xtaydi; qurilma to'liq yakunlab `process.finished` yuborgach
            // ReportDeviceFinishedAsync yakuniy miqdorni yozadi, balansni yechadi va lockni bo'shatadi.
            // Tasdiq kelmasa, watchdog (FinalizeStalledProcessesAsync) zaxira sifatida yakunlaydi.
            var serial = process.Session?.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                await _commandPublisher.PublishStopAsync(serial!, process.Id);

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
            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            var validation = ValidateProcessOwner(process, dto.UserId);
            if (validation is not null) return validation;

            if (process!.Status != ProcessStatus.InProcess && process.Status != ProcessStatus.Started)
                return GenericDto<ProcessControlResultDto>.Error(400, "Jarayon pauza qilish uchun mos holatda emas.");

            // Faqat pause buyrug'ini yuboramiz — DB statusini O'ZGARTIRMAYMIZ.
            // Qurilma oqimni inersiya bilan to'xtatib, `process.paused` yuborgach
            // ReportDevicePausedAsync statusni Paused ga o'tkazadi.
            var serial = process.Session?.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                await _commandPublisher.PublishPauseAsync(serial!, process.Id);

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
            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            var validation = ValidateProcessOwner(process, dto.UserId);
            if (validation is not null) return validation;

            if (process!.Status != ProcessStatus.Paused)
                return GenericDto<ProcessControlResultDto>.Error(400, "Jarayon pauzada emas.");

            var serial = process.Session?.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                await _commandPublisher.PublishResumeAsync(serial!, process.Id);

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

            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            if (process is null)
                return GenericDto<ProcessTelemetryResultDto>.Error(404, "Jarayon topilmadi.");

            if (process.Session?.SessionToken != dto.SessionToken)
                return GenericDto<ProcessTelemetryResultDto>.Error(403, "Sessiya tokeni mos kelmadi.");

            if (process.Session.Device?.SerialNumber != dto.SerialNumber)
                return GenericDto<ProcessTelemetryResultDto>.Error(403, "Qurilma bu jarayonga tegishli emas.");

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
            await _sessionRepo.TouchAsync(process.Session.Id);

            var totalGiven = dto.TotalGiven;
            var currentCost = totalGiven * process.PricePerUnit;
            var sessionToken = process.Session.SessionToken;
            var serial = process.Session.Device?.SerialNumber;
            var userId = process.Session.UserId;

            if (totalGiven >= process.RequestedAmount && process.RequestedAmount > 0)
            {
                var endedAt = DateTime.Now;
                var completed = await _processRepo.CompleteProcessAsync(
                    process.Id, totalGiven, ProcessEndReason.Completed, endedAt);

                if (completed > 0)
                {
                    if (!string.IsNullOrWhiteSpace(serial))
                    {
                        await _commandPublisher.PublishStopAsync(serial!, process.Id);
                        await _deviceLock.UnlockDeviceAsync(serial!, userId);
                    }

                    var deductedOnAutoComplete = await _billing.DeductForProcessAsync(process.Id);

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
            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            if (process is null)
                return GenericDto<DeviceProcessReportResultDto>.Error(404, "Jarayon topilmadi.");

            if (process.Session?.SessionToken != dto.SessionToken)
                return GenericDto<DeviceProcessReportResultDto>.Error(403, "Sessiya tokeni mos kelmadi.");

            if (process.Session.Device?.SerialNumber != dto.SerialNumber)
                return GenericDto<DeviceProcessReportResultDto>.Error(403, "Qurilma bu jarayonga tegishli emas.");

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
            await _processRepo.UpdateAsync(process);

            var deducted = await _billing.DeductForProcessAsync(process.Id);

            var serial = process.Session.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                await _deviceLock.UnlockDeviceAsync(serial!, process.Session.UserId);

            await TouchSessionAsync(process.Session);

            await _notifier.NotifyProcessEndedAsync(process.Session.SessionToken, new
            {
                process_id = process.Id,
                end_reason = dto.EndReason.ToString(),
                total_given = process.GivenAmount,
                total_cost = deducted,
                ended_at = process.EndedAt
            });

            return GenericDto<DeviceProcessReportResultDto>.Success(new DeviceProcessReportResultDto
            {
                ResultMessage = "Jarayon yakunlandi.",
                TotalDelivered = process.GivenAmount,
                TotalCost = deducted
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> ReportDevicePausedAsync(DeviceProcessPausedDto dto)
        {
            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            if (process is null)
                return GenericDto<ProcessControlResultDto>.Error(404, "Jarayon topilmadi.");

            if (process.Session?.SessionToken != dto.SessionToken)
                return GenericDto<ProcessControlResultDto>.Error(403, "Sessiya tokeni mos kelmadi.");

            if (process.Session.Device?.SerialNumber != dto.SerialNumber)
                return GenericDto<ProcessControlResultDto>.Error(403, "Qurilma bu jarayonga tegishli emas.");

            // Idempotent — allaqachon yakunlangan yoki pauza qilingan bo'lsa qayta o'zgartirmaymiz.
            if (process.Status == ProcessStatus.Ended)
                return GenericDto<ProcessControlResultDto>.Error(400, "Jarayon allaqachon yakunlangan.");

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

            await TouchSessionAsync(process.Session);

            // Balans yechilmaydi — process tugamadi, resume qilinishi mumkin.
            await _notifier.NotifyProcessUpdatedAsync(process.Session.SessionToken, new
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
                var completed = await _processRepo.CompleteProcessAsync(
                    process.Id, process.GivenAmount, ProcessEndReason.DeviceError, endedAt);
                if (completed == 0)
                    continue;

                var deducted = await _billing.DeductForProcessAsync(process.Id);

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

        private static GenericDto<ProcessControlResultDto>? ValidateProcessOwner(ProductProcessEntity? process, long userId)
        {
            if (process is null)
                return GenericDto<ProcessControlResultDto>.Error(404, "Jarayon topilmadi.");

            if (process.Session is null || process.Session.UserId != userId)
                return GenericDto<ProcessControlResultDto>.Error(403, "Bu jarayon sizga tegishli emas.");

            return null;
        }

        private async Task TouchSessionAsync(SessionEntity? session)
        {
            if (session is null) return;
            // Atomic UPDATE — SaveChanges chaqirmaydi, shuning uchun tracker'da modified bo'lib
            // turgan boshqa entitylarni yozishga urinmaydi (race-safe va concurrency-safe).
            await _sessionRepo.TouchAsync(session.Id);
        }
    }
}
