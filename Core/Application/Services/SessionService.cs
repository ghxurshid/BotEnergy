using Domain.Dtos.Base;
using Domain.Dtos.Session;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISessionNotifier _notifier;

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

        public SessionService(
            ISessionRepository sessionRepository,
            IDeviceRepository deviceRepository,
            IProductRepository productRepository,
            IUserRepository userRepository,
            ISessionNotifier notifier)
        {
            _sessionRepository = sessionRepository;
            _deviceRepository = deviceRepository;
            _productRepository = productRepository;
            _userRepository = userRepository;
            _notifier = notifier;
        }

        /// <summary>
        /// 1-qadam: Bo'sh sessiya yaratish. Faqat user info bilan.
        /// Product va miqdor keyinroq to'ldiriladi.
        /// </summary>
        public async Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto)
        {
            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<CreateSessionResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            if (user.IsBlocked)
                return GenericDto<CreateSessionResultDto>.Error(403, "Foydalanuvchi bloklangan.");

            var session = new UsageSessionEntity
            {
                UserId = dto.UserId,
                SessionToken = Guid.NewGuid().ToString("N"),
                Status = SessionStatus.Pending,
                StartedAt = DateTime.Now,
                LastActivityAt = DateTime.Now,
                UserPhoneNumber = user.PhoneNumber,
            };

            var created = await _sessionRepository.CreateAsync(session);

            return GenericDto<CreateSessionResultDto>.Success(new CreateSessionResultDto
            {
                SessionId = created.Id,
                SessionToken = created.SessionToken,
                ExpiresAt = created.StartedAt.Add(IdleTimeout),
                ResultMessage = "Sessiya yaratildi. Qurilma QR kodni skanerlab ulanishini kuting."
            });
        }

        /// <summary>
        /// 2-qadam: Qurilma QR kodni skanerlab sessiyaga ulanadi.
        /// Qurilmaning birinchi aktiv product i sessiyaga avtomatik yoziladi.
        /// </summary>
        public async Task<GenericDto<DeviceConnectedResultDto>> DeviceConnectAsync(DeviceConnectedDto dto)
        {
            var session = await _sessionRepository.GetByTokenAsync(dto.SessionToken);
            if (session is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Status != SessionStatus.Pending)
                return GenericDto<DeviceConnectedResultDto>.Error(400, "Sessiya pending holatida emas.");

            var device = await _deviceRepository.GetBySerialNumberAsync(dto.SerialNumber);
            if (device is null)
                return GenericDto<DeviceConnectedResultDto>.Error(404, "Qurilma topilmadi yoki faol emas.");

            var product = device.Products?.FirstOrDefault();
            if (product is null)
                return GenericDto<DeviceConnectedResultDto>.Error(400, "Qurilmada aktiv mahsulot topilmadi.");

            session.DeviceId = device.Id;
            session.DeviceSerialNumber = device.SerialNumber;
            session.ProductId = product.Id;
            session.ProductName = product.Name;
            session.PricePerUnit = product.Price;
            session.Unit = product.Unit;
            session.Status = SessionStatus.DeviceConnected;
            session.DeviceConnectedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);

            await _notifier.NotifyDeviceConnectedAsync(session.SessionToken, new
            {
                device_id = device.Id,
                serial_number = device.SerialNumber,
                product_id = product.Id,
                product_name = product.Name,
                unit = product.Unit.ToString(),
                price_per_unit = product.Price,
                connected_at = session.DeviceConnectedAt
            });

            return GenericDto<DeviceConnectedResultDto>.Success(new DeviceConnectedResultDto
            {
                SessionId = session.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit.ToString(),
                PricePerUnit = product.Price,
                DeviceSerialNumber = device.SerialNumber,
                ResultMessage = "Qurilma sessiyaga ulandi. Endi miqdor belgilash uchun /SetQuantity ga murojaat qiling."
            });
        }

        /// <summary>
        /// 3-qadam: Miqdor belgilash. Ikkala tomon ulangandan keyin user miqdor belgilaydi.
        /// Balans tekshiriladi, limit hisoblanadi.
        /// </summary>
        public async Task<GenericDto<SetQuantityResultDto>> SetQuantityAsync(SetQuantityDto dto)
        {
            var session = await _sessionRepository.GetByIdAsync(dto.SessionId);
            if (session is null)
                return GenericDto<SetQuantityResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.UserId != dto.UserId)
                return GenericDto<SetQuantityResultDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            if (session.Status != SessionStatus.DeviceConnected)
                return GenericDto<SetQuantityResultDto>.Error(400, "Qurilma hali ulanmagan yoki sessiya allaqachon boshlangan.");

            if (session.ProductId is null)
                return GenericDto<SetQuantityResultDto>.Error(400, "Mahsulot hali belgilanmagan.");

            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user is null)
                return GenericDto<SetQuantityResultDto>.Error(404, "Foydalanuvchi topilmadi.");

            decimal availableBalance = GetUserBalance(user);
            decimal maxQuantity = session.PricePerUnit > 0
                ? availableBalance / session.PricePerUnit
                : 0;

            decimal limit = dto.RequestedQuantity.HasValue
                ? Math.Min(dto.RequestedQuantity.Value, maxQuantity)
                : maxQuantity;

            if (limit <= 0)
                return GenericDto<SetQuantityResultDto>.Error(400, "Balans yetarli emas.");

            session.RequestedQuantity = limit;
            session.Status = SessionStatus.InProgress;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);

            return GenericDto<SetQuantityResultDto>.Success(new SetQuantityResultDto
            {
                LimitQuantity = limit,
                ProductName = session.ProductName,
                Unit = session.Unit?.ToString() ?? string.Empty,
                PricePerUnit = session.PricePerUnit,
                DeviceSerialNumber = session.DeviceSerialNumber,
                ProductId = session.ProductId ?? 0,
                ResultMessage = "Miqdor belgilandi. Qurilmaga start buyrug'i yuborildi."
            });
        }

        public async Task<GenericDto<SessionProgressResultDto>> ReportProgressAsync(SessionProgressDto dto)
        {
            var session = await _sessionRepository.GetByTokenAsync(dto.SessionToken);
            if (session is null)
                return GenericDto<SessionProgressResultDto>.Error(404, "Sessiya topilmadi.");

            if (session.Device?.SerialNumber != dto.SerialNumber)
                return GenericDto<SessionProgressResultDto>.Error(403, "Qurilma bu sessiyaga tegishli emas.");

            if (session.Status != SessionStatus.InProgress)
                return GenericDto<SessionProgressResultDto>.Error(400, "Sessiya aktiv holatda emas.");

            session.DeliveredQuantity += dto.Quantity;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepository.UpdateAsync(session);

            await _notifier.NotifyProgressUpdateAsync(session.SessionToken, new
            {
                quantity = dto.Quantity,
                total_quantity = session.DeliveredQuantity,
                product_id = session.ProductId,
                unit = session.Unit?.ToString(),
                price_per_unit = session.PricePerUnit,
                current_cost = session.DeliveredQuantity * session.PricePerUnit
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

            if (session.Device?.SerialNumber != dto.SerialNumber)
                return GenericDto<DeviceFinishResultDto>.Error(403, "Qurilma bu sessiyaga tegishli emas.");

            if (session.Status != SessionStatus.InProgress && session.Status != SessionStatus.DeviceConnected)
                return GenericDto<DeviceFinishResultDto>.Error(400, "Sessiya aktiv holatda emas.");

            session.Status = SessionStatus.Completed;
            session.DeliveredQuantity = dto.FinalQuantity;
            session.EndReason = dto.EndReason;
            session.EndedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);
            await DeductUserBalanceAsync(session);

            await _notifier.NotifySessionCompletedAsync(session.SessionToken, new
            {
                total_delivered = session.DeliveredQuantity,
                product_id = session.ProductId,
                price_per_unit = session.PricePerUnit,
                total_cost = session.DeliveredQuantity * session.PricePerUnit,
                end_reason = session.EndReason,
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
            session.EndReason = "closed_by_user";
            session.EndedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;

            await _sessionRepository.UpdateAsync(session);
            await DeductUserBalanceAsync(session);

            await _notifier.NotifySessionClosedAsync(session.SessionToken, new
            {
                reason = "closed_by_user",
                total_delivered = session.DeliveredQuantity,
                total_cost = session.DeliveredQuantity * session.PricePerUnit,
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
                session.EndReason = "timed_out";
                session.EndedAt = DateTime.Now;

                await _sessionRepository.UpdateAsync(session);
                await DeductUserBalanceAsync(session);

                await _notifier.NotifySessionClosedAsync(session.SessionToken, new
                {
                    reason = "timed_out",
                    total_delivered = session.DeliveredQuantity,
                    total_cost = session.DeliveredQuantity * session.PricePerUnit,
                    ended_at = session.EndedAt
                });
            }
        }

        private decimal GetUserBalance(UserEntity user)
        {
            if (user is NaturalUserEntity natural)
                return natural.Balance;

            if (user is LegalUserEntity legal && legal.Organization is not null)
                return legal.Organization.Balance;

            return 0;
        }

        private async Task DeductUserBalanceAsync(UsageSessionEntity session)
        {
            if (session.DeliveredQuantity <= 0 || session.PricePerUnit <= 0)
                return;

            var cost = session.DeliveredQuantity * session.PricePerUnit;
            var user = await _userRepository.GetByIdAsync(session.UserId);

            if (user is NaturalUserEntity naturalUser)
            {
                naturalUser.Balance = naturalUser.Balance >= cost
                    ? naturalUser.Balance - cost
                    : 0;
                await _userRepository.UpdateUserAsync(naturalUser);
            }
            else if (user is LegalUserEntity legalUser)
            {
                var organization = legalUser.Organization;
                if (organization is null)
                    return;

                organization.Balance = organization.Balance >= cost
                    ? organization.Balance - cost
                    : 0;
                await _userRepository.UpdateUserAsync(legalUser);
            }
        }
    }
}
