using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace SessionApi.Services
{
    public sealed class DeviceSessionService : IDeviceSessionService
    {
        private readonly IDeviceRepository _deviceRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly IPendingSessionStore _pendingStore;
        private readonly ISessionService _sessionService;
        private readonly ILogger<DeviceSessionService> _logger;

        public DeviceSessionService(
            IDeviceRepository deviceRepo,
            ISessionRepository sessionRepo,
            IPendingSessionStore pendingStore,
            ISessionService sessionService,
            ILogger<DeviceSessionService> logger)
        {
            _deviceRepo = deviceRepo;
            _sessionRepo = sessionRepo;
            _pendingStore = pendingStore;
            _sessionService = sessionService;
            _logger = logger;
        }

        public async Task<DeviceConnectResult> TryConnectAsync(
            string serialNumber,
            long userId,
            string sessionToken,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "[CONNECT] Boshlandi: serial={Serial} userId={UserId} tokenLen={Len}",
                serialNumber, userId, sessionToken?.Length ?? 0);

            try
            {
                if (string.IsNullOrEmpty(sessionToken))
                {
                    _logger.LogWarning("[CONNECT] Step 0 — session_token bo'sh.");
                    return Fail(ConnectResultCodes.InvalidPayload);
                }

                // ── Step 1: Pending sessiya cache'idan tokenni olish ──
                _logger.LogInformation(
                    "[CONNECT] Step 1 — pending store: GetAsync(userId={UserId})", userId);

                var entry = await _pendingStore.GetAsync(userId);
                if (entry is null)
                {
                    _logger.LogWarning(
                        "[CONNECT] Step 1 — pending sessiya topilmadi userId={UserId}", userId);
                    return Fail(ConnectResultCodes.NoPendingSession);
                }

                _logger.LogInformation(
                    "[CONNECT] Step 1 — cache topildi cachedTokenLen={Len}",
                    entry.SessionToken?.Length ?? 0);

                // ── Step 2: Constant-time token comparison ──
                _logger.LogInformation("[CONNECT] Step 2 — token solishtirilmoqda.");

                var cachedBytes = Encoding.UTF8.GetBytes(entry.SessionToken ?? string.Empty);
                var receivedBytes = Encoding.UTF8.GetBytes(sessionToken);
                if (cachedBytes.Length != receivedBytes.Length ||
                    !CryptographicOperations.FixedTimeEquals(cachedBytes, receivedBytes))
                {
                    _logger.LogWarning(
                        "[CONNECT] Step 2 — token mos kelmadi userId={UserId} serial={Serial}",
                        userId, serialNumber);
                    return Fail(ConnectResultCodes.TokenMismatch);
                }

                _logger.LogInformation("[CONNECT] Step 2 — token mos keldi.");

                // ── Step 3: Qurilmani topish ──
                _logger.LogInformation("[CONNECT] Step 3 — device repo: GetBySerialNumberAsync({Serial})", serialNumber);

                var device = await _deviceRepo.GetBySerialNumberAsync(serialNumber);
                if (device is null)
                {
                    _logger.LogWarning(
                        "[CONNECT] Step 3 — qurilma topilmadi serial={Serial}", serialNumber);
                    return Fail(ConnectResultCodes.DeviceUnknown);
                }

                _logger.LogInformation(
                    "[CONNECT] Step 3 — qurilma topildi deviceId={DeviceId} type={Type}",
                    device.Id, device.DeviceType);

                // ── Step 4: Race condition guard ──
                _logger.LogInformation("[CONNECT] Step 4 — HasActiveAsync tekshiruvi userId={UserId}", userId);

                var hasActive = await _sessionRepo.HasActiveAsync(
                    userId,
                    SessionStatus.Created,
                    SessionStatus.Connected,
                    SessionStatus.InProcess);

                if (hasActive)
                {
                    _logger.LogWarning(
                        "[CONNECT] Step 4 — aktiv DB sessiya allaqachon mavjud userId={UserId}", userId);
                    return Fail(ConnectResultCodes.ActiveSessionExists);
                }

                _logger.LogInformation("[CONNECT] Step 4 — aktiv sessiya yo'q, davom etamiz.");

                // ── Step 5: DB'ga sessiya yozish ──
                var now = DateTime.Now;
                var session = new SessionEntity
                {
                    UserId = userId,
                    DeviceId = device.Id,
                    SessionToken = sessionToken,
                    Status = SessionStatus.Connected,
                    CreatedAt = now,
                    ConnectedAt = now,
                    LastActivityAt = now
                };

                await _sessionRepo.CreateAsync(session);

                _logger.LogInformation(
                    "[CONNECT] Step 5 — DB'da sessiya yaratildi sessionId={SessionId} userId={UserId} deviceId={DeviceId}",
                    session.Id, userId, device.Id);

                // ── Step 6: SignalR push mobile'ga (RabbitMQ oraliq hop emas — to'g'ridan-to'g'ri) ──
                var notifyResult = await _sessionService.NotifyDeviceConnectedAsync(sessionToken);
                if (!notifyResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "[CONNECT] Step 6 — NotifyDeviceConnected muvaffaqiyatsiz sessionId={SessionId}: {Err}",
                        session.Id, notifyResult.ErrorObj?.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation(
                        "[CONNECT] Step 6 — SignalR push yuborildi sessionId={SessionId}", session.Id);
                }

                return new DeviceConnectResult(
                    Success: true,
                    Code: ConnectResultCodes.Success,
                    Message: ConnectResultMessages.For(ConnectResultCodes.Success),
                    SessionId: session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[CONNECT] Kutilmagan xatolik serial={Serial} userId={UserId}",
                    serialNumber, userId);
                return Fail(ConnectResultCodes.InternalError);
            }
        }

        private static DeviceConnectResult Fail(string code)
            => new(Success: false, Code: code, Message: ConnectResultMessages.For(code), SessionId: null);
    }
}
