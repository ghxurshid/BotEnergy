using System.Text.Json;
using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Guards;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Options;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// Barcha to'lov strategiyalari uchun UMUMIY yadro: intent yozuvi, FIFO consume,
    /// audit step'lar, lease/backoff bilan watcher tick'i, sessiya yakuniy hisob-kitobi va
    /// balans eventi. Provider bilan gaplashish qismi abstrakt — uni har bir strategiya yozadi.
    ///
    /// Yozilmagan qoida: status HECH QACHON to'g'ridan-to'g'ri yozilmaydi —
    /// faqat <see cref="IPaymentIntentRepository.TryTransitionAsync"/> (state machine tekshiradi).
    /// </summary>
    public abstract class PaymentStrategyBase : ISessionPaymentStrategy
    {
        protected readonly IPaymentIntentRepository Intents;
        protected readonly IPaymentSessionRepository PaymentSessions;
        protected readonly ISessionRepository Sessions;
        protected readonly IDeviceRepository Devices;
        protected readonly IProductProcessRepository Processes;
        protected readonly ISessionNotifier Notifier;
        protected readonly IDeviceCommandPublisher Commands;
        protected readonly IPushNotificationService Push;
        protected readonly ITransactionRunner Tx;
        protected readonly PaymentOptions Options;
        protected readonly ILogger Logger;

        protected PaymentStrategyBase(PaymentStrategyDependencies deps, ILogger logger)
        {
            Intents = deps.Intents;
            PaymentSessions = deps.PaymentSessions;
            Sessions = deps.Sessions;
            Devices = deps.Devices;
            Processes = deps.Processes;
            Notifier = deps.Notifier;
            Commands = deps.Commands;
            Push = deps.Push;
            Tx = deps.Tx;
            Options = deps.Options;
            Logger = logger;
        }

        public abstract PaymentStrategyProfile Profile { get; }

        // ═══════════════════════ Provider ilgaklari ═══════════════════════
        // Strategiya FAQAT shularni yozadi.

        /// <summary>
        /// Provider tomonda mablag' so'rash. Intent yozuvi allaqachon Created holatda mavjud
        /// (<paramref name="intent"/>.ProviderOrderId to'ldirilgan).
        /// </summary>
        protected abstract Task<ProviderFunding> RequestFundingAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CreatePaymentIntentDto dto, CancellationToken ct);

        /// <summary>Mijoz to'ladimi — provider holatini so'rash (polling).</summary>
        protected abstract Task<ProviderPoll> PollFundingAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct);

        /// <summary>
        /// Yakuniy hisob-kitob: ishlatilgan summani merchantga o'tkazish.
        /// Prepaid strategiyalarda pul allaqachon yechilgan — <see cref="ProviderCall.NoOp"/> qaytariladi.
        /// </summary>
        protected abstract Task<ProviderCall> CaptureAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, long amountTiyin, CancellationToken ct);

        /// <summary>Ishlatilmagan mablag'ni mijozga qaytarish (hold bo'shatish yoki real refund).</summary>
        protected abstract Task<ProviderCall> RefundAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct);

        /// <summary>
        /// Hali to'lanmagan chekni bekor qilish. Sukut bo'yicha refund bilan bir xil
        /// (Payme'da receipts.cancel ikkalasini ham bajaradi).
        /// </summary>
        protected virtual Task<ProviderCall> CancelUnpaidAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
            => RefundAsync(intent, ps, ct);

        /// <summary>
        /// Strategiyaga xos qo'shimcha to'siqlar (masalan: merchant credential'lari sozlanganmi,
        /// tasdiqlangan karta bormi). null — to'siq yo'q.
        /// </summary>
        protected virtual Task<StopFactor?> ValidateCreateAsync(
            PaymentSessionEntity ps, CreatePaymentIntentDto dto)
            => Task.FromResult<StopFactor?>(null);

        /// <summary>
        /// Sessiya yopilganda mablag' bilan nima bo'lishi. Sukut — strategiya profili;
        /// prepaid strategiyalar merchant sozlamasini (RefundUnusedFunds) hisobga olish uchun override qiladi.
        /// </summary>
        protected virtual Task<SettlementMode> ResolveSettlementModeAsync(PaymentSessionEntity ps)
            => Task.FromResult(Profile.Settlement);

        /// <summary>
        /// Mijozga ko'rsatiladigan qisqa ko'rsatma — to'lov oynasining sarlavhasi ostida turadi.
        /// Har bir strategiya o'z matnini beradi, shuning uchun sirtda usul nomiga qarab
        /// <c>if</c> yozilmaydi.
        /// </summary>
        protected abstract string CustomerHint { get; }

        /// <summary>
        /// Mijoz shu usul bilan to'lay olishi uchun nima kerakligi. Sukut bo'yicha qo'shimcha
        /// shart yo'q — karta/telefon talab qiladigan strategiyalar buni override qiladi.
        /// </summary>
        public virtual Task<PaymentPrerequisites> GetPrerequisitesAsync(PaymentSessionEntity ps, long userId)
            => Task.FromResult(PaymentPrerequisites.Ready(CustomerHint));

        /// <summary>Watcher shu intent'ni ishlay oladimi (provider identifikatori yetarlimi).</summary>
        protected virtual bool IsProcessable(PaymentIntentEntity intent)
            => !string.IsNullOrEmpty(intent.ProviderReceiptId);

        /// <summary>IsProcessable false bo'lganda yoziladigan sabab.</summary>
        protected virtual string NotProcessableReason => "Provider identifikatori yo'q.";

        /// <summary>
        /// Kutayotgan intent qanchadan keyin qayta ko'riladi. Polling qiladigan strategiyalar
        /// uchun sukut (Payments:PollSeconds); callback ishlatadigan usullar buni uzaytiradi —
        /// ular uchun tick faqat TTL tekshiruvi, ya'ni tez-tez aylanish bekorga DB yuki.
        /// </summary>
        protected virtual int PollSeconds => Options.PollSeconds;

        // ═══════════════════════ Intent yaratish ═══════════════════════

        public async Task<GenericDto<PaymentIntentResultDto>> CreateIntentAsync(
            CreatePaymentIntentDto dto, CancellationToken ct = default)
        {
            if (dto.AmountUzs <= 0)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.AmountNotPositive);

            var ps = await PaymentSessions.GetBySessionIdAsync(dto.SessionId);
            if (ps is null)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Session.NoPaymentContext);

            // Takroriy so'rov — mavjud intent qaytariladi (provider ikkinchi marta chaqirilmaydi).
            // Kalit mijoz header'idan keladi, ya'ni BOSHQA foydalanuvchi yoki boshqa sessiyaning
            // kalitiga to'g'ri kelib qolishi mumkin — shuning uchun egalik ham tekshiriladi,
            // aks holda begona to'lov ma'lumoti qaytarilardi.
            if (!string.IsNullOrEmpty(dto.IdempotencyKey))
            {
                var replay = await Intents.GetByIdempotencyKeyAsync(dto.IdempotencyKey);
                if (replay is not null)
                {
                    if (replay.PaymentSessionId != ps.Id || replay.CreatedByUserId != dto.UserId)
                        return GenericDto<PaymentIntentResultDto>.Blocked(
                            StopFactors.Payment.IdempotencyKeyReused);

                    return GenericDto<PaymentIntentResultDto>.Success(
                        MapResult(replay, "Takroriy so'rov — mavjud to'lov qaytarildi."));
                }
            }
            if (ps.Status != PaymentSessionStatus.Active)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Session.PaymentContextClosed);

            var activeCount = await Intents.CountActiveForPaymentSessionAsync(ps.Id);
            if (activeCount >= Options.MaxIntentsPerSession)
                return GenericDto<PaymentIntentResultDto>.Blocked(
                    StopFactors.Payment.IntentLimit(Options.MaxIntentsPerSession));

            var strategyStop = await ValidateCreateAsync(ps, dto);
            if (strategyStop is not null)
                return GenericDto<PaymentIntentResultDto>.Blocked(strategyStop);

            var amountTiyin = Money.ToTiyin(dto.AmountUzs);
            var intent = new PaymentIntentEntity
            {
                PaymentSessionId = ps.Id,
                Method = Profile.Method,
                Kind = Profile.Kind,
                SequenceNo = await Intents.NextSequenceNoAsync(ps.Id),
                AmountTiyin = amountTiyin,
                ProviderOrderId = $"ps{ps.Id}-{Guid.NewGuid():N}",
                IdempotencyKey = dto.IdempotencyKey,
                CustomerCardId = dto.CardId,
                CreatedByUserId = dto.UserId
            };
            try
            {
                intent = await Intents.CreateAsync(intent);
            }
            catch (DuplicateIdempotencyKeyException)
            {
                // Poyga: xuddi shu kalit bilan ikkinchi so'rov bizdan oldin ulgurdi.
                // Provider'ni qayta chaqirmaymiz — birinchi to'lovni qaytaramiz.
                var winner = await Intents.GetByIdempotencyKeyAsync(dto.IdempotencyKey!);
                return winner is not null
                    ? GenericDto<PaymentIntentResultDto>.Success(
                        MapResult(winner, "Takroriy so'rov — mavjud to'lov qaytarildi."))
                    : GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IdempotencyKeyReused);
            }

            await LogStepAsync(intent, ps, PaymentIntentStepType.Initiated, PaymentStepStatus.Info,
                message: $"method={Profile.Method} kind={Profile.Kind} amount={amountTiyin} tiyin " +
                         $"({dto.AmountUzs} UZS), seq={intent.SequenceNo}");
            await LogStepAsync(intent, ps, PaymentIntentStepType.Validated, PaymentStepStatus.Success);

            await LogStepAsync(intent, ps, PaymentIntentStepType.FundingRequested, PaymentStepStatus.Info,
                message: $"order_id={intent.ProviderOrderId}");

            var funding = await RequestFundingAsync(intent, ps, dto, ct);

            await LogStepAsync(intent, ps, PaymentIntentStepType.FundingCreated,
                funding.Call.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: funding.Call.RequestPayload,
                responsePayload: funding.Call.ResponsePayload,
                message: funding.Call.Message);

            if (!funding.Call.IsSuccess)
            {
                // Provider ATAYLAB rad etdi (mablag' yetmadi, karta bloklangan): pul umuman
                // harakatlanmadi va strategiya ochilgan chekni bekor qildi — bu operator ishi
                // emas. Shuning uchun Cancelled: mijoz darhol boshqa karta bilan qayta urinadi,
                // sessiya yopilishi esa "Failed intent" tufayli kutib qolmaydi.
                //
                // Tarmoq/timeout (Transient) da esa provider tomonda nima bo'lgani NOMA'LUM —
                // u Failed bo'lib operator ro'yxatiga tushadi.
                var permanent = funding.Call.Outcome == ProviderOutcome.Permanent;

                await Intents.TryTransitionAsync(intent.Id,
                    permanent ? PaymentIntentStatus.Cancelled : PaymentIntentStatus.Failed,
                    failureReason: $"Mablag' so'rash: {funding.Call.Message}");

                await PublishSessionPaymentStateAsync(ps.SessionId,
                    permanent ? BalanceChangeReasons.Cancelled : BalanceChangeReasons.Failed, intent.Id);

                // Mijoz uchun butunlay boshqa xabar: 502 da qayta urinadi, 402 da karta almashtiradi.
                return GenericDto<PaymentIntentResultDto>.Blocked(
                    permanent
                        ? StopFactors.Payment.ProviderRejected(funding.Call.Message)
                        : StopFactors.Payment.ProviderUnavailable);
            }

            intent.ProviderReceiptId = funding.ReceiptId ?? intent.ProviderReceiptId;
            intent.ProviderTransactionId = funding.TransactionId ?? intent.ProviderTransactionId;
            intent.ProviderState = funding.ProviderState ?? intent.ProviderState;
            intent.CheckoutUrl = funding.CheckoutUrl ?? intent.CheckoutUrl;
            await Intents.UpdateAsync(intent);

            if (funding.AlreadyFunded)
            {
                // Subscribe: saqlangan karta bilan darhol to'landi — mijoz hech qayerga o'tmaydi.
                await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.WaitingForConfirmation);
                await MarkFundedAsync(intent.Id, ps, "provider javobida darhol ta'minlandi");
                intent = await Intents.GetByIdAsync(intent.Id) ?? intent;

                // Poyga tufayli Funded bo'lmay qolsa, intent next_attempt_at=null bilan
                // watcher ko'rmaydigan holatda osilib qolardi — pul esa providerda ushlangan.
                // Shuning uchun majburan navbatga qaytaramiz: keyingi tick holatni aniqlaydi.
                if (intent.Status != PaymentIntentStatus.Funded)
                {
                    Logger.LogWarning(
                        "[PAY] intentId={IntentId} provider to'ladi, lekin Funded bo'lmadi (status={Status}) — polling'ga qaytarildi.",
                        intent.Id, intent.Status);
                    await Intents.SchedulePollAsync(intent.Id, DateTime.Now);
                }
            }
            else
            {
                // Mijoz to'lovini kutamiz — watcher polling boshlaydi.
                await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.WaitingForConfirmation,
                    nextAttemptAt: DateTime.Now);
                intent.Status = PaymentIntentStatus.WaitingForConfirmation;

                await PublishSessionPaymentStateAsync(ps.SessionId, BalanceChangeReasons.InvoiceCreated, intent.Id);
            }

            Logger.LogInformation(
                "[PAY] Intent yaratildi method={Method} intentId={IntentId} seq={Seq} amount={Amount} tiyin",
                Profile.Method, intent.Id, intent.SequenceNo, amountTiyin);

            return GenericDto<PaymentIntentResultDto>.Success(MapResult(intent, CreatedMessage(intent)));
        }

        /// <summary>Mijozga ko'rsatiladigan xabar — strategiya o'zgartirishi mumkin.</summary>
        protected virtual string CreatedMessage(PaymentIntentEntity intent)
            => intent.Status == PaymentIntentStatus.Funded
                ? "To'lov amalga oshdi — mablag' sessiyaga qo'shildi."
                : "To'lov yaratildi — tasdiqlashni kuting.";

        // ═══════════════════════ Bekor qilish ═══════════════════════

        public async Task<GenericDto<PaymentIntentResultDto>> CancelIntentAsync(
            long intentId, long userId, CancellationToken ct = default)
        {
            var intent = await Intents.GetByIdAsync(intentId);
            if (intent is null)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IntentNotFound);

            var ps = await PaymentSessions.GetByIdAsync(intent.PaymentSessionId);
            if (ps is null || ps.UserId != userId)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IntentNotOwned);

            // Mablag'ning bir qismi xizmatga ketgan — bekor qilish uni qaytarmaydi.
            if (intent.ConsumedTiyin > 0)
                return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IntentPartiallyConsumed);

            switch (intent.Status)
            {
                case PaymentIntentStatus.Funded:
                    // Qaytarishni qo'llamaydigan usulda (masalan Merchant API) pul allaqachon
                    // merchantda — mijozga "bekor qilindi" deb yolg'on javob bermaymiz.
                    if (!Profile.Supports(PaymentCapabilities.Refund))
                        return GenericDto<PaymentIntentResultDto>.Blocked(
                            StopFactors.Payment.CapabilityNotSupported(Profile.Method, "qaytarish"));

                    if (!await Intents.TryTransitionAsync(intentId, PaymentIntentStatus.RefundPending,
                            nextAttemptAt: DateTime.Now))
                        return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IntentStateChanged);

                    // Sessiya balansidan chiqaramiz — bu mablag' endi ishlatilmaydi.
                    await PaymentSessions.TryAddFundedBalanceAsync(ps.Id, -intent.AmountTiyin);

                    await LogStepAsync(intent, ps, PaymentIntentStepType.SettlementTargetAssigned,
                        PaymentStepStatus.Info, message: "User cancel: Funded → RefundPending");
                    break;

                case PaymentIntentStatus.Created:
                case PaymentIntentStatus.WaitingForConfirmation:
                    if (IsProcessable(intent))
                    {
                        var call = await CancelUnpaidAsync(intent, ps, ct);
                        await LogStepAsync(intent, ps, PaymentIntentStepType.Cancelled,
                            call.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                            requestPayload: call.RequestPayload,
                            responsePayload: call.ResponsePayload,
                            message: call.Message);
                        // Cancel xatosi bo'lsa ham davom etamiz — to'lanmagan chek TTL bilan o'zi o'ladi.
                    }

                    if (!await Intents.TryTransitionAsync(intentId, PaymentIntentStatus.Cancelled))
                        return GenericDto<PaymentIntentResultDto>.Blocked(StopFactors.Payment.IntentStateChanged);
                    break;

                default:
                    return GenericDto<PaymentIntentResultDto>.Blocked(
                        StopFactors.Payment.IntentTransitionNotAllowed(intent.Status, "bekor qilish"));
            }

            await PublishSessionPaymentStateAsync(ps.SessionId, BalanceChangeReasons.Cancelled, intentId);

            intent = await Intents.GetByIdAsync(intentId);
            return GenericDto<PaymentIntentResultDto>.Success(MapResult(intent!, "To'lov bekor qilindi."));
        }

        // ═══════════════════════ Funding ═══════════════════════

        public async Task<long> GetAvailableTiyinAsync(long sessionId)
        {
            var ps = await PaymentSessions.GetBySessionIdAsync(sessionId);
            if (ps is null || ps.Status != PaymentSessionStatus.Active)
                return 0;
            return Math.Max(0, ps.FundedTiyin - ps.ConsumedTiyin);
        }

        public async Task<decimal> ConsumeForProcessAsync(long processId)
        {
            return await Tx.RunAsync(async () =>
            {
                var process = await Processes.GetByIdWithSessionAsync(processId);
                if (process is null || process.Session is null)
                    return 0m;

                // Telemetry hot path ExecuteUpdate bilan yangilagan bo'lishi mumkin — freshlaymiz.
                await Processes.ReloadAsync(process);

                // Yagona claim — device-finished / watchdog / session-close parallel chaqirsa
                // faqat bittasi yutadi (double-settle himoyasi).
                if (!await Processes.TryClaimBalanceDeductionAsync(processId))
                    return 0m;

                var costTiyin = Money.ToTiyin(process.GivenAmount * process.PricePerUnit);
                if (costTiyin <= 0)
                    return 0m;

                var ps = await PaymentSessions.GetBySessionIdAsync(process.Session.Id);
                if (ps is null)
                {
                    Logger.LogWarning("[PAY] processId={ProcessId} uchun payment session topilmadi.", processId);
                    return 0m;
                }

                var intents = await Intents.GetByPaymentSessionAsync(ps.Id);
                long remaining = costTiyin;
                long totalApplied = 0;

                foreach (var intent in intents.OrderBy(i => i.SequenceNo))
                {
                    if (remaining <= 0) break;
                    if (intent.Status is not (PaymentIntentStatus.Funded or PaymentIntentStatus.PartiallyConsumed))
                        continue;

                    var applied = await Intents.ConsumeAtomicAsync(intent.Id, remaining);
                    if (applied <= 0) continue;

                    // Intent hisoblagichi allaqachon oshdi — sessiya hisoblagichi oshmasa ikkisi
                    // ajralib ketadi. Bu invariant buzilishi, jimgina o'tkazib bo'lmaydi.
                    if (!await PaymentSessions.TryConsumeBalanceAsync(ps.Id, applied))
                        Logger.LogError(
                            "[PAY] BALANS NOMUVOFIQLIGI: paymentSessionId={PsId} intentId={IntentId} " +
                            "applied={Applied} tiyin intent'ga yozildi, sessiya hisoblagichiga yozilmadi.",
                            ps.Id, intent.Id, applied);

                    var newConsumed = intent.ConsumedTiyin + applied;
                    var target = newConsumed >= intent.AmountTiyin
                        ? PaymentIntentStatus.FullyConsumed
                        : PaymentIntentStatus.PartiallyConsumed;
                    // PartiallyConsumed→PartiallyConsumed ruxsat jadvalida yo'q — o'sha holatda no-op.
                    if (intent.Status != target)
                        await Intents.TryTransitionAsync(intent.Id, target);

                    await LogStepAsync(intent, ps, PaymentIntentStepType.ConsumeApplied, PaymentStepStatus.Info,
                        message: $"processId={processId} applied={applied} tiyin (seq={intent.SequenceNo})");

                    remaining -= applied;
                    totalApplied += applied;
                }

                if (remaining > 0)
                    Logger.LogWarning(
                        "[PAY] Mablag' yetmadi: processId={ProcessId} cost={Cost} applied={Applied} qoldi={Remaining} tiyin",
                        processId, costTiyin, totalApplied, remaining);

                await PublishBalanceAsync(ps, BalanceChangeReasons.Consumed, null);

                return Money.ToUzs(totalApplied);
            });
        }

        // ═══════════════════════ Sessiya yakuniy hisob-kitobi ═══════════════════════

        public async Task<bool> BeginSessionSettlementAsync(long sessionId, CancellationToken ct = default)
        {
            var ps = await PaymentSessions.GetBySessionIdAsync(sessionId);
            if (ps is null)
                return false; // to'lov konteksti yo'q

            if (!await Intents.AnyNonTerminalAsync(ps.Id))
            {
                // Hammasi terminal — kutish shart emas, lekin kontekstni yopamiz.
                await PaymentSessions.TryTransitionAsync(ps.Id,
                    PaymentSessionStatus.Settled, PaymentSessionStatus.Active, PaymentSessionStatus.Settling);
                return false;
            }

            // Active → Settling (yangi intent yaratishni bloklaydi).
            await PaymentSessions.TryTransitionAsync(ps.Id, PaymentSessionStatus.Settling, PaymentSessionStatus.Active);

            var mode = await ResolveSettlementModeAsync(ps);
            var intents = await Intents.GetByPaymentSessionAsync(ps.Id);
            var now = DateTime.Now;

            foreach (var intent in intents)
            {
                switch (intent.Status)
                {
                    case PaymentIntentStatus.Funded when intent.ConsumedTiyin == 0:
                        // Umuman ishlatilmadi.
                        await AssignUnusedTargetAsync(intent, ps, mode, now);
                        break;

                    case PaymentIntentStatus.Funded:
                    case PaymentIntentStatus.PartiallyConsumed:
                    case PaymentIntentStatus.FullyConsumed:
                        // Ishlatilgan qism merchantga o'tadi. Hold'da qolgani provider tomonda
                        // avtomatik bo'shaydi; prepaid'da qoldiq alohida qaytarilmaydi
                        // (Payme chekni qisman qaytarishni qo'llamaydi) — buni step'ga yozamiz.
                        await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.SettlePending,
                            captureAmountTiyin: intent.ConsumedTiyin, nextAttemptAt: now);
                        await LogStepAsync(intent, ps, PaymentIntentStepType.SettlementTargetAssigned,
                            PaymentStepStatus.Info,
                            message: $"Consumed={intent.ConsumedTiyin} tiyin → SettlePending"
                                   + (Profile.Kind == PaymentIntentKind.Charge
                                        && intent.ConsumedTiyin < intent.AmountTiyin
                                        && !Profile.Supports(PaymentCapabilities.PartialRefund)
                                      ? $" (qoldiq {intent.AmountTiyin - intent.ConsumedTiyin} tiyin qisman qaytarilmaydi)"
                                      : string.Empty));
                        break;

                    case PaymentIntentStatus.WaitingForConfirmation:
                        // To'lanmagan / poyga — watcher bekor qiladi (to'langan bo'lib qolgan bo'lsa pul qaytadi).
                        // Ro'yxat o'qilgandan keyin to'lov tasdiqlanib ulgurgan bo'lishi mumkin,
                        // shuning uchun balansni kamaytirish qarori ENG SO'NGGI holat bo'yicha qilinadi.
                        if (await AssignRefundPendingAsync(intent.Id, now))
                            await LogStepAsync(intent, ps, PaymentIntentStepType.SettlementTargetAssigned,
                                PaymentStepStatus.Info, message: "WaitingForConfirmation → RefundPending (cancel)");
                        break;

                    case PaymentIntentStatus.Created:
                        await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Cancelled);
                        break;

                    case PaymentIntentStatus.SettlePending:
                    case PaymentIntentStatus.RefundPending:
                        // Allaqachon maqsad qo'yilgan (masalan user cancel) — darhol ishlov berilsin.
                        await Intents.SchedulePollAsync(intent.Id, now);
                        break;
                }
            }

            Logger.LogInformation(
                "[PAY] Sessiya settlement boshlandi method={Method} sessionId={SessionId} paymentSessionId={PsId} mode={Mode}",
                Profile.Method, sessionId, ps.Id, mode);
            return true;
        }

        /// <summary>Umuman ishlatilmagan mablag'ga maqsad holat qo'yish — settlement rejimiga qarab.</summary>
        private async Task AssignUnusedTargetAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, SettlementMode mode, DateTime now)
        {
            if (mode == SettlementMode.None)
            {
                // Merchant siyosati: ishlatilmagan mablag' qaytarilmaydi.
                if (!await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.SettlePending,
                        captureAmountTiyin: intent.AmountTiyin, nextAttemptAt: now))
                    return;

                await LogStepAsync(intent, ps, PaymentIntentStepType.SettlementTargetAssigned,
                    PaymentStepStatus.Info, message: "Ishlatilmagan mablag' → SettlePending (merchant qaytarmaydi)");
                return;
            }

            if (!await AssignRefundPendingAsync(intent.Id, now))
                return;

            await LogStepAsync(intent, ps, PaymentIntentStepType.SettlementTargetAssigned,
                PaymentStepStatus.Info,
                message: $"Ishlatilmagan mablag' → RefundPending (balansdan -{intent.AmountTiyin} tiyin)");
        }

        /// <summary>
        /// Intent'ni qaytarish navbatiga qo'yadi va agar mablag' sessiya balansiga qo'shilgan
        /// bo'lsa, o'shanda darhol chiqaradi.
        ///
        /// Balans AYNAN shu yerda (maqsad qo'yilganda) kamayadi, watcher refund'ni bajarganda
        /// emas — aks holda RefundPending oynasida pul hali "mavjud" ko'rinib, yangi jarayonga
        /// sarflanib ketishi mumkin edi. Ikki marta kamaytirish ham shu sababdan mumkin emas:
        /// ikkinchi chaqiruvda status allaqachon RefundPending bo'lib, o'tish rad etiladi.
        /// </summary>
        private async Task<bool> AssignRefundPendingAsync(long intentId, DateTime now)
        {
            // Eng so'nggi holat: sessiya yopilayotganda provider to'lovni tasdiqlab ulgurgan
            // bo'lishi mumkin, ya'ni ro'yxatdagi nusxa eskirgan bo'lishi mumkin.
            var fresh = await Intents.GetByIdAsync(intentId);
            if (fresh is null)
                return false;

            if (!await Intents.TryTransitionAsync(intentId, PaymentIntentStatus.RefundPending, nextAttemptAt: now))
                return false;

            if (fresh.Status == PaymentIntentStatus.Funded && fresh.ConsumedTiyin == 0)
                await PaymentSessions.TryAddFundedBalanceAsync(fresh.PaymentSessionId, -fresh.AmountTiyin);

            return true;
        }

        // ═══════════════════════ Watcher tick ═══════════════════════

        public async Task ProcessDueAsync(string ownerId, CancellationToken ct = default)
        {
            var leaseUntil = DateTime.Now.AddSeconds(Options.LeaseSeconds);
            var due = await Intents.ClaimDueAsync(Profile.Method, ownerId, leaseUntil, Options.BatchSize);

            foreach (var intent in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await ProcessOneAsync(intent, ct);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[PAY-WATCH] intentId={IntentId} ishlovida xato", intent.Id);
                    await Intents.ReleaseLeaseAsync(intent.Id, ownerId);
                }
            }
        }

        private async Task ProcessOneAsync(PaymentIntentEntity intent, CancellationToken ct)
        {
            var ps = await PaymentSessions.GetByIdAsync(intent.PaymentSessionId);
            if (ps is null)
            {
                // Kontekst yo'q — bu intent bilan ishlay olmaymiz. Lease'ni ushlab turmaymiz,
                // aks holda keyingi tick'lar uni bekorga kutib turardi.
                await Intents.ReleaseLeaseAsync(intent.Id, intent.LockedBy ?? string.Empty);
                Logger.LogWarning("[PAY-WATCH] intentId={IntentId} uchun to'lov konteksti topilmadi.", intent.Id);
                return;
            }

            if (!IsProcessable(intent))
            {
                await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Failed,
                    failureReason: NotProcessableReason);
                return;
            }

            switch (intent.Status)
            {
                case PaymentIntentStatus.WaitingForConfirmation:
                    await PollAsync(intent, ps, ct);
                    break;
                case PaymentIntentStatus.SettlePending:
                    await SettleAsync(intent, ps, ct);
                    break;
                case PaymentIntentStatus.RefundPending:
                    await RefundPendingAsync(intent, ps, ct);
                    break;
                default:
                    await Intents.SchedulePollAsync(intent.Id, DateTime.Now.AddSeconds(PollSeconds));
                    break;
            }
        }

        private async Task PollAsync(PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
        {
            var poll = await PollFundingAsync(intent, ps, ct);

            if (poll.Call.Outcome is ProviderOutcome.Transient or ProviderOutcome.Permanent)
            {
                // Transient tarmoq/provider xatosi HOLAT o'zgarishi EMAS: audit jadvaliga takroriy
                // Error step yozmaymiz (soatlab pending'da minglab qator bo'lardi) — faqat log + retry.
                Logger.LogWarning("[PAY-WATCH] poll muvaffaqiyatsiz intentId={IntentId}: {Msg}",
                    intent.Id, poll.Call.Message);
                await Intents.SchedulePollAsync(intent.Id, DateTime.Now.AddSeconds(PollSeconds));
                return;
            }

            // Audit qadamini FAQAT provider holati haqiqatan o'zgarganda yozamiz (status-spam oldi olinadi).
            if (poll.ProviderState != intent.ProviderState)
                await LogStepAsync(intent, ps, PaymentIntentStepType.CheckPolled, PaymentStepStatus.Info,
                    requestPayload: poll.Call.RequestPayload, responsePayload: poll.Call.ResponsePayload,
                    message: $"state {intent.ProviderState?.ToString() ?? "—"} → {poll.ProviderState?.ToString() ?? "—"}");

            switch (poll.State)
            {
                case FundingPollState.Funded:
                    await MarkFundedAsync(intent.Id, ps, $"provider state={poll.ProviderState}");
                    return;

                case FundingPollState.Cancelled:
                    await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Cancelled);
                    await LogStepAsync(intent, ps, PaymentIntentStepType.Cancelled, PaymentStepStatus.Info,
                        message: "Provider tomonda bekor qilingan.");
                    // Balans o'zgarmaydi, lekin status o'zgardi — UI real-time yangilanishi uchun event.
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Cancelled, intent.Id);
                    return;
            }

            // Hali kutilmoqda — TTL tekshiruvi.
            var age = DateTime.Now - intent.CreatedDate;
            if (age > TimeSpan.FromMinutes(Options.IntentTtlMinutes))
            {
                var cancelCall = await CancelUnpaidAsync(intent, ps, ct);

                // POYGA: mijoz aynan shu daqiqada to'lagan bo'lishi mumkin — bunda bekor qilish
                // rad etiladi. Tekshirmasdan Expired qilsak, pul providerda qolib, sessiya
                // balansiga tushmasdan yo'qolardi. Shuning uchun bekor qilish yiqilsa qayta so'raymiz.
                if (!cancelCall.IsSuccess)
                {
                    var recheck = await PollFundingAsync(intent, ps, ct);
                    if (recheck.State == FundingPollState.Funded)
                    {
                        Logger.LogWarning(
                            "[PAY-WATCH] intentId={IntentId} TTL tugashi bilan to'lov to'qnashdi — to'lov ustun.",
                            intent.Id);
                        await LogStepAsync(intent, ps, PaymentIntentStepType.CheckPolled, PaymentStepStatus.Info,
                            requestPayload: recheck.Call.RequestPayload, responsePayload: recheck.Call.ResponsePayload,
                            message: "TTL bilan poyga: mijoz to'lagan — Expired o'rniga Funded.");
                        await MarkFundedAsync(intent.Id, ps, "TTL poygasi: to'lov tasdiqlandi");
                        return;
                    }

                    // Holat noaniq bo'lsa (tarmoq xatosi) Expired qilmaymiz — keyingi tick aniqlaydi.
                    if (recheck.Call.Outcome is ProviderOutcome.Transient)
                    {
                        await Intents.SchedulePollAsync(intent.Id, DateTime.Now.AddSeconds(PollSeconds));
                        return;
                    }
                }

                await LogStepAsync(intent, ps, PaymentIntentStepType.Expired,
                    cancelCall.IsSuccess ? PaymentStepStatus.Info : PaymentStepStatus.Error,
                    requestPayload: cancelCall.RequestPayload, responsePayload: cancelCall.ResponsePayload,
                    message: $"TTL ({Options.IntentTtlMinutes}min) tugadi — bekor qilindi.");
                await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Expired);
                await PublishBalanceAsync(ps, BalanceChangeReasons.Expired, intent.Id);
                return;
            }

            await Intents.SchedulePollAsync(intent.Id, DateTime.Now.AddSeconds(PollSeconds), poll.ProviderState);
        }

        private async Task SettleAsync(PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
        {
            // Maqsad summa sessiya yopilayotganda yozilgan; o'sha paytda qurilmadan kelayotgan
            // oxirgi consume hali commit bo'lmagan bo'lishi mumkin edi. Consumed faqat o'sadi,
            // shuning uchun kattarog'ini olamiz — aks holda merchant kam yechib qolardi.
            var captureTiyin = Math.Max(intent.CaptureAmountTiyin ?? 0, intent.ConsumedTiyin);

            // Ishlatilmagan bo'lsa capture o'rniga to'liq qaytarish.
            if (captureTiyin <= 0)
            {
                await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.RefundPending,
                    nextAttemptAt: DateTime.Now);
                return;
            }

            await LogStepAsync(intent, ps, PaymentIntentStepType.CaptureRequested, PaymentStepStatus.Info,
                message: $"amount={captureTiyin} tiyin");

            var call = await CaptureAsync(intent, ps, captureTiyin, ct);
            await LogStepAsync(intent, ps, PaymentIntentStepType.CaptureResponded,
                call.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: call.RequestPayload, responsePayload: call.ResponsePayload, message: call.Message);

            switch (call.Outcome)
            {
                case ProviderOutcome.Success:
                case ProviderOutcome.AlreadyDone:
                    await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Settled);
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Captured, intent.Id);
                    break;
                case ProviderOutcome.Transient:
                case ProviderOutcome.Pending:
                    await BackoffOrFailAsync(intent, ps, $"Capture: {call.Message}");
                    break;
                default:
                    await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Failed,
                        failureReason: $"Capture (permanent): {call.Message}");
                    Logger.LogError("[PAY-WATCH] Capture Failed intentId={IntentId}: {Msg}", intent.Id, call.Message);
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Failed, intent.Id);
                    break;
            }
        }

        private async Task RefundPendingAsync(PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
        {
            await LogStepAsync(intent, ps, PaymentIntentStepType.RefundRequested, PaymentStepStatus.Info);

            var call = await RefundAsync(intent, ps, ct);
            await LogStepAsync(intent, ps, PaymentIntentStepType.RefundResponded,
                call.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: call.RequestPayload, responsePayload: call.ResponsePayload, message: call.Message);

            switch (call.Outcome)
            {
                case ProviderOutcome.Success:
                case ProviderOutcome.AlreadyDone:
                    await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Refunded);
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Refunded, intent.Id);
                    break;
                case ProviderOutcome.Transient:
                case ProviderOutcome.Pending:
                    await BackoffOrFailAsync(intent, ps, $"Refund: {call.Message}");
                    break;
                default:
                    await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Failed,
                        failureReason: $"Refund (permanent): {call.Message}");
                    Logger.LogError("[PAY-WATCH] Refund Failed intentId={IntentId}: {Msg}", intent.Id, call.Message);
                    await PublishBalanceAsync(ps, BalanceChangeReasons.Failed, intent.Id);
                    break;
            }
        }

        /// <summary>
        /// Intent Funded holatiga o'tkazadi, sessiya balansini oshiradi va event yuboradi.
        /// Provider callback'idan ham (Merchant API), polling'dan ham shu yagona yo'l ishlatiladi.
        /// </summary>
        protected async Task MarkFundedAsync(long intentId, PaymentSessionEntity ps, string? note = null)
        {
            var intent = await Intents.GetByIdAsync(intentId);
            if (intent is null) return;

            if (!await Intents.TryTransitionAsync(intentId, PaymentIntentStatus.Funded))
                return; // poyga: allaqachon o'tgan yoki ruxsat yo'q

            await PaymentSessions.TryAddFundedBalanceAsync(ps.Id, intent.AmountTiyin);

            await LogStepAsync(intent, ps, PaymentIntentStepType.Funded, PaymentStepStatus.Success,
                message: $"+{intent.AmountTiyin} tiyin balansga"
                       + (string.IsNullOrEmpty(note) ? string.Empty : $" ({note})"));

            await PublishBalanceAsync(ps, FundedReason, intentId);
        }

        // ═══════════════════════ Provider callback'i ═══════════════════════

        public async Task<bool> ConfirmFundingFromProviderAsync(long intentId, string? note = null)
        {
            var intent = await Intents.GetByIdAsync(intentId);
            if (intent is null) return false;

            var ps = await PaymentSessions.GetByIdAsync(intent.PaymentSessionId);
            if (ps is null) return false;

            // Provider callback'i takror kelishi mumkin — allaqachon ta'minlangan bo'lsa muvaffaqiyat.
            if (intent.Status is PaymentIntentStatus.Funded
                or PaymentIntentStatus.PartiallyConsumed
                or PaymentIntentStatus.FullyConsumed
                or PaymentIntentStatus.SettlePending
                or PaymentIntentStatus.Settled)
                return true;

            await MarkFundedAsync(intentId, ps, note);

            var after = await Intents.GetByIdAsync(intentId);
            return after?.Status == PaymentIntentStatus.Funded;
        }

        public async Task<bool> ReverseFundingFromProviderAsync(long intentId, string? note = null)
        {
            var intent = await Intents.GetByIdAsync(intentId);
            if (intent is null) return false;

            var ps = await PaymentSessions.GetByIdAsync(intent.PaymentSessionId);
            if (ps is null) return false;

            if (intent.Status == PaymentIntentStatus.Refunded)
                return true; // takroriy callback

            // Ishlatilgan mablag'ni qaytarib bo'lmaydi: xizmat allaqachon ko'rsatilgan va
            // consumed'ni orqaga qaytaradigan yo'l yo'q. Chaqiruvchi buni rad javob sifatida
            // providerga qaytarishi kerak (Payme: -31007).
            if (intent.ConsumedTiyin > 0)
            {
                Logger.LogWarning(
                    "[PAY] intentId={IntentId} qaytarilmadi — {Consumed} tiyin allaqachon ishlatilgan.",
                    intentId, intent.ConsumedTiyin);
                return false;
            }

            // State machine bo'ylab: Funded → RefundPending → Refunded (to'g'ridan sakrash yo'q).
            if (!await Intents.TryTransitionAsync(intentId, PaymentIntentStatus.RefundPending))
                return false;

            if (!await Intents.TryTransitionAsync(intentId, PaymentIntentStatus.Refunded))
                return false;

            // Ta'minlangan bo'lsa sessiya balansidan chiqaramiz (ishlatilmagan qismi bilan birga).
            if (intent.Status is PaymentIntentStatus.Funded or PaymentIntentStatus.PartiallyConsumed
                or PaymentIntentStatus.FullyConsumed)
                await PaymentSessions.TryAddFundedBalanceAsync(ps.Id, -intent.AmountTiyin);

            await LogStepAsync(intent, ps, PaymentIntentStepType.ProviderCallback, PaymentStepStatus.Info,
                message: $"Provider qaytardi: -{intent.AmountTiyin} tiyin"
                       + (string.IsNullOrEmpty(note) ? string.Empty : $" ({note})"));

            await PublishBalanceAsync(ps, BalanceChangeReasons.Refunded, intentId);
            return true;
        }

        /// <summary>Hold'da pul ushlandi, prepaid'da yechildi — event sababi shunga qarab.</summary>
        private string FundedReason => Profile.Kind == PaymentIntentKind.Hold
            ? BalanceChangeReasons.InvoiceHeld
            : BalanceChangeReasons.Funded;

        private async Task BackoffOrFailAsync(PaymentIntentEntity intent, PaymentSessionEntity ps, string reason)
        {
            if (intent.AttemptCount + 1 >= Options.MaxAttempts)
            {
                await Intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Failed,
                    failureReason: $"Retry limiti tugadi: {reason}");
                Logger.LogError("[PAY-WATCH] intentId={IntentId} retry limiti tugadi — Failed", intent.Id);
                await PublishBalanceAsync(ps, BalanceChangeReasons.Failed, intent.Id);
            }
            else
            {
                await Intents.ScheduleRetryAsync(intent.Id, NextBackoff(intent.AttemptCount), reason);
            }
        }

        private DateTime NextBackoff(int attemptCount)
        {
            var seconds = Math.Min(
                Options.BackoffBaseSeconds * Math.Pow(2, attemptCount),
                Options.BackoffMaxSeconds);
            return DateTime.Now.AddSeconds(seconds);
        }

        // ═══════════════════════ Settling → Closed ═══════════════════════

        public async Task FinalizeSettledAsync(CancellationToken ct = default)
        {
            var settling = await PaymentSessions.GetSettlingAsync(Options.BatchSize);

            foreach (var ps in settling)
            {
                if (ct.IsCancellationRequested) break;
                if (ps.Method != Profile.Method) continue;      // boshqa strategiyaning sessiyasi

                if (await Intents.AnyNonTerminalAsync(ps.Id))
                {
                    await ReleaseIfOnlyFailedAsync(ps);
                    continue;
                }

                if (!await PaymentSessions.TryTransitionAsync(ps.Id,
                        PaymentSessionStatus.Settled, PaymentSessionStatus.Settling))
                    continue;

                await CloseParentSessionAsync(ps);
            }
        }

        /// <summary>
        /// Failed intent operator aralashuvini kutadi va o'z-o'zidan yechilmaydi. Aynan shu
        /// sababli qurilma sessiyasi Settling'da abadiy qolib, foydalanuvchiga YANGI SESSIYA
        /// ochishga ham imkon bermay qo'yardi. Grace vaqtidan keyin sessiyani ozod qilamiz;
        /// to'lov konteksti esa Settling'da qoladi — moliyaviy yakun operator ro'yxatida ko'rinadi.
        /// </summary>
        private async Task ReleaseIfOnlyFailedAsync(PaymentSessionEntity ps)
        {
            // Failed'dan boshqa kutilayotgan ish bo'lsa — watcher o'zi yakunlaydi, aralashmaymiz.
            if (await Intents.AnyNonTerminalExceptFailedAsync(ps.Id))
                return;

            var stuckFor = DateTime.Now - ps.UpdatedDate;
            if (stuckFor < TimeSpan.FromMinutes(Options.SettlingGraceMinutes))
                return;

            var session = await Sessions.GetByIdAsync(ps.SessionId);
            if (session is not null && session.Status != SessionStatus.Closed)
            {
                Logger.LogError(
                    "[PAY] paymentSessionId={PsId} {Minutes} daqiqadan beri Failed intent tufayli " +
                    "hisob-kitobda qoldi — qurilma sessiyasi ozod qilindi, moliyaviy yakun operatorda.",
                    ps.Id, (int)stuckFor.TotalMinutes);

                await CloseParentSessionAsync(ps, settledFully: false);
            }

            // Bu kontekst operator hal qilguncha Settling'da qoladi. Navbat UpdatedDate bo'yicha
            // saralanadi, shuning uchun uni oxiriga suramiz — aks holda bir nechta shunday yozuv
            // batch'ni to'ldirib, YANGI sessiyalar hech qachon yakunlanmay qolardi.
            await PaymentSessions.TouchAsync(ps.Id);
        }

        private async Task CloseParentSessionAsync(PaymentSessionEntity ps, bool settledFully = true)
        {
            var session = await Sessions.GetByIdAsync(ps.SessionId);
            if (session is null || session.Status == SessionStatus.Closed)
                return;

            session.Status = SessionStatus.Closed;
            session.CloseReason ??= SessionCloseReason.UserClosed;
            session.ClosedAt = DateTime.Now;
            session.LastActivityAt = DateTime.Now;
            await Sessions.UpdateAsync(session);

            // Xabar yuborishdagi nosozlik sessiyani yopilmagan holatda qoldirmasligi kerak:
            // yozuv allaqachon saqlandi, qolgani — eng yaxshi harakat.
            try
            {
                if (session.Device is not null)
                    await Commands.PublishSessionClosedAsync(
                        session.Device.SerialNumber, session.Id, session.CloseReason.ToString()!);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[PAY] MQTT session.close yuborilmadi sessionId={SessionId}", session.Id);
            }

            try
            {
                await Notifier.NotifySessionClosedAsync(session.SessionToken, new
                {
                    session_id = session.Id,
                    reason = session.CloseReason.ToString(),
                    settled = settledFully,
                    closed_at = session.ClosedAt
                });

                await Push.SendAsync(session.UserId, new PushNotification
                {
                    Title = "Sessiya yakunlandi",
                    Body = !settledFully
                        ? "Sessiya yopildi. To'lov hisob-kitobi tekshiruvda — natijasi haqida xabar beramiz."
                        : Profile.Kind == PaymentIntentKind.Hold
                            ? "To'lov hisob-kitobi yakunlandi. Ishlatilmagan mablag' qaytarildi."
                            : "To'lov hisob-kitobi yakunlandi.",
                    DeepLink = $"botenergy://sessions/{session.Id}"
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[PAY] Sessiya yopilishi xabari yuborilmadi sessionId={SessionId}", session.Id);
            }

            Logger.LogInformation("[PAY] Settling sessiya yopildi sessionId={SessionId} settled={Settled}",
                session.Id, settledFully);
        }

        // ═══════════════════════ Event ═══════════════════════

        public async Task PublishSessionPaymentStateAsync(long sessionId, string reason, long? intentId = null)
        {
            var ps = await PaymentSessions.GetBySessionIdAsync(sessionId);
            if (ps is not null)
                await PublishBalanceAsync(ps, reason, intentId);
        }

        /// <summary>Yagona balans event: SignalR (SessionBalanceChanged) + MQTT (balance.update).</summary>
        protected async Task PublishBalanceAsync(PaymentSessionEntity ps, string reason, long? intentId)
        {
            // Yangi qiymatlarni o'qiymiz (atomik update'lardan keyin).
            var fresh = await PaymentSessions.GetByIdAsync(ps.Id) ?? ps;
            var available = Math.Max(0, fresh.FundedTiyin - fresh.ConsumedTiyin);

            var dto = new SessionBalanceChangedDto
            {
                SessionId = fresh.SessionId,
                PaymentSessionId = fresh.Id,
                IntentId = intentId,
                Method = fresh.Method,
                FundedTiyin = fresh.FundedTiyin,
                ConsumedTiyin = fresh.ConsumedTiyin,
                AvailableTiyin = available,
                AvailableUzs = Money.ToUzs(available),
                Reason = reason,
                CorrelationId = fresh.CorrelationId
            };

            var session = await Sessions.GetByIdAsync(fresh.SessionId);
            if (session is not null)
                await Notifier.NotifySessionBalanceChangedAsync(session.SessionToken, fresh.UserId, dto);

            var device = await Devices.GetByIdAsync(fresh.DeviceId);
            if (device is not null)
            {
                try { await Commands.PublishBalanceUpdateAsync(device.SerialNumber, dto); }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[PAY] MQTT balance.update yuborilmadi serial={Serial}", device.SerialNumber);
                }
            }
        }

        // ═══════════════════════ Audit ═══════════════════════

        protected Task LogStepAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps,
            PaymentIntentStepType stepType, PaymentStepStatus status,
            string? requestPayload = null, string? responsePayload = null, string? message = null)
            => Intents.AddStepAsync(new PaymentIntentStepEntity
            {
                PaymentIntentId = intent.Id,
                PaymentSessionId = ps.Id,
                SessionId = ps.SessionId,
                MerchantId = ps.MerchantId,
                DeviceId = ps.DeviceId,
                UserId = ps.UserId,
                StepType = stepType,
                Status = status,
                RequestPayload = EnsureJson(requestPayload),
                ResponsePayload = EnsureJson(responsePayload),
                Message = message,
                CorrelationId = ps.CorrelationId
            });

        /// <summary>jsonb ustuniga faqat valid JSON yozish mumkin — bo'sh/buzuq bo'lsa string sifatida o'raladi.</summary>
        protected static string? EnsureJson(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try { using var _ = JsonDocument.Parse(payload); return payload; }
            catch (JsonException) { return JsonSerializer.Serialize(payload); }
        }

        internal static PaymentIntentResultDto MapResult(PaymentIntentEntity intent, string message) => new()
        {
            IntentId = intent.Id,
            SequenceNo = intent.SequenceNo,
            Status = intent.Status,
            Method = intent.Method,
            Kind = intent.Kind,
            ProviderReceiptId = intent.ProviderReceiptId,
            AmountTiyin = intent.AmountTiyin,
            AmountUzs = Money.ToUzs(intent.AmountTiyin),
            CheckoutUrl = intent.CheckoutUrl,
            RequiresUserAction = intent.Status is PaymentIntentStatus.Created
                                              or PaymentIntentStatus.WaitingForConfirmation,
            ResultMessage = message
        };

        internal static PaymentIntentItemDto MapItem(PaymentIntentEntity i) => new()
        {
            IntentId = i.Id,
            SequenceNo = i.SequenceNo,
            Status = i.Status,
            Method = i.Method,
            Kind = i.Kind,
            AmountTiyin = i.AmountTiyin,
            ConsumedTiyin = i.ConsumedTiyin,
            ProviderReceiptId = i.ProviderReceiptId,
            CheckoutUrl = i.CheckoutUrl,
            CreatedDate = i.CreatedDate,
            FundedAt = i.FundedAt,
            SettledAt = i.SettledAt,
            FailureReason = i.FailureReason
        };
    }
}
