using System.Security.Cryptography;
using Domain.Dtos.Base;
using Domain.Dtos.Session;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

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
        private readonly ICustomerUserRepository _userRepo;
        private readonly IProductProcessRepository _processRepo;
        private readonly ISessionNotifier _notifier;
        private readonly IDeviceCommandPublisher _commandPublisher;
        private readonly IDeviceLockService _deviceLock;
        private readonly IProcessSettlementService _settlement;
        private readonly ISessionPaymentService _payments;
        private readonly IPushNotificationService _push;
        private readonly IPendingSessionStore _pendingStore;
        private readonly IDeviceQrStore _qrStore;
        private readonly IDeviceStatusService _deviceStatus;
        private readonly ITransactionRunner _tx;
        private readonly ILogger<SessionService> _logger;

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PendingSessionTtl = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Qurilma oflayn deb hisoblanadigan jimlik chegarasi. To'siq tekshiruvi bilan BIR XIL
        /// manbadan olinadi — aks holda "buyruq yuborsa bo'ladi" deb hisoblangan qurilma
        /// bu yerda oflayn sanalib, ikki qatlam bir-biriga zid xulosa chiqarardi.
        /// </summary>
        private static readonly TimeSpan DeviceOfflineThreshold = DeviceAvailability.OfflineThreshold;

        public SessionService(
            ISessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            ICustomerUserRepository userRepo,
            IProductProcessRepository processRepo,
            ISessionNotifier notifier,
            IDeviceCommandPublisher commandPublisher,
            IDeviceLockService deviceLock,
            IProcessSettlementService settlement,
            ISessionPaymentService payments,
            IPushNotificationService push,
            IPendingSessionStore pendingStore,
            IDeviceQrStore qrStore,
            IDeviceStatusService deviceStatus,
            ITransactionRunner tx,
            ILogger<SessionService> logger)
        {
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _userRepo = userRepo;
            _processRepo = processRepo;
            _notifier = notifier;
            _commandPublisher = commandPublisher;
            _deviceLock = deviceLock;
            _settlement = settlement;
            _payments = payments;
            _push = push;
            _pendingStore = pendingStore;
            _qrStore = qrStore;
            _deviceStatus = deviceStatus;
            _tx = tx;
            _logger = logger;
        }

        public async Task<GenericDto<CreateSessionResultDto>> CreateSessionAsync(CreateSessionDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.UserId);

            var stop = await StopFactorCheck.For(StopActions.SessionCreate)
                .StopIf(user is null, StopFactors.User.NotFound)
                .StopIf(() => user!.IsBlocked, StopFactors.User.Blocked)
                // DB'da aktiv sessiya bor bo'lsa — yangi pending yaratmaymiz (avval yopish kerak).
                // Paused (device offline) va Settling (hisob-kitob) ham "band" hisoblanadi.
                .StopIfAsync(() => _sessionRepo.HasActiveAsync(
                        dto.UserId,
                        SessionStatus.Created,
                        SessionStatus.Connected,
                        SessionStatus.InProcess,
                        SessionStatus.Paused,
                        SessionStatus.Settling),
                    StopFactors.Session.AlreadyActive)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<CreateSessionResultDto>.Blocked(stop);

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

        /// <summary>
        /// Kolonka ekranidagi bir martalik QR kod uchun amal qilish muddati.
        /// Qisqa muddat — skrinshot bilan boshqa joyda ishlatishga yo'l qo'ymaslik uchun;
        /// qurilma muddati tugashidan oldin yangisini so'rab turadi.
        /// </summary>
        private static readonly TimeSpan DeviceQrTtl = TimeSpan.FromMinutes(2);

        /// <summary>QR kod matnining prefiksi — ilova uni stikerdan ajratadi.</summary>
        public const string DeviceQrPrefix = "BE1:";

        public async Task<GenericDto<DeviceQrDto>> IssueDeviceQrAsync(string serialNumber, int? ttlSeconds = null)
        {
            var serial = (serialNumber ?? string.Empty).Trim();
            var device = string.IsNullOrEmpty(serial) ? null : await _deviceRepo.GetBySerialNumberAsync(serial);

            var stop = StopFactorCheck.For("Session.IssueDeviceQr")
                .StopIf(device is null, StopFactors.Device.NotFound)
                .StopIf(() => !device!.IsActive, StopFactors.Device.Inactive)
                .StopIf(() => device!.Station is null, StopFactors.Device.NoStation)
                .StopIf(() => device!.Station is { IsActive: false }, StopFactors.Station.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<DeviceQrDto>.Blocked(stop);

            var ttl = ttlSeconds is > 0
                ? TimeSpan.FromSeconds(Math.Min(ttlSeconds.Value, 600))
                : DeviceQrTtl;

            var code = GenerateQrCode();
            var expiresAt = DateTime.Now.Add(ttl);

            await _qrStore.SetAsync(code, new DeviceQrEntry(device!.Id, serial, expiresAt), ttl);

            _logger.LogInformation(
                "[QR] Kod berildi serial={Serial} deviceId={DeviceId} ttl={Ttl}s",
                serial, device.Id, (int)ttl.TotalSeconds);

            return GenericDto<DeviceQrDto>.Success(new DeviceQrDto
            {
                Code = code,
                Payload = DeviceQrPrefix + code,
                ExpiresAt = expiresAt,
                TtlSeconds = (int)ttl.TotalSeconds
            });
        }

        public async Task<GenericDto<CurrentSessionDto>> ConnectByQrAsync(ConnectByQrDto dto)
        {
            var raw = (dto.Code ?? string.Empty).Trim();
            if (raw.StartsWith(DeviceQrPrefix, StringComparison.OrdinalIgnoreCase))
                raw = raw[DeviceQrPrefix.Length..];

            if (string.IsNullOrEmpty(raw))
                return GenericDto<CurrentSessionDto>.Blocked(StopFactors.Session.QrInvalid);

            // Kodni oldindan o'qiymiz: mijoz yoki qurilma tekshiruvidan o'tmasa,
            // kod iste'mol qilinmasin (mijoz qayta urinib ko'ra olsin).
            var entry = await _qrStore.GetAsync(raw);
            if (entry is null)
                return GenericDto<CurrentSessionDto>.Blocked(StopFactors.Session.QrInvalid);

            var result = await ConnectByDeviceAsync(new ConnectByDeviceDto
            {
                UserId = dto.UserId,
                SerialNumber = entry.SerialNumber
            });

            if (!result.IsSuccess)
                return result;

            // Sessiya ochildi — kod bir martalik, darhol kuydiriladi.
            await _qrStore.ConsumeAsync(raw);

            _logger.LogInformation(
                "[QR] Kod ishlatildi deviceId={DeviceId} userId={UserId} sessionId={SessionId}",
                entry.DeviceId, dto.UserId, result.Result?.SessionId);

            return result;
        }

        /// <summary>Bir martalik QR kod — 12 belgili URL-xavfsiz tasodifiy matn.</summary>
        private static string GenerateQrCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(9);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Telefon qurilmadagi QR stikerni skanerlaganda sessiya ochish.
        /// Qurilma readeri orqali ulanish bilan bir xil tekshiruvlardan o'tadi
        /// (qurilma bor/faol, stansiya faol, mijoz bloklanmagan, boshqa aktiv
        /// sessiya yo'q) va oxirida xuddi shunday Connected sessiya + to'lov
        /// konteksti qoldiradi.
        /// </summary>
        public async Task<GenericDto<CurrentSessionDto>> ConnectByDeviceAsync(ConnectByDeviceDto dto)
        {
            var serial = (dto.SerialNumber ?? string.Empty).Trim();

            var user = await _userRepo.GetByIdAsync(dto.UserId);
            var device = string.IsNullOrEmpty(serial) ? null : await _deviceRepo.GetBySerialNumberAsync(serial);

            var stop = StopFactorCheck.For("Session.ConnectByDevice")
                .StopIf(user is null, StopFactors.User.NotFound)
                .StopIf(() => user!.IsBlocked, StopFactors.User.Blocked)
                .StopIf(() => device is null, StopFactors.Device.NotFound)
                .StopIf(() => !device!.IsActive, StopFactors.Device.Inactive)
                .StopIf(() => device!.Station is null, StopFactors.Device.NoStation)
                .StopIf(() => device!.Station is { IsActive: false }, StopFactors.Station.Inactive)
                .Result();

            if (stop is not null)
                return GenericDto<CurrentSessionDto>.Blocked(stop);

            // Bitta mijozda bir vaqtda bitta sessiya — qurilma readeri orqali
            // ulanishdagi bilan bir xil qoida.
            var hasActive = await _sessionRepo.HasActiveAsync(
                dto.UserId,
                SessionStatus.Created,
                SessionStatus.Connected,
                SessionStatus.InProcess,
                SessionStatus.Paused,
                SessionStatus.Settling);

            if (hasActive)
                return GenericDto<CurrentSessionDto>.Blocked(StopFactors.Session.AlreadyActive);

            var now = DateTime.Now;
            var session = new SessionEntity
            {
                UserId = dto.UserId,
                DeviceId = device!.Id,
                SessionToken = GenerateSessionToken(),
                Status = SessionStatus.Connected,
                CreatedAt = now,
                ConnectedAt = now,
                LastActivityAt = now
            };

            await _sessionRepo.CreateAsync(session);

            // To'lov konteksti bu yerda yaratilmaydi: uni `SessionPayment/Checkout`
            // birinchi chaqiruvda o'zi ochadi (EnsureForSessionAsync). Shu sabab
            // SessionService faqat SessionApi'da mavjud bo'lgan IPaymentSessionService'ga
            // bog'lanib qolmaydi — u boshqa API'larda ro'yxatdan o'tmagan.

            // Kolonka ekrani QR ni olib tashlab, "mijoz ulandi" holatiga o'tsin.
            try
            {
                await _commandPublisher.PublishSessionAttachedAsync(device.SerialNumber, session.Id, dto.UserId);
                await _qrStore.InvalidateForDeviceAsync(device.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[CONNECT-QR] Qurilmaga session.attached yuborilmadi serial={Serial}", device.SerialNumber);
            }

            // Boshqa qurilmalarda ochilgan ilova nusxalari ham holatni bilsin.
            var notify = await NotifyDeviceConnectedAsync(session.SessionToken);
            if (!notify.IsSuccess)
            {
                _logger.LogWarning("[CONNECT-QR] SignalR xabari yuborilmadi sessionId={SessionId}", session.Id);
            }

            _logger.LogInformation(
                "[CONNECT-QR] Sessiya ochildi sessionId={SessionId} userId={UserId} serial={Serial}",
                session.Id, dto.UserId, serial);

            var current = await _sessionRepo.GetByIdWithProcessesAsync(session.Id) ?? session;
            return GenericDto<CurrentSessionDto>.Success(await MapToCurrentAsync(current));
        }

        public async Task<GenericDto<DeviceConnectedResultDto>> NotifyDeviceConnectedAsync(string sessionToken)
        {
            var found = await _sessionRepo.GetByTokenAsync(sessionToken);

            var stop = StopFactorCheck.For(StopActions.SessionConnect)
                .StopIf(found is null, StopFactors.Session.NotFound)
                .StopIf(() => found!.Device is null, StopFactors.Device.NotAttachedToSession)
                .Result();

            if (stop is not null)
                return GenericDto<DeviceConnectedResultDto>.Blocked(stop);

            var session = found!;

            // Mahsulotlarni alohida yuklash kerak — GetByTokenAsync Device.Products'ni include qilmaydi.
            var device = await _deviceRepo.GetBySerialNumberAsync(session.Device!.SerialNumber);
            if (device is null)
                return GenericDto<DeviceConnectedResultDto>.Blocked(StopFactors.Device.NotFound);

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

            // To'lov konteksti ulanish paytida yaratiladi — ilova mahsulot tanlashdan OLDIN
            // qaysi usul bilan to'lashini bilishi uchun uni shu javobga qo'shamiz.
            var payment = await _payments.GetSnapshotAsync(session.Id);

            var result = new DeviceConnectedResultDto
            {
                SessionId = session.Id,
                DeviceId = device.Id,
                DeviceSerialNumber = device.SerialNumber,
                DeviceType = device.DeviceType.ToString(),
                Products = capabilities,
                Payment = payment,
                ResultMessage = "Qurilma sessiyaga ulandi."
            };

            await _notifier.NotifyDeviceConnectedAsync(session.SessionToken, new
            {
                session_id = session.Id,
                device_id = device.Id,
                serial_number = device.SerialNumber,
                device_type = device.DeviceType.ToString(),
                products = capabilities,
                payment,
                connected_at = session.ConnectedAt
            });

            // Pending cache shu paytdan keraksiz — TTL bilan o'chmasin, darhol tozalaymiz.
            await _pendingStore.DeleteAsync(session.UserId);

            return GenericDto<DeviceConnectedResultDto>.Success(result);
        }

        public async Task<GenericDto<CloseSessionResultDto>> CloseSessionByUserAsync(CloseSessionDto dto)
        {
            var found = await _sessionRepo.GetByIdWithProcessesAsync(dto.SessionId);

            var stop = await StopFactorCheck.For(StopActions.SessionClose)
                .StopIf(found is null, StopFactors.Session.NotFound)
                .StopIf(() => found!.UserId != dto.UserId, StopFactors.Session.NotOwned)
                .StopIf(() => found!.Status == SessionStatus.Closed, StopFactors.Session.Closed)
                .StopIf(() => found!.Status == SessionStatus.Settling, StopFactors.Session.Settling)
                // Aktiv jarayon (Started/InProcess/Paused yoki stop-pending) bo'lsa, sessiyani yopishga
                // ruxsat bermaymiz — avval jarayon qurilma tasdig'i bilan to'liq yakunlanishi kerak.
                // (Background timeout/offline cleaner'lar bundan istisno — ular majburan yopadi.)
                // Paused sessiyada esa yopishga ruxsat beramiz (qurilma offline, user qo'lda yakunlaydi).
                .StopIfAsync(async () => found!.Status != SessionStatus.Paused
                                         && await _processRepo.HasActiveProcessAsync(found.Id),
                             StopFactors.Session.HasActiveProcess)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<CloseSessionResultDto>.Blocked(stop);

            var session = found!;

            var (totalDelivered, totalCost) = await FinalizeOpenProcessesAsync(
                session, ProcessEndReason.UserStopped, sendStopCommand: session.Status != SessionStatus.Paused);

            // Hold invoice'lar bor bo'lsa — capture/refund maqsadlarini qo'yib Settling qilamiz;
            // watcher yakunlab sessiyani yopadi. Aks holda darhol yopamiz (legacy oqim).
            var needsSettlement = await _payments.BeginSessionSettlementAsync(session.Id);

            if (needsSettlement)
            {
                session.Status = SessionStatus.Settling;
                session.CloseReason = SessionCloseReason.UserClosed;
                session.LastActivityAt = DateTime.Now;
                await _sessionRepo.UpdateAsync(session);

                await _notifier.NotifySessionUpdatedAsync(session.SessionToken, new
                {
                    session_id = session.Id,
                    status = SessionStatus.Settling.ToString(),
                    total_delivered = totalDelivered,
                    total_cost = totalCost
                });

                _logger.LogInformation(
                    "Sessiya hisob-kitobga o'tdi (user): sessionId={SessionId} userId={UserId}",
                    session.Id, session.UserId);

                return GenericDto<CloseSessionResultDto>.Success(new CloseSessionResultDto
                {
                    ResultMessage = "Sessiya yakunlanmoqda — hold mablag'lari hisob-kitob qilinmoqda.",
                    TotalDelivered = totalDelivered,
                    TotalCost = totalCost
                });
            }

            session.Status = SessionStatus.Closed;
            session.CloseReason = SessionCloseReason.UserClosed;
            session.ClosedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            // Qurilmani sessiya yopilgani haqida xabardor qilamiz — jarayon allaqachon
            // tugagan bo'lsa ham (process-level stop yuborilmagan bo'lsa ham) QR/ekran tozalansin.
            if (session.Device is not null)
                await _commandPublisher.PublishSessionClosedAsync(
                    session.Device.SerialNumber, session.Id, nameof(SessionCloseReason.UserClosed));

            await _notifier.NotifySessionClosedAsync(session.SessionToken, new
            {
                reason = nameof(SessionCloseReason.UserClosed),
                total_delivered = totalDelivered,
                total_cost = totalCost,
                closed_at = session.ClosedAt
            });

            _logger.LogInformation(
                "Sessiya yopildi (user): sessionId={SessionId} userId={UserId} delivered={Delivered} cost={Cost}",
                session.Id, session.UserId, totalDelivered, totalCost);

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
                // Paused sessiyada qurilma offline — stop yubormaymiz (yetib bormaydi).
                var sendStop = session.Status != SessionStatus.Paused;

                var (totalDelivered, totalCost) = await FinalizeOpenProcessesAsync(
                    session, ProcessEndReason.DeviceError, sendStopCommand: sendStop);

                // Hold invoice'lar bo'lsa — Settling qilamiz, watcher yopadi.
                if (await _payments.BeginSessionSettlementAsync(session.Id))
                {
                    session.Status = SessionStatus.Settling;
                    session.CloseReason = SessionCloseReason.Timeout;
                    session.LastActivityAt = DateTime.Now;
                    await _sessionRepo.UpdateAsync(session);

                    _logger.LogInformation(
                        "Sessiya hisob-kitobga o'tdi (idle timeout): sessionId={SessionId}", session.Id);
                    continue;
                }

                session.Status = SessionStatus.Closed;
                session.CloseReason = SessionCloseReason.Timeout;
                session.ClosedAt = DateTime.Now;
                await _sessionRepo.UpdateAsync(session);

                if (session.Device is not null)
                    await _commandPublisher.PublishSessionClosedAsync(
                        session.Device.SerialNumber, session.Id, nameof(SessionCloseReason.Timeout));

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

                _logger.LogInformation(
                    "Sessiya yopildi (idle timeout): sessionId={SessionId} userId={UserId} cost={Cost}",
                    session.Id, session.UserId, totalCost);
            }
        }

        public async Task<GenericDto<CurrentSessionDto?>> GetCurrentAsync(long userId)
        {
            var session = await _sessionRepo.GetActiveByUserAsync(userId);
            if (session is null)
                return GenericDto<CurrentSessionDto?>.Success(null);

            return GenericDto<CurrentSessionDto?>.Success(await MapToCurrentAsync(session));
        }

        public async Task<GenericDto<CurrentSessionDto>> GetByIdAsync(long sessionId, long userId)
        {
            var found = await _sessionRepo.GetByIdWithProcessesAsync(sessionId);

            var stop = StopFactorCheck.For("Session.GetById")
                .StopIf(found is null, StopFactors.Session.NotFound)
                .StopIf(() => found!.UserId != userId, StopFactors.Session.NotOwned)
                .Result();

            if (stop is not null)
                return GenericDto<CurrentSessionDto>.Blocked(stop);

            return GenericDto<CurrentSessionDto>.Success((await MapToCurrentAsync(found))!);
        }

        public async Task<GenericDto<HeartbeatResultDto>> HeartbeatAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);

            var stop = StopFactorCheck.For(StopActions.SessionHeartbeat)
                .StopIf(session is null, StopFactors.Session.NotFound)
                .StopIf(() => session!.UserId != userId, StopFactors.Session.NotOwned)
                .StopIf(() => session!.Status == SessionStatus.Closed, StopFactors.Session.Closed)
                .Result();

            if (stop is not null)
                return GenericDto<HeartbeatResultDto>.Blocked(stop);

            // Poyga: tekshiruvdan keyin sessiya yopilib qolgan bo'lishi mumkin.
            var affected = await _sessionRepo.TouchAsync(sessionId);
            if (affected == 0)
                return GenericDto<HeartbeatResultDto>.Blocked(StopFactors.Session.Closed);

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

        /// <summary>
        /// Snapshot + to'lov holati. To'lov konteksti sessiyaning ajralmas qismi
        /// (mahsulot tanlash undan moliyalanadi), shuning uchun u HAR BIR sessiya
        /// o'qishida birga keladi — ilova alohida so'rov qilishi shart emas.
        /// </summary>
        internal async Task<CurrentSessionDto?> MapToCurrentAsync(SessionEntity? session)
        {
            var dto = MapToCurrent(session);
            if (dto is not null)
                dto.Payment = await _payments.GetSnapshotAsync(dto.SessionId);

            return dto;
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

        /// <summary>
        /// Offline (stale) qurilmalarning aktiv sessiyalarini YOPMAYDI — Paused qiladi.
        /// Qurilma qayta ulansa avto-resume; ulanmasa idle-timeout yakunlab yopadi.
        /// Jarayonlarni bu yerda majburan yakunlamaymiz — 60s stalled-watchdog (device qaytargan
        /// real GivenAmount bilan) yakunlaydi, shunда capture taxminiy qiymatda bo'lmaydi.
        /// </summary>
        public async Task PauseOfflineDeviceSessionsAsync()
        {
            var threshold = DateTime.Now.Subtract(DeviceOfflineThreshold);
            var staleDevices = await _deviceRepo.GetStaleOnlineDevicesAsync(threshold);

            foreach (var device in staleDevices)
            {
                var activeSessions = await _sessionRepo.GetActiveSessionsForDeviceAsync(device.Id);
                foreach (var session in activeSessions)
                {
                    // Faqat jonli holatlarni pauza qilamiz (Paused/Settling/Closed'ga tegmaymiz).
                    if (session.Status is not (SessionStatus.Connected or SessionStatus.InProcess))
                        continue;

                    session.Status = SessionStatus.Paused;
                    session.LastActivityAt = DateTime.Now; // idle-timer davom etadi — tashlab qo'yilgan pauza ham yopiladi
                    await _sessionRepo.UpdateAsync(session);

                    await _notifier.NotifySessionUpdatedAsync(session.SessionToken, new
                    {
                        session_id = session.Id,
                        status = SessionStatus.Paused.ToString(),
                        reason = nameof(SessionCloseReason.DeviceLost)
                    });

                    await _push.SendAsync(session.UserId, new PushNotification
                    {
                        Title = "Qurilma bilan aloqa uzildi",
                        Body = $"Qurilma {device.SerialNumber} javob bermayapti. Sessiya pauza qilindi.",
                        DeepLink = $"botenergy://sessions/{session.Id}"
                    });

                    _logger.LogWarning(
                        "Sessiya pauza qilindi (device lost): sessionId={SessionId} serial={Serial}",
                        session.Id, device.SerialNumber);
                }

                device.IsOnline = false;
                await _deviceRepo.UpdateAsync(device);

                await _deviceStatus.NotifyOfflineAsync(
                    device.SerialNumber,
                    lost: activeSessions.Count > 0,
                    sessionId: activeSessions.FirstOrDefault()?.Id);
            }
        }

        /// <summary>
        /// Qurilma qayta online bo'lganda (DeviceStatusService.MarkSeenAsync edge) chaqiriladi —
        /// Paused sessiyalarni avtomatik Resume qiladi.
        /// </summary>
        public async Task ResumePausedSessionsForDeviceAsync(long deviceId)
        {
            var sessions = await _sessionRepo.GetActiveSessionsForDeviceAsync(deviceId);
            foreach (var session in sessions)
            {
                if (session.Status != SessionStatus.Paused)
                    continue;

                // Aktiv (tugamagan) jarayon bo'lsa InProcess, aks holda Connected.
                var hasActive = await _processRepo.HasActiveProcessAsync(session.Id);
                session.Status = hasActive ? SessionStatus.InProcess : SessionStatus.Connected;
                session.LastActivityAt = DateTime.Now;
                await _sessionRepo.UpdateAsync(session);

                await _notifier.NotifySessionUpdatedAsync(session.SessionToken, new
                {
                    session_id = session.Id,
                    status = session.Status.ToString(),
                    resumed = true
                });

                await _push.SendAsync(session.UserId, new PushNotification
                {
                    Title = "Qurilma qayta ulandi",
                    Body = "Sessiyangiz davom ettirildi.",
                    DeepLink = $"botenergy://sessions/{session.Id}"
                });

                _logger.LogInformation(
                    "Sessiya resume qilindi (device reconnect): sessionId={SessionId} deviceId={DeviceId}",
                    session.Id, deviceId);
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
                    await _commandPublisher.PublishStopAsync(session.Device.SerialNumber, process.Id);
                }

                process.Status = ProcessStatus.Ended;
                process.EndReason = endReason;
                process.EndedAt = DateTime.Now;

                // Yakunlash + hisob-kitob (funding manbasiga qarab) — bitta tranzaksiya (crash-safe).
                var deducted = await _tx.RunAsync(async () =>
                {
                    await _processRepo.UpdateAsync(process);
                    return await _settlement.SettleAsync(process.Id);
                });

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
