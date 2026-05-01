using Domain.Dtos.Base;
using Domain.Dtos.Session;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    /// <summary>
    /// Sessiya darajasidagi operatsiyalar — yaratish, qurilma ulanish, yopish.
    /// Mahsulot berish jarayonlari (start/stop/pause/resume/telemetry) <see cref="ProcessService"/> da.
    /// </summary>
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly IUserRepository _userRepo;
        private readonly IProductProcessRepository _processRepo;
        private readonly ISessionNotifier _notifier;
        private readonly IDeviceCommandPublisher _commandPublisher;
        private readonly IDeviceLockService _deviceLock;
        private readonly IBillingService _billing;

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DeviceOfflineThreshold = TimeSpan.FromSeconds(90);

        public SessionService(
            ISessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            IUserRepository userRepo,
            IProductProcessRepository processRepo,
            ISessionNotifier notifier,
            IDeviceCommandPublisher commandPublisher,
            IDeviceLockService deviceLock,
            IBillingService billing)
        {
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _userRepo = userRepo;
            _processRepo = processRepo;
            _notifier = notifier;
            _commandPublisher = commandPublisher;
            _deviceLock = deviceLock;
            _billing = billing;
        }

        public async Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<CreateSessionResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsBlocked)
                return GenericDto<CreateSessionResultDto>.Error(403, "Foydalanuvchi bloklangan.");

            var session = new SessionEntity
            {
                UserId = dto.UserId,
                SessionToken = Guid.NewGuid().ToString("N"),
                Status = SessionStatus.Created,
                CreatedAt = DateTime.Now,
                LastActivityAt = DateTime.Now
            };

            var created = await _sessionRepo.CreateAsync(session);

            return GenericDto<CreateSessionResultDto>.Success(new CreateSessionResultDto
            {
                SessionId = created.Id,
                SessionToken = created.SessionToken,
                ExpiresAt = created.CreatedAt.Add(IdleTimeout),
                ResultMessage = "Sessiya yaratildi. Qurilma QR kodni skanerlab ulanishini kuting."
            });
        }

        public async Task<GenericDto<DeviceConnectedResultDto>> DeviceConnectAsync(DeviceConnectedDto dto)
        {
            var session = await _sessionRepo.GetByTokenAsync(dto.SessionToken);
            if (session is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Status != SessionStatus.Created)
                return GenericDto<DeviceConnectedResultDto>.Error(400, "Sessiya allaqachon ulangan yoki yopilgan.");

            var device = await _deviceRepo.GetBySerialNumberAsync(dto.SerialNumber);
            if (device is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Qurilma topilmadi yoki faol emas.");

            session.DeviceId = device.Id;
            session.Status = SessionStatus.Connected;
            session.ConnectedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepo.UpdateAsync(session);

            var capabilities = (device.Products ?? Enumerable.Empty<ProductEntity>())
                .Where(p => p.IsActive)
                .Select(p => new DeviceProductCapabilityDto
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Type = p.Type.ToString(),
                    Unit = p.Unit.ToString(),
                    Price = p.Price
                })
                .ToList();

            var result = new DeviceConnectedResultDto
            {
                SessionId = session.Id,
                DeviceId = device.Id,
                DeviceSerialNumber = device.SerialNumber,
                DeviceType = device.DeviceType.ToString(),
                Products = capabilities,
                ResultMessage = "Qurilma sessiyaga ulandi. Mahsulot tanlash uchun /Process/Start ga murojaat qiling."
            };

            await _notifier.NotifyDeviceConnectedAsync(session.SessionToken, new
            {
                session_id = session.Id,
                device_id = device.Id,
                serial_number = device.SerialNumber,
                device_type = device.DeviceType.ToString(),
                products = capabilities,
                connected_at = session.ConnectedAt
            });

            return GenericDto<DeviceConnectedResultDto>.Success(result);
        }

        public async Task<GenericDto<CloseSessionResultDto>> CloseSessionByUserAsync(CloseSessionDto dto)
        {
            var session = await _sessionRepo.GetByIdWithProcessesAsync(dto.SessionId);
            if (session is null)
                return GenericDto<CloseSessionResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.UserId != dto.UserId)
                return GenericDto<CloseSessionResultDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            if (session.Status == SessionStatus.Closed)
                return GenericDto<CloseSessionResultDto>.Error(400, "Sessiya allaqachon yopilgan.");

            var (totalDelivered, totalCost) = await FinalizeOpenProcessesAsync(
                session, ProcessEndReason.UserStopped, sendStopCommand: true);

            session.Status = SessionStatus.Closed;
            session.CloseReason = SessionCloseReason.UserClosed;
            session.ClosedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            await _notifier.NotifySessionClosedAsync(session.SessionToken, new
            {
                reason = nameof(SessionCloseReason.UserClosed),
                total_delivered = totalDelivered,
                total_cost = totalCost,
                closed_at = session.ClosedAt
            });

            return GenericDto<CloseSessionResultDto>.Success(new CloseSessionResultDto
            {
                ResultMessage = "Sessiya muvaffaqiyatli yopildi.",
                TotalDelivered = totalDelivered,
                TotalCost = totalCost
            });
        }

        public async Task CloseTimedOutSessionsAsync()
        {
            var idleBefore = DateTime.Now.Subtract(IdleTimeout);
            var idleSessions = await _sessionRepo.GetIdleSessionsAsync(idleBefore);

            foreach (var session in idleSessions)
            {
                var (totalDelivered, totalCost) = await FinalizeOpenProcessesAsync(
                    session, ProcessEndReason.DeviceError, sendStopCommand: true);

                session.Status = SessionStatus.Closed;
                session.CloseReason = SessionCloseReason.Timeout;
                session.ClosedAt = DateTime.Now;
                await _sessionRepo.UpdateAsync(session);

                await _notifier.NotifySessionClosedAsync(session.SessionToken, new
                {
                    reason = nameof(SessionCloseReason.Timeout),
                    total_delivered = totalDelivered,
                    total_cost = totalCost,
                    closed_at = session.ClosedAt
                });
            }
        }

        public async Task CloseOfflineDeviceSessionsAsync()
        {
            var threshold = DateTime.Now.Subtract(DeviceOfflineThreshold);
            var staleDevices = await _deviceRepo.GetStaleOnlineDevicesAsync(threshold);

            foreach (var device in staleDevices)
            {
                var activeSessions = await _sessionRepo.GetActiveSessionsForDeviceAsync(device.Id);
                foreach (var session in activeSessions)
                {
                    var (totalDelivered, totalCost) = await FinalizeOpenProcessesAsync(
                        session, ProcessEndReason.DeviceError, sendStopCommand: false);

                    session.Status = SessionStatus.Closed;
                    session.CloseReason = SessionCloseReason.DeviceLost;
                    session.ClosedAt = DateTime.Now;
                    session.LastActivityAt = DateTime.Now;
                    await _sessionRepo.UpdateAsync(session);

                    await _notifier.NotifySessionUpdatedAsync(session.SessionToken, new
                    {
                        session_id = session.Id,
                        status = SessionStatus.Closed.ToString(),
                        reason = nameof(SessionCloseReason.DeviceLost),
                        total_delivered = totalDelivered,
                        total_cost = totalCost,
                        closed_at = session.ClosedAt
                    });
                }

                device.IsOnline = false;
                await _deviceRepo.UpdateAsync(device);
            }
        }

        /// <summary>
        /// Sessiya yopilayotganda hali tugamagan barcha jarayonlarni yakunlaydi:
        /// qurilmaga stop yuboradi, statusni Ended ga o'tkazadi, balansni yechadi va lockni bo'shatadi.
        /// </summary>
        private async Task<(decimal totalDelivered, decimal totalCost)> FinalizeOpenProcessesAsync(
            SessionEntity session,
            ProcessEndReason endReason,
            bool sendStopCommand)
        {
            decimal totalDelivered = 0;
            decimal totalCost = 0;

            if (session.Processes is null || session.Processes.Count == 0)
                return (totalDelivered, totalCost);

            foreach (var process in session.Processes)
            {
                if (process.Status == ProcessStatus.Ended)
                {
                    totalDelivered += process.GivenAmount;
                    totalCost += process.GivenAmount * process.PricePerUnit;
                    continue;
                }

                if (sendStopCommand && session.Device is not null)
                {
                    _commandPublisher.PublishStop(session.Device.SerialNumber, process.Id);
                }

                process.Status = ProcessStatus.Ended;
                process.EndReason = endReason;
                process.EndedAt = DateTime.Now;
                await _processRepo.UpdateAsync(process);

                var deducted = await _billing.DeductForProcessAsync(process.Id);

                totalDelivered += process.GivenAmount;
                totalCost += deducted;

                if (session.Device is not null)
                    await _deviceLock.UnlockDeviceAsync(session.Device.SerialNumber, session.UserId);

                await _notifier.NotifyProcessEndedAsync(session.SessionToken, new
                {
                    process_id = process.Id,
                    end_reason = endReason.ToString(),
                    total_delivered = process.GivenAmount,
                    total_cost = deducted,
                    ended_at = process.EndedAt
                });
            }

            return (totalDelivered, totalCost);
        }
    }
}
