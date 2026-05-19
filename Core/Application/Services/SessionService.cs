using System.Security.Cryptography;
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
        private readonly IPushNotificationService _push;
        private readonly IPendingSessionStore _pendingStore;

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PendingSessionTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DeviceOfflineThreshold = TimeSpan.FromSeconds(90);

        public SessionService(
            ISessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            IUserRepository userRepo,
            IProductProcessRepository processRepo,
            ISessionNotifier notifier,
            IDeviceCommandPublisher commandPublisher,
            IDeviceLockService deviceLock,
            IBillingService billing,
            IPushNotificationService push,
            IPendingSessionStore pendingStore)
        {
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _userRepo = userRepo;
            _processRepo = processRepo;
            _notifier = notifier;
            _commandPublisher = commandPublisher;
            _deviceLock = deviceLock;
            _billing = billing;
            _push = push;
            _pendingStore = pendingStore;
        }

        public async Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<CreateSessionResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsBlocked)
                return GenericDto<CreateSessionResultDto>.Error(403, "Foydalanuvchi bloklangan.");

            // DB'da aktiv sessiya bor bo'lsa — yangi pending yaratmaymiz (avval yopish kerak).
            var hasActive = await _sessionRepo.HasActiveAsync(
                dto.UserId,
                SessionStatus.Created,
                SessionStatus.Connected,
                SessionStatus.InProcess);

            if (hasActive)
                return GenericDto<CreateSessionResultDto>.Error(409, "Sizda allaqachon faol sessiya bor. Avval uni yoping.");

            // Pending cache'da bo'lsa — xuddi shu tokenni qaytarish (idempotent retry).
            var existing = await _pendingStore.GetAsync(dto.UserId);
            if (existing is not null)
            {
                return GenericDto<CreateSessionResultDto>.Success(new CreateSessionResultDto
                {
                    UserId = dto.UserId,
                    SessionToken = existing.SessionToken,
                    IdleAfter = existing.ExpiresAt,
                    ResultMessage = "Mavjud pending sessiya tokeni qaytarildi."
                });
            }

            var token = GenerateSessionToken();
            await _pendingStore.SetAsync(dto.UserId, token, PendingSessionTtl);

            return GenericDto<CreateSessionResultDto>.Success(new CreateSessionResultDto
            {
                UserId = dto.UserId,
                SessionToken = token,
                IdleAfter = DateTime.Now.Add(PendingSessionTtl),
                ResultMessage = "Pending sessiya yaratildi. QR ni qurilmaga ko'rsating."
            });
        }

        private static string GenerateSessionToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(24);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public async Task<GenericDto<DeviceConnectedResultDto>> NotifyDeviceConnectedAsync(string sessionToken)
        {
            var session = await _sessionRepo.GetByTokenAsync(sessionToken);
            if (session is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Device is null)
                return GenericDto<DeviceConnectedResultDto>.Error(500, "Sessiya qurilmasiz ko'rinadi.");

            // Mahsulotlarni alohida yuklash kerak — GetByTokenAsync Device.Products'ni include qilmaydi.
            var device = await _deviceRepo.GetBySerialNumberAsync(session.Device.SerialNumber);
            if (device is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Qurilma topilmadi yoki faol emas.");

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
                ResultMessage = "Qurilma sessiyaga ulandi."
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

            // Pending cache shu paytdan keraksiz — TTL bilan o'chmasin, darhol tozalaymiz.
            await _pendingStore.DeleteAsync(session.UserId);

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

                // App background/yopiq bo'lsa SignalR yetib bormaydi — push xabar.
                await _push.SendAsync(session.UserId, new PushNotification
                {
                    Title = "Sessiya yopildi",
                    Body = $"Sessiyangiz harakatsizlik tufayli yopildi. Jami: {totalCost:N2}",
                    DeepLink = $"botenergy://sessions/{session.Id}"
                });
            }
        }

        public async Task<GenericDto<CurrentSessionDto?>> GetCurrentAsync(long userId)
        {
            var session = await _sessionRepo.GetActiveByUserAsync(userId);
            if (session is null)
                return GenericDto<CurrentSessionDto?>.Success(null);

            return GenericDto<CurrentSessionDto?>.Success(MapToCurrent(session));
        }

        public async Task<GenericDto<CurrentSessionDto>> GetByIdAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdWithProcessesAsync(sessionId);
            if (session is null)
                return GenericDto<CurrentSessionDto>.Error(404, "Sessiya topilmadi.");

            if (session.UserId != userId)
                return GenericDto<CurrentSessionDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            return GenericDto<CurrentSessionDto>.Success(MapToCurrent(session)!);
        }

        public async Task<GenericDto<HeartbeatResultDto>> HeartbeatAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session is null)
                return GenericDto<HeartbeatResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.UserId != userId)
                return GenericDto<HeartbeatResultDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            if (session.Status == SessionStatus.Closed)
                return GenericDto<HeartbeatResultDto>.Error(400, "Sessiya yopilgan, heartbeat qabul qilinmaydi.");

            var affected = await _sessionRepo.TouchAsync(sessionId);
            if (affected == 0)
                return GenericDto<HeartbeatResultDto>.Error(409, "Sessiya holati o'zgargan.");

            var now = DateTime.Now;
            return GenericDto<HeartbeatResultDto>.Success(new HeartbeatResultDto
            {
                SessionId = sessionId,
                LastActivityAt = now,
                IdleAfter = now.Add(IdleTimeout)
            });
        }

        public async Task<GenericDto<PagedResult<SessionHistoryItemDto>>> GetHistoryAsync(long userId, PaginationParams pagination, DateTime? from, DateTime? to)
        {
            var page = await _sessionRepo.GetHistoryByUserAsync(userId, pagination, from, to);

            return GenericDto<PagedResult<SessionHistoryItemDto>>.Success(page.Map(s => new SessionHistoryItemDto
            {
                SessionId = s.Id,
                Status = s.Status.ToString(),
                CloseReason = s.CloseReason?.ToString(),
                DeviceSerialNumber = s.Device?.SerialNumber,
                CreatedAt = s.CreatedAt,
                ClosedAt = s.ClosedAt
            }));
        }

        internal CurrentSessionDto? MapToCurrent(SessionEntity? session)
        {
            if (session is null) return null;

            var activeProcess = session.Processes?
                .Where(p => p.Status != ProcessStatus.Ended)
                .OrderByDescending(p => p.StartedAt)
                .FirstOrDefault();

            return new CurrentSessionDto
            {
                SessionId = session.Id,
                SessionToken = session.SessionToken,
                Status = session.Status.ToString(),
                CreatedAt = session.CreatedAt,
                ConnectedAt = session.ConnectedAt,
                LastActivityAt = session.LastActivityAt,
                IdleAfter = session.LastActivityAt.Add(IdleTimeout),
                Device = session.Device is null ? null : new CurrentSessionDeviceDto
                {
                    DeviceId = session.Device.Id,
                    SerialNumber = session.Device.SerialNumber,
                    DeviceType = session.Device.DeviceType.ToString(),
                    Model = session.Device.Model,
                    IsOnline = session.Device.IsOnline,
                    LastSeenAt = session.Device.LastSeenAt,
                    Products = (session.Device.Products ?? Enumerable.Empty<ProductEntity>())
                        .Where(p => p.IsActive)
                        .Select(p => new DeviceProductCapabilityDto
                        {
                            ProductId = p.Id,
                            Name = p.Name,
                            Type = p.Type.ToString(),
                            Unit = p.Unit.ToString(),
                            Price = p.Price
                        })
                        .ToList()
                },
                ActiveProcess = activeProcess is null ? null : new CurrentSessionProcessDto
                {
                    ProcessId = activeProcess.Id,
                    ProductId = activeProcess.ProductId,
                    ProductName = activeProcess.ProductName,
                    Unit = activeProcess.Unit.ToString(),
                    PricePerUnit = activeProcess.PricePerUnit,
                    RequestedAmount = activeProcess.RequestedAmount,
                    GivenAmount = activeProcess.GivenAmount,
                    CurrentCost = activeProcess.GivenAmount * activeProcess.PricePerUnit,
                    Status = activeProcess.Status.ToString(),
                    StartedAt = activeProcess.StartedAt,
                    PausedAt = activeProcess.PausedAt
                }
            };
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

                    await _push.SendAsync(session.UserId, new PushNotification
                    {
                        Title = "Qurilma bilan aloqa uzildi",
                        Body = $"Qurilma {device.SerialNumber} javob bermayapti. Sessiya yopildi. Jami: {totalCost:N2}",
                        DeepLink = $"botenergy://sessions/{session.Id}"
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
                    total_given = process.GivenAmount,
                    total_cost = deducted,
                    ended_at = process.EndedAt
                });
            }

            return (totalDelivered, totalCost);
        }
    }
}
