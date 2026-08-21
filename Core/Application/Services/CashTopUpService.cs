using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Interfaces.Bank;
using Domain.Options;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    /// <summary>
    /// Naqd → karta oqimi. Qurilma MQTT handler'lari va fon servislari shu servisni chaqiradi.
    ///
    /// Pul harakati bo'yicha ikkita qat'iy qoida:
    ///  1. Qurilma naqd qoldig'i faqat <c>IDeviceRepository.AddCashAsync</c> orqali (FOR UPDATE),
    ///     hech qachon entity ustida read-modify-write bilan emas.
    ///  2. Bankka yuboriladigan <c>orderId</c> sessiya bo'yicha BARQAROR (<c>cash-{id}</c>) —
    ///     qayta urinishlarda bank o'z tomonida ikkinchi o'tkazma yasamaydi.
    /// </summary>
    public class CashTopUpService : ICashTopUpService
    {
        private readonly ICashSessionRepository _sessionRepo;
        private readonly IDeviceRepository _deviceRepo;
        private readonly ICardPayoutClient _bank;
        private readonly ITransactionRunner _transaction;
        private readonly CashTopUpOptions _options;
        private readonly ILogger<CashTopUpService> _logger;

        public CashTopUpService(
            ICashSessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            ICardPayoutClient bank,
            ITransactionRunner transaction,
            IOptions<CashTopUpOptions> options,
            ILogger<CashTopUpService> logger)
        {
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _bank = bank;
            _transaction = transaction;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<GenericDto<CashSessionOpenedDto>> OpenSessionAsync(
            string serialNumber, string cardPan, CancellationToken ct = default)
        {
            if (!CardNumberHelper.TryNormalize(cardPan, out var pan))
                return GenericDto<CashSessionOpenedDto>.Error(400, CardNumberHelper.ErrorMessage);

            var device = await _deviceRepo.GetBySerialNumberAsync(serialNumber);
            if (device is null)
                return GenericDto<CashSessionOpenedDto>.Error(404, "Qurilma topilmadi yoki faol emas.");

            // Bitta qurilmada bir vaqtda bitta ochiq sessiya. Ilgarigisi osilib qolgan bo'lsa —
            // mijoz almashgan bo'lishi mumkin, shuning uchun avtomatik yopmaymiz: bu pul
            // bilan bog'liq holat, qaror idle timeout siyosatiga qoldiriladi.
            var active = await _sessionRepo.GetActiveByDeviceAsync(device.Id);
            if (active is not null)
            {
                return GenericDto<CashSessionOpenedDto>.Error(409,
                    "Qurilmada tugallanmagan naqd sessiya bor. Avval uni yakunlang.");
            }

            var verification = await _bank.VerifyCardAsync(pan, ct);
            if (!verification.IsSuccess)
            {
                _logger.LogWarning(
                    "[CASH] Karta tasdiqlanmadi serial={Serial} masked={Masked} kind={Kind} code={Code}",
                    serialNumber, CardNumberHelper.Mask(pan), verification.FailureKind, verification.ErrorCode);

                var message = verification.FailureKind == CardPayoutFailureKind.Rejected
                    ? verification.FailureMessage ?? "Karta qabul qilinmadi."
                    : "Bank bilan aloqa yo'q. Birozdan keyin urinib ko'ring.";

                return GenericDto<CashSessionOpenedDto>.Error(422, message);
            }

            var card = verification.Result!;

            var session = await _sessionRepo.CreateAsync(new CashSessionEntity
            {
                DeviceId = device.Id,
                SerialNumber = device.SerialNumber,
                CardMasked = card.MaskedPan,
                CardToken = card.CardToken,
                Status = CashSessionStatus.Accepting,
                LastActivityAt = DateTime.Now
            });

            _logger.LogInformation(
                "[CASH] Sessiya ochildi serial={Serial} sessionId={SessionId} card={Masked}",
                serialNumber, session.Id, card.MaskedPan);

            return GenericDto<CashSessionOpenedDto>.Success(new CashSessionOpenedDto
            {
                CashSessionId = session.Id,
                CardMasked = card.MaskedPan
            });
        }

        public async Task<GenericDto<CashSessionTotalDto>> AddBillAsync(
            string serialNumber, long cashSessionId, decimal denomination, int billSeq,
            CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(cashSessionId);
            if (session is null || session.SerialNumber != serialNumber)
                return GenericDto<CashSessionTotalDto>.Error(404, "Naqd sessiya topilmadi.");

            if (session.Status != CashSessionStatus.Accepting)
                return GenericDto<CashSessionTotalDto>.Error(409, "Sessiya naqd qabul qilish holatida emas.");

            // Nominal ro'yxati — buzilgan yoki soxta xabar summani sun'iy oshirib yuborishidan himoya.
            if (!_options.AllowedDenominations.Contains(denomination))
            {
                _logger.LogWarning(
                    "[CASH] Ruxsat etilmagan nominal serial={Serial} sessionId={SessionId} nominal={Denomination}",
                    serialNumber, cashSessionId, denomination);
                return GenericDto<CashSessionTotalDto>.Error(400, "Kupyura nominali qabul qilinmaydi.");
            }

            if (session.AcceptedAmount + denomination > _options.MaxSessionAmount)
                return GenericDto<CashSessionTotalDto>.Error(409, "Sessiya bo'yicha maksimal summa oshib ketdi.");

            if (billSeq <= 0)
                return GenericDto<CashSessionTotalDto>.Error(400, "bill_seq noldan katta bo'lishi kerak.");

            // Kupyura yozuvi va jami summa bitta tranzaksiyada — biri yozilib ikkinchisi
            // yozilmay qolsa hisob buziladi.
            var result = await _transaction.RunAsync(() =>
                _sessionRepo.TryAddBillAsync(
                    session.Id, session.DeviceId, session.SerialNumber, denomination, billSeq));

            if (!result.Added)
            {
                _logger.LogInformation(
                    "[CASH] Takroriy kupyura serial={Serial} sessionId={SessionId} seq={Seq}",
                    serialNumber, cashSessionId, billSeq);
            }

            return GenericDto<CashSessionTotalDto>.Success(new CashSessionTotalDto
            {
                CashSessionId = session.Id,
                AcceptedTotal = result.AcceptedTotal,
                BillCount = result.BillCount,
                Added = result.Added
            });
        }

        public async Task<GenericDto<CashSessionResultDto>> CommitAsync(
            string serialNumber, long cashSessionId, string? clientRef, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(cashSessionId);
            if (session is null || session.SerialNumber != serialNumber)
                return GenericDto<CashSessionResultDto>.Error(404, "Naqd sessiya topilmadi.");

            // Takroriy commit (device javobni olmay qayta yuborgan) — mavjud natijani qaytaramiz.
            if (session.Status is CashSessionStatus.Completed or CashSessionStatus.Committing)
                return GenericDto<CashSessionResultDto>.Success(ToResult(session, message: null));

            if (session.Status != CashSessionStatus.PayoutFailed && session.Status != CashSessionStatus.Accepting)
                return GenericDto<CashSessionResultDto>.Error(409, "Sessiya yakunlangan.");

            if (session.AcceptedAmount <= 0)
                return GenericDto<CashSessionResultDto>.Error(400, "Hech qanday pul qabul qilinmagan.");

            if (!string.IsNullOrWhiteSpace(clientRef))
                session.IdempotencyKey = clientRef;

            return await ExecutePayoutAsync(session, ct);
        }

        public async Task<GenericDto<CashSessionResultDto>> CancelAsync(
            string serialNumber, long cashSessionId, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(cashSessionId);
            if (session is null || session.SerialNumber != serialNumber)
                return GenericDto<CashSessionResultDto>.Error(404, "Naqd sessiya topilmadi.");

            if (session.Status != CashSessionStatus.Accepting)
                return GenericDto<CashSessionResultDto>.Error(409, "Sessiya bekor qilinadigan holatda emas.");

            // Bill acceptor qabul qilingan pulni qaytara olmaydi — pul solingan bo'lsa
            // yagona to'g'ri yakun kartaga o'tkazish.
            if (session.AcceptedAmount > 0)
            {
                return GenericDto<CashSessionResultDto>.Error(409,
                    "Pul qabul qilingan — sessiyani bekor qilib bo'lmaydi, kartaga o'tkazing.");
            }

            session.Status = CashSessionStatus.Cancelled;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            _logger.LogInformation("[CASH] Sessiya bekor qilindi sessionId={SessionId}", session.Id);

            return GenericDto<CashSessionResultDto>.Success(ToResult(session, "Sessiya bekor qilindi."));
        }

        public async Task<GenericDto<CashSessionResultDto>> RetryPayoutAsync(
            long cashSessionId, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(cashSessionId);
            if (session is null)
                return GenericDto<CashSessionResultDto>.Error(404, "Naqd sessiya topilmadi.");

            if (session.Status != CashSessionStatus.PayoutFailed)
                return GenericDto<CashSessionResultDto>.Error(409, "Sessiya qayta urinishga muhtoj emas.");

            return await ExecutePayoutAsync(session, ct);
        }

        public async Task<int> CloseIdleSessionsAsync(CancellationToken ct = default)
        {
            var threshold = DateTime.Now.AddMinutes(-_options.IdleTimeoutMinutes);
            var idle = await _sessionRepo.GetIdleAsync(threshold);
            if (idle.Count == 0)
                return 0;

            var closed = 0;

            foreach (var session in idle)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (session.AcceptedAmount > 0)
                {
                    // Mijoz pul solib ketib qolgan — pul unga tegishli, kartaga o'tkazamiz.
                    _logger.LogWarning(
                        "[CASH] Idle sessiya avtomatik commit sessionId={SessionId} amount={Amount}",
                        session.Id, session.AcceptedAmount);

                    await ExecutePayoutAsync(session, ct);
                }
                else
                {
                    session.Status = CashSessionStatus.Expired;
                    await _sessionRepo.UpdateAsync(session);

                    _logger.LogInformation("[CASH] Bo'sh sessiya Expired sessionId={SessionId}", session.Id);
                }

                closed++;
            }

            return closed;
        }

        /// <summary>
        /// Bankka o'tkazma va natijaga qarab sessiya holatini yakunlash.
        /// Commit, watcher retry va idle auto-commit — uchalasi ham shu yerdan o'tadi.
        /// </summary>
        private async Task<GenericDto<CashSessionResultDto>> ExecutePayoutAsync(
            CashSessionEntity session, CancellationToken ct)
        {
            // Parallel commit'ni to'sish: bank chaqiruvidan OLDIN holat o'zgaradi.
            session.Status = CashSessionStatus.Committing;
            session.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(session);

            // orderId sessiya bo'yicha barqaror — qayta urinishda bank ikkinchi o'tkazma yasamaydi.
            var orderId = $"cash-{session.Id}";

            var payout = await _bank.PayoutAsync(session.CardToken, session.AcceptedAmount, orderId, ct);

            if (payout.IsSuccess && payout.Result!.State != CardPayoutState.Failed)
            {
                decimal newBalance = 0m;

                // Sessiyani yopish va qurilma qoldig'ini oshirish — bitta tranzaksiyada.
                await _transaction.RunAsync(async () =>
                {
                    session.Status = CashSessionStatus.Completed;
                    session.PayoutReference = payout.Result.ProviderRef;
                    session.CompletedAt = DateTime.Now;
                    session.FailureReason = null;
                    session.NextAttemptAt = null;
                    session.LockedBy = null;
                    session.LeaseUntil = null;
                    await _sessionRepo.UpdateAsync(session);

                    // Pul jismonan qurilmada qoldi — box qoldig'i shuncha oshadi.
                    newBalance = await _deviceRepo.AddCashAsync(session.DeviceId, session.AcceptedAmount);
                });

                _logger.LogInformation(
                    "[CASH] Payout OK sessionId={SessionId} amount={Amount} ref={Ref} deviceCash={Balance}",
                    session.Id, session.AcceptedAmount, payout.Result.ProviderRef, newBalance);

                var okResult = ToResult(session, "Pul kartaga o'tkazildi.");
                okResult.DeviceCashBalance = newBalance;
                return GenericDto<CashSessionResultDto>.Success(okResult);
            }

            // ── Yiqildi ────────────────────────────────────────────────────────
            session.Status = CashSessionStatus.PayoutFailed;
            session.AttemptCount++;
            session.FailureReason = payout.FailureMessage ?? payout.ErrorCode ?? "Noma'lum xato.";
            session.LockedBy = null;
            session.LeaseUntil = null;

            var retryable = payout.IsRetryable && session.AttemptCount < _options.MaxAttempts;
            session.NextAttemptAt = retryable ? DateTime.Now.Add(ComputeBackoff(session.AttemptCount)) : null;

            await _sessionRepo.UpdateAsync(session);

            _logger.LogError(
                "[CASH] Payout yiqildi sessionId={SessionId} attempt={Attempt} kind={Kind} retry={Retry} reason={Reason}",
                session.Id, session.AttemptCount, payout.FailureKind, retryable, session.FailureReason);

            var message = retryable
                ? "Bank bilan aloqa yo'q — qayta urinilmoqda."
                : "Pulni kartaga o'tkazib bo'lmadi. Operator bilan bog'laning.";

            var failResult = ToResult(session, message);
            failResult.RetryScheduled = retryable;
            return GenericDto<CashSessionResultDto>.Success(failResult);
        }

        /// <summary>Eksponensial backoff, yuqori chegara bilan.</summary>
        private TimeSpan ComputeBackoff(int attemptCount)
        {
            var seconds = _options.BackoffBaseSeconds * Math.Pow(2, Math.Max(0, attemptCount - 1));
            return TimeSpan.FromSeconds(Math.Min(seconds, _options.BackoffMaxSeconds));
        }

        private static CashSessionResultDto ToResult(CashSessionEntity session, string? message)
            => new()
            {
                CashSessionId = session.Id,
                Status = session.Status,
                Amount = session.AcceptedAmount,
                PayoutReference = session.PayoutReference,
                Message = message,
                // Takroriy commit'da ham qurilma "kutilmoqda"ni to'g'ri ko'rsatishi uchun:
                // rejalashtirilgan urinish bor-yo'qligi holatning o'zidan olinadi.
                RetryScheduled = session.Status == CashSessionStatus.PayoutFailed
                                 && session.NextAttemptAt is not null
            };
    }
}
