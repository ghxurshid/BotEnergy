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

            _commandPublisher.PublishStart(session.Device.SerialNumber, process.Id, product.Id, limit);

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

            // Qurilmaga stop yuboramiz, lekin yakuniy holatni qurilmadan keladigan response/timeout kutmasdan
            // shu yerda ham fix qilamiz — agar device-event keyinchalik kelsa, idempotency saqlanadi.
            var serial = process.Session?.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                _commandPublisher.PublishStop(serial!, process.Id);

            process.Status = ProcessStatus.Ended;
            process.EndReason = ProcessEndReason.UserStopped;
            process.EndedAt = DateTime.Now;
            await _processRepo.UpdateAsync(process);

            var deducted = await _billing.DeductForProcessAsync(process.Id);

            if (!string.IsNullOrWhiteSpace(serial))
                await _deviceLock.UnlockDeviceAsync(serial!, dto.UserId);

            await TouchSessionAsync(process.Session);

            await _notifier.NotifyProcessEndedAsync(process.Session!.SessionToken, new
            {
                process_id = process.Id,
                end_reason = nameof(ProcessEndReason.UserStopped),
                total_delivered = process.GivenAmount,
                total_cost = deducted,
                ended_at = process.EndedAt
            });

            return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
            {
                ProcessId = process.Id,
                Status = process.Status.ToString(),
                ResultMessage = "Jarayon to'xtatildi."
            });
        }

        public async Task<GenericDto<ProcessControlResultDto>> PauseAsync(ProcessControlDto dto)
        {
            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            var validation = ValidateProcessOwner(process, dto.UserId);
            if (validation is not null) return validation;

            if (process!.Status != ProcessStatus.InProcess && process.Status != ProcessStatus.Started)
                return GenericDto<ProcessControlResultDto>.Error(400, "Jarayon pauza qilish uchun mos holatda emas.");

            var serial = process.Session?.Device?.SerialNumber;
            if (!string.IsNullOrWhiteSpace(serial))
                _commandPublisher.PublishPause(serial!, process.Id);

            process.Status = ProcessStatus.Paused;
            process.PausedAt = DateTime.Now;
            await _processRepo.UpdateAsync(process);

            await TouchSessionAsync(process.Session);

            await _notifier.NotifyProcessUpdatedAsync(process.Session!.SessionToken, new
            {
                process_id = process.Id,
                status = process.Status.ToString(),
                paused_at = process.PausedAt
            });

            return GenericDto<ProcessControlResultDto>.Success(new ProcessControlResultDto
            {
                ProcessId = process.Id,
                Status = process.Status.ToString(),
                ResultMessage = "Jarayon pauza qilindi."
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
                _commandPublisher.PublishResume(serial!, process.Id);

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
            if (dto.ProcessId <= 0 || dto.Quantity <= 0)
                return GenericDto<ProcessTelemetryResultDto>.Error(400, "ProcessId va Quantity 0 dan katta bo'lishi shart.");

            var process = await _processRepo.GetByIdWithSessionAsync(dto.ProcessId);
            if (process is null)
                return GenericDto<ProcessTelemetryResultDto>.Error(404, "Jarayon topilmadi.");

            if (process.Session?.SessionToken != dto.SessionToken)
                return GenericDto<ProcessTelemetryResultDto>.Error(403, "Sessiya tokeni mos kelmadi.");

            if (process.Session.Device?.SerialNumber != dto.SerialNumber)
                return GenericDto<ProcessTelemetryResultDto>.Error(403, "Qurilma bu jarayonga tegishli emas.");

            // Atomic + idempotent SQL update.
            var affected = await _processRepo.IncrementGivenAmountAsync(process.Id, dto.Quantity, dto.Sequence);
            if (affected == 0)
                return GenericDto<ProcessTelemetryResultDto>.Success(new ProcessTelemetryResultDto
                {
                    ResultMessage = "Telemetry ignored (duplicate yoki jarayon aktiv emas)."
                });

            // Statusni Started → InProcess ga o'tkazish (birinchi telemetry kelganda).
            if (process.Status == ProcessStatus.Started)
            {
                process.Status = ProcessStatus.InProcess;
                await _processRepo.UpdateAsync(process);
            }

            await TouchSessionAsync(process.Session);

            // Yangi qiymatni o'qish — atomic update natijasi uchun.
            var refreshed = await _processRepo.GetByIdAsync(process.Id);
            var totalGiven = refreshed?.GivenAmount ?? process.GivenAmount + dto.Quantity;

            if (process.Status != ProcessStatus.Ended && totalGiven >= process.RequestedAmount)
            {
                process.GivenAmount = totalGiven;
                process.Status = ProcessStatus.Ended;
                process.EndReason = ProcessEndReason.Completed;
                process.EndedAt = DateTime.Now;
                await _processRepo.UpdateAsync(process);

                var serial = process.Session.Device?.SerialNumber;
                if (!string.IsNullOrWhiteSpace(serial))
                {
                    _commandPublisher.PublishStop(serial!, process.Id);
                    await _deviceLock.UnlockDeviceAsync(serial!, process.Session.UserId);
                }

                var deductedOnAutoComplete = await _billing.DeductForProcessAsync(process.Id);

                await _notifier.NotifyProcessEndedAsync(process.Session.SessionToken, new
                {
                    process_id = process.Id,
                    end_reason = nameof(ProcessEndReason.Completed),
                    total_delivered = process.GivenAmount,
                    total_cost = deductedOnAutoComplete,
                    ended_at = process.EndedAt
                });

                return GenericDto<ProcessTelemetryResultDto>.Success(new ProcessTelemetryResultDto
                {
                    ResultMessage = "Telemetry qabul qilindi va jarayon avtomatik yakunlandi."
                });
            }

            await _notifier.NotifyProcessUpdatedAsync(process.Session.SessionToken, new
            {
                process_id = process.Id,
                quantity = dto.Quantity,
                total_quantity = totalGiven,
                product_id = process.ProductId,
                unit = process.Unit.ToString(),
                price_per_unit = process.PricePerUnit,
                current_cost = totalGiven * process.PricePerUnit
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

            if (dto.FinalQuantity > process.GivenAmount)
                process.GivenAmount = dto.FinalQuantity;

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
                total_delivered = process.GivenAmount,
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
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);
        }
    }
}
