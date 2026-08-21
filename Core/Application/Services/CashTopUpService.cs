using Domain.Dtos.Base;
using Domain.Dtos.Cash;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
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
        private readonly IUsageProbeRepository _usageProbe;
        private readonly ICardPayoutClient _bank;
        private readonly ITransactionRunner _transaction;
        private readonly CashTopUpOptions _options;
        private readonly ILogger<CashTopUpService> _logger;

        public CashTopUpService(
            ICashSessionRepository sessionRepo,
            IDeviceRepository deviceRepo,
            IUsageProbeRepository usageProbe,
            ICardPayoutClient bank,
            ITransactionRunner transaction,
            IOptions<CashTopUpOptions> options,
            ILogger<CashTopUpService> logger)
        {
            _sessionRepo = sessionRepo;
            _deviceRepo = deviceRepo;
            _usageProbe = usageProbe;
            _bank = bank;
            _transaction = transaction;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<GenericDto<CashSessionOpenedDto>> OpenSessionAsync(
            string serialNumber, string cardPan, CancellationToken ct = default)
        {
            var cardOk = CardNumberHelper.TryNormalize(cardPan, out var pan);
            var found = await _deviceRepo.GetBySerialNumberAsync(serialNumber);

            // Bank chaqiruvidan OLDIN barcha to'siqlar tekshiriladi — baribir rad etiladigan
            // so'rov bilan tashqi tizimni bezovta qilmaymiz va sessiya yozuvi ham yaratilmaydi.
            var stop = await StopFactorCheck.For(StopActions.CashSessionOpen)
                .StopIf(!cardOk, StopFactors.Cash.CardInvalid(CardNumberHelper.ErrorMessage))
                // GetBySerialNumberAsync faqat faol qurilmani qaytaradi — topilmasa yo yo'q, yo nofaol.
                .StopIf(found is null, StopFactors.Device.NotFound)
                .StopIf(() => found!.Station is not null && !found.Station.IsActive,
                        StopFactors.Station.Inactive)
                // Bitta qurilmada bir vaqtda bitta ochiq sessiya. Ilgarigisi osilib qolgan bo'lsa —
                // mijoz almashgan bo'lishi mumkin, shuning uchun avtomatik yopmaymiz: bu pul
                // bilan bog'liq holat, qaror idle timeout siyosatiga qoldiriladi.
                .StopIfAsync(async () => await _sessionRepo.GetActiveByDeviceAsync(found!.Id) is not null,
                             StopFactors.Device.HasOpenCashSession)
                // Box inkassatsiya uchun ochiq bo'lsa, tushgan kupyura kutilgan summadan
                // tashqarida qoladi — inkassatsiya hisobi buziladi.
                .StopIfAsync(() => _usageProbe.DeviceHasOpenCollectionAsync(found!.Id),
                             StopFactors.Cash.BoxOpen)
                .ResultAsync();

            if (stop is not null)
            {
                _logger.LogInformation(
                    "[CASH] Sessiya ochilmadi serial={Serial} sabab={Reason}", serialNumber, stop.Code);
                return GenericDto<CashSessionOpenedDto>.Blocked(stop);
            }

            var device = found!;

            var verification = await _bank.VerifyCardAsync(pan, ct);
            if (!verification.IsSuccess)
            {
                _logger.LogWarning(
                    "[CASH] Karta tasdiqlanmadi serial={Serial} masked={Masked} kind={Kind} code={Code}",
                    serialNumber, CardNumberHelper.Mask(pan), verification.FailureKind, verification.ErrorCode);

                return GenericDto<CashSessionOpenedDto>.Blocked(
                    verification.FailureKind == CardPayoutFailureKind.Rejected
                        ? StopFactors.Cash.CardRejected(verification.FailureMessage ?? "Karta qabul qilinmadi.")
                        : StopFactors.Cash.BankUnavailable);
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

            var stop = StopFactorCheck.For(StopActions.CashBillAdd)
                .StopIf(session is null || session.SerialNumber != serialNumber, StopFactors.Cash.SessionNotFound)
                .StopIf(() => session!.Status != CashSessionStatus.Accepting, StopFactors.Cash.NotAccepting)
                // Nominal ro'yxati — buzilgan yoki soxta xabar summani sun'iy oshirib yuborishidan himoya.
                .StopIf(() => !_options.AllowedDenominations.Contains(denomination),
                        StopFactors.Cash.DenominationRejected)
                .StopIf(() => session!.AcceptedAmount + denomination > _options.MaxSessionAmount,
                        StopFactors.Cash.LimitExceeded)
                .StopIf(billSeq <= 0, StopFactors.Cash.InvalidBillSequence)
                .Result();

            if (stop is not null)
            {
                _logger.LogWarning(
                    "[CASH] Kupyura rad etildi serial={Serial} sessionId={SessionId} nominal={Denomination} sabab={Reason}",
                    serialNumber, cashSessionId, denomination, stop.Code);
                return GenericDto<CashSessionTotalDto>.Blocked(stop);
            }

            var accepting = session!;

            // Kupyura yozuvi va jami summa bitta tranzaksiyada — biri yozilib ikkinchisi
            // yozilmay qolsa hisob buziladi.
            var result = await _transaction.RunAsync(() =>
                _sessionRepo.TryAddBillAsync(
                    accepting.Id, accepting.DeviceId, accepting.SerialNumber, denomination, billSeq));

            if (!result.Added)
            {
                _logger.LogInformation(
                    "[CASH] Takroriy kupyura serial={Serial} sessionId={SessionId} seq={Seq}",
                    serialNumber, cashSessionId, billSeq);
            }

            return GenericDto<CashSessionTotalDto>.Success(new CashSessionTotalDto
            {
                CashSessionId = accepting.Id,
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
                return GenericDto<CashSessionResultDto>.Blocked(StopFactors.Cash.SessionNotFound);

            // Takroriy commit (device javobni olmay qayta yuborgan) — mavjud natijani qaytaramiz.
            if (session.Status is CashSessionStatus.Completed or CashSessionStatus.Committing)
                return GenericDto<CashSessionResultDto>.Success(ToResult(session, message: null));

            var stop = StopFactorCheck.For(StopActions.CashCommit)
                .StopIf(session.Status is not (CashSessionStatus.PayoutFailed or CashSessionStatus.Accepting),
                        StopFactors.Cash.Finished)
                .StopIf(session.AcceptedAmount <= 0, StopFactors.Cash.Empty)
                .Result();

            if (stop is not null)
                return GenericDto<CashSessionResultDto>.Blocked(stop);

            if (!string.IsNullOrWhiteSpace(clientRef))
                session.IdempotencyKey = clientRef;

            return await ExecutePayoutAsync(session, ct);
        }

        public async Task<GenericDto<CashSessionResultDto>> CancelAsync(
            string serialNumber, long cashSessionId, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(cashSessionId);

            var stop = StopFactorCheck.For(StopActions.CashCancel)
                .StopIf(session is null || session.SerialNumber != serialNumber, StopFactors.Cash.SessionNotFound)
                .StopIf(() => session!.Status != CashSessionStatus.Accepting, StopFactors.Cash.NotAccepting)
                // Bill acceptor qabul qilingan pulni qaytara olmaydi — pul solingan bo'lsa
                // yagona to'g'ri yakun kartaga o'tkazish.
                .StopIf(() => session!.AcceptedAmount > 0, StopFactors.Cash.HasMoney)
                .Result();

            if (stop is not null)
                return GenericDto<CashSessionResultDto>.Blocked(stop);

            var cancelling = session!;
            cancelling.Status = CashSessionStatus.Cancelled;
            cancelling.LastActivityAt = DateTime.Now;
            await _sessionRepo.UpdateAsync(cancelling);

            _logger.LogInformation("[CASH] Sessiya bekor qilindi sessionId={SessionId}", cancelling.Id);

            return GenericDto<CashSessionResultDto>.Success(ToResult(cancelling, "Sessiya bekor qilindi."));
        }

        public async Task<GenericDto<CashSessionResultDto>> RetryPayoutAsync(
            long cashSessionId, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetByIdAsync(cashSessionId);

            var stop = StopFactorCheck.For(StopActions.CashRetry)
                .StopIf(session is null, StopFactors.Cash.SessionNotFound)
                .StopIf(() => session!.Status != CashSessionStatus.PayoutFailed, StopFactors.Cash.RetryNotNeeded)
                .Result();

            if (stop is not null)
                return GenericDto<CashSessionResultDto>.Blocked(stop);

            return await ExecutePayoutAsync(session!, ct);
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
