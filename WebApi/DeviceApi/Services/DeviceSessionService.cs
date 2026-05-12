using System.Security.Cryptography;
using System.Text;
using CommonConfiguration.Grpc;
using CommonConfiguration.Messaging;
using Domain.Entities;
using Domain.Enums;
using Domain.Messaging;
using Domain.Messaging.Events;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace DeviceApi.Services
{
    public sealed class DeviceSessionService : IDeviceSessionService
    {
        private readonly IDeviceRepository _deviceRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly PendingSessionService.PendingSessionServiceClient _pendingClient;
        private readonly RabbitMqPublisher _rabbitPublisher;
        private readonly ILogger<DeviceSessionService> _logger;

        public DeviceSessionService(
            IDeviceRepository deviceRepo,
            ISessionRepository sessionRepo,
            PendingSessionService.PendingSessionServiceClient pendingClient,
            RabbitMqPublisher rabbitPublisher,
            ILogger<DeviceSessionService> logger)
        {
            _deviceRepo = deviceRepo;
            _sessionRepo = sessionRepo;
            _pendingClient = pendingClient;
            _rabbitPublisher = rabbitPublisher;
            _logger = logger;
        }

        public async Task<DeviceConnectResult> TryConnectAsync(
            string serialNumber,
            long userId,
            string sessionToken,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return new DeviceConnectResult(false, null, "empty_token");

            GetPendingTokenResponse response;
            try
            {
                response = await _pendingClient.GetPendingTokenAsync(
                    new GetPendingTokenRequest { UserId = userId },
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "gRPC GetPendingToken xatosi userId={UserId}", userId);
                return new DeviceConnectResult(false, null, "pending_service_unavailable");
            }

            if (!response.Exists)
            {
                _logger.LogInformation(
                    "Pending sessiya topilmadi userId={UserId} serial={Serial}", userId, serialNumber);
                return new DeviceConnectResult(false, null, "no_pending_session");
            }

            var cachedBytes = Encoding.UTF8.GetBytes(response.SessionToken);
            var receivedBytes = Encoding.UTF8.GetBytes(sessionToken);
            if (cachedBytes.Length != receivedBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(cachedBytes, receivedBytes))
            {
                _logger.LogWarning(
                    "Token mos kelmadi userId={UserId} serial={Serial}", userId, serialNumber);
                return new DeviceConnectResult(false, null, "token_mismatch");
            }

            var device = await _deviceRepo.GetBySerialNumberAsync(serialNumber);
            if (device is null)
                return new DeviceConnectResult(false, null, "device_unknown");

            // Race condition guard — bir userda allaqachon aktiv DB sessiya bo'lsa, ikkinchisini yaratmaymiz.
            var hasActive = await _sessionRepo.HasActiveAsync(
                userId,
                SessionStatus.Created,
                SessionStatus.Connected,
                SessionStatus.InProcess);

            if (hasActive)
            {
                _logger.LogWarning(
                    "Aktiv sessiya allaqachon mavjud userId={UserId} serial={Serial}", userId, serialNumber);
                return new DeviceConnectResult(false, null, "active_session_exists");
            }

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
                "Sessiya yaratildi sessionId={SessionId} userId={UserId} deviceId={DeviceId}",
                session.Id, userId, device.Id);

            _rabbitPublisher.Publish(QueueNames.EventQueue, new DeviceEvent
            {
                EventType = DeviceEventTypes.Connected,
                SerialNumber = serialNumber,
                SessionToken = sessionToken
            });

            return new DeviceConnectResult(true, session.Id, null);
        }
    }
}
