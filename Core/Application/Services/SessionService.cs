using Application.Hubs;
using Domain.Dtos.Base;
using Domain.Dtos.Session;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace Application.Services
{
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly ISessionProgressRepository _progressRepository;
        private readonly IHubContext<SessionHub> _hubContext;

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

        public SessionService(
            ISessionRepository sessionRepository,
            ISessionProgressRepository progressRepository,
            IHubContext<SessionHub> hubContext)
        {
            _sessionRepository = sessionRepository;
            _progressRepository = progressRepository;
            _hubContext = hubContext;
        }

        public async Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto)
        {
            var session = new UsageSessionEntity
            {
                UserId = dto.UserId,
                SessionToken = Guid.NewGuid().ToString("N"),
                Status = SessionStatus.Pending,
                StartedAt = DateTime.Now,
                LastActivityAt = DateTime.Now
            };

            var created = await _sessionRepository.CreateAsync(session);

            return GenericDto<CreateSessionResultDto>.Success(new CreateSessionResultDto
            {
                SessionId = created.Id,
                SessionToken = created.SessionToken,
                ExpiresAt = created.StartedAt.Add(IdleTimeout),
                ResultMessage = "Sessiya muvaffaqiyatli yaratildi."
            });
        }

        public async Task<GenericDto<DeviceConnectedResultDto>> DeviceConnectAsync(DeviceConnectedDto dto)
        {
            var session = await _sessionRepository.GetByTokenAsync(dto.SessionToken);
            if (session is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Status != SessionStatus.Pending)
                return GenericDto<DeviceConnectedResultDto>.Error(400, "Sessiya pending holatida emas.");

            session.DeviceId = dto.DeviceId;
            session.ProductType = dto.ProductType;
            session.Status = SessionStatus.DeviceConnected;
            session.DeviceConnectedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);

            await _hubContext.Clients
                .Group(session.SessionToken)
                .SendAsync("DeviceConnected", new
                {
                    device_id = dto.DeviceId,
                    product_type = dto.ProductType,
                    connected_at = session.DeviceConnectedAt
                });

            return GenericDto<DeviceConnectedResultDto>.Success(new DeviceConnectedResultDto
            {
                SessionId = session.Id,
                ResultMessage = "Device sessiyaga muvaffaqiyatli ulandi."
            });
        }

        public async Task<GenericDto<SessionProgressResultDto>> ReportProgressAsync(SessionProgressDto dto)
        {
            var session = await _sessionRepository.GetByTokenAsync(dto.SessionToken);
            if (session is null)
                return GenericDto<SessionProgressResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Status == SessionStatus.DeviceConnected)
                session.Status = SessionStatus.InProgress;

            if (session.Status != SessionStatus.InProgress)
                return GenericDto<SessionProgressResultDto>.Error(400, "Sessiya aktiv holatda emas.");

            var progress = new SessionProgressEntity
            {
                SessionId = session.Id,
                Quantity = dto.Quantity,
                TotalQuantity = dto.TotalQuantity,
                ReportedAt = DateTime.Now
            };
            await _progressRepository.CreateAsync(progress);

            session.DeliveredQuantity = dto.TotalQuantity;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepository.UpdateAsync(session);

            await _hubContext.Clients
                .Group(session.SessionToken)
                .SendAsync("ProgressUpdate", new
                {
                    quantity = dto.Quantity,
                    total_quantity = dto.TotalQuantity,
                    product_type = session.ProductType
                });

            return GenericDto<SessionProgressResultDto>.Success(new SessionProgressResultDto
            {
                ResultMessage = "Progress qabul qilindi."
            });
        }

        public async Task<GenericDto<DeviceFinishResultDto>> DeviceFinishAsync(DeviceFinishDto dto)
        {
            var session = await _sessionRepository.GetByTokenAsync(dto.SessionToken);
            if (session is null)
                return GenericDto<DeviceFinishResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Status != SessionStatus.InProgress && session.Status != SessionStatus.DeviceConnected)
                return GenericDto<DeviceFinishResultDto>.Error(400, "Sessiya aktiv holatda emas.");

            session.Status = SessionStatus.Completed;
            session.DeliveredQuantity = dto.FinalQuantity;
            session.EndedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);

            await _hubContext.Clients
                .Group(session.SessionToken)
                .SendAsync("SessionCompleted", new
                {
                    total_delivered = session.DeliveredQuantity,
                    product_type = session.ProductType,
                    ended_at = session.EndedAt
                });

            return GenericDto<DeviceFinishResultDto>.Success(new DeviceFinishResultDto
            {
                ResultMessage = "Sessiya muvaffaqiyatli tugallandi.",
                TotalDelivered = session.DeliveredQuantity
            });
        }

        public async Task<GenericDto<CloseSessionResultDto>> CloseSessionByUserAsync(CloseSessionDto dto)
        {
            var session = await _sessionRepository.GetByIdAsync(dto.SessionId);
            if (session is null)
                return GenericDto<CloseSessionResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.UserId != dto.UserId)
                return GenericDto<CloseSessionResultDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            if (session.Status == SessionStatus.Completed ||
                session.Status == SessionStatus.ClosedByUser ||
                session.Status == SessionStatus.TimedOut)
                return GenericDto<CloseSessionResultDto>.Error(400, "Sessiya allaqachon yopilgan.");

            session.Status = SessionStatus.ClosedByUser;
            session.EndedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);

            await _hubContext.Clients
                .Group(session.SessionToken)
                .SendAsync("SessionClosed", new
                {
                    reason = "closed_by_user",
                    total_delivered = session.DeliveredQuantity,
                    ended_at = session.EndedAt
                });

            return GenericDto<CloseSessionResultDto>.Success(new CloseSessionResultDto
            {
                ResultMessage = "Sessiya muvaffaqiyatli yopildi.",
                TotalDelivered = session.DeliveredQuantity
            });
        }

        public async Task CloseTimedOutSessionsAsync()
        {
            var idleBefore = DateTime.Now.Subtract(IdleTimeout);
            var idleSessions = await _sessionRepository.GetIdleSessionsAsync(idleBefore);

            foreach (var session in idleSessions)
            {
                session.Status = SessionStatus.TimedOut;
                session.EndedAt = DateTime.Now;

                await _sessionRepository.UpdateAsync(session);

                await _hubContext.Clients
                    .Group(session.SessionToken)
                    .SendAsync("SessionClosed", new
                    {
                        reason = "timed_out",
                        total_delivered = session.DeliveredQuantity,
                        ended_at = session.EndedAt
                    });
            }
        }
    }
}
