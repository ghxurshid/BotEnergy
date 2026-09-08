using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces.Payme;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// Payme Subscribe API — YAGONA hold (pre-authorization) qo'llaydigan strategiya.
    ///
    /// Oqim: <c>receipts.create(hold:true)</c> → mijozning SAQLANGAN kartasi bo'lsa server
    /// <c>receipts.pay(token)</c> bilan darhol to'laydi (mijoz Payme ilovasiga o'tmaydi),
    /// aks holda mijoz chekni o'zi tasdiqlaydi → chek <c>Held</c> holatiga o'tadi va pul
    /// kartada BLOKLANADI → dispense FIFO consume → sessiya yopilganda
    /// <c>receipts.confirm_hold(consumed)</c> ishlatilgan qismni yechadi, qolgani bo'shaydi.
    ///
    /// Payme'ga tegadigan barcha chaqiruvlar SHU YERDA; umumiy mantiq
    /// <see cref="PaymentStrategyBase"/> da.
    /// </summary>
    public class SubscribePaymentStrategy : PaymentStrategyBase
    {
        private readonly IPaymeClient _payme;
        private readonly IPaymeCredentialResolver _credResolver;
        private readonly ICustomerCardRepository _cards;

        public SubscribePaymentStrategy(
            PaymentStrategyDependencies deps,
            IPaymeClient payme,
            IPaymeCredentialResolver credResolver,
            ICustomerCardRepository cards,
            ILogger<SubscribePaymentStrategy> logger)
            : base(deps, logger)
        {
            _payme = payme;
            _credResolver = credResolver;
            _cards = cards;
        }

        public override PaymentStrategyProfile Profile { get; } = new(
            PaymentMethod.Subscribe,
            PaymentIntentKind.Hold,
            PaymentCapabilities.Hold
                | PaymentCapabilities.PartialCapture
                | PaymentCapabilities.Refund
                | PaymentCapabilities.SavedCard,
            SettlementMode.CaptureOnClose);

        protected override string CreatedMessage(PaymentIntentEntity intent)
            => intent.Status == PaymentIntentStatus.Funded
                ? "To'lov tasdiqlandi — mablag' kartangizda bloklandi."
                : "Hold yaratildi — Payme ilovasida to'lovni tasdiqlang.";

        // ── To'siqlar ───────────────────────────────────────────────

        protected override async Task<StopFactor?> ValidateCreateAsync(
            PaymentSessionEntity ps, CreatePaymentIntentDto dto)
        {
            // Fallback YO'Q: merchant kassasi sozlanmagan bo'lsa hold yaratilmaydi.
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return StopFactors.Merchant.PaymeNotConfigured;

            // Karta aniq ko'rsatilgan bo'lsa — u yaroqli ekanini OLDINDAN tekshiramiz
            // (aks holda chek yaratilib, to'lash bosqichida yiqilardi).
            if (dto.CardId is not null)
            {
                var card = await _cards.GetByIdAsync(dto.CardId.Value);
                if (card is null) return StopFactors.Payment.CardNotFound;
                if (card.UserId != dto.UserId) return StopFactors.Payment.CardNotOwned;
                if (card.MerchantId != ps.MerchantId) return StopFactors.Payment.CardWrongMerchant;
                if (!card.IsVerified) return StopFactors.Payment.CardNotVerified;
            }

            return null;
        }

        // ── Provider chaqiruvlari ───────────────────────────────────

        protected override async Task<ProviderFunding> RequestFundingAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CreatePaymentIntentDto dto, CancellationToken ct)
        {
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return new ProviderFunding(new ProviderCall(
                    ProviderOutcome.Permanent, Message: "Merchant Payme kassasi sozlanmagan."));

            var create = await _payme.CreateReceiptAsync(
                intent.AmountTiyin, intent.ProviderOrderId, hold: true,
                description: $"BotEnergy sessiya #{ps.SessionId} hold", creds: creds, ct: ct);

            if (!create.IsSuccess)
                return new ProviderFunding(ToCall(create));

            var receiptId = create.Result!.Id;

            // ── Saqlangan karta bo'lsa — server o'zi to'laydi ──
            var card = dto.CardId is not null
                ? await _cards.GetByIdAsync(dto.CardId.Value)
                : await _cards.GetUsableAsync(dto.UserId, ps.MerchantId);

            if (card is { IsVerified: true } && card.MerchantId == ps.MerchantId && card.UserId == dto.UserId)
            {
                var pay = await _payme.PayReceiptAsync(receiptId, card.Token, creds, ct);

                await LogStepAsync(intent, ps, PaymentIntentStepType.FundingRequested,
                    pay.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                    requestPayload: pay.RequestBody, responsePayload: pay.ResponseBody,
                    message: pay.IsSuccess
                        ? $"receipts.pay saqlangan karta bilan (cardId={card.Id})"
                        : $"receipts.pay rad etildi (cardId={card.Id}): {pay.FailureMessage}");

                if (!pay.IsSuccess)
                {
                    // To'lanmagan chek osilib qolmasin — darhol bekor qilamiz.
                    await _payme.CancelReceiptAsync(receiptId, creds, ct);
                    return new ProviderFunding(ToCall(pay), ReceiptId: receiptId);
                }

                intent.CustomerCardId = card.Id;
                card.LastUsedAt = DateTime.Now;
                await _cards.UpdateAsync(card);

                var paidState = pay.Result!.State;
                var funded = paidState is PaymeReceiptStates.Held or PaymeReceiptStates.Paid;

                return new ProviderFunding(
                    ToCall(pay),
                    ReceiptId: receiptId,
                    ProviderState: paidState,
                    // Kutilmagan holat kelsa AlreadyFunded qo'ymaymiz — watcher polling aniqlaydi.
                    AlreadyFunded: funded);
            }

            // Karta yo'q — mijoz Payme ilovasida o'zi tasdiqlaydi (watcher polling kutadi).
            if (Options.SendReceiptToPhone && !string.IsNullOrWhiteSpace(dto.Phone))
            {
                var send = await _payme.SendReceiptAsync(receiptId, dto.Phone!, creds, ct);
                await LogStepAsync(intent, ps, PaymentIntentStepType.DeliveryRequested,
                    send.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                    requestPayload: send.RequestBody, responsePayload: send.ResponseBody,
                    message: send.FailureMessage);
                // Yuborish xatosi kritik emas — mijoz ilovada ham to'lay oladi.
            }

            return new ProviderFunding(
                ToCall(create),
                ReceiptId: receiptId,
                ProviderState: create.Result.State);
        }

        protected override async Task<ProviderPoll> PollFundingAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
        {
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return new ProviderPoll(new ProviderCall(
                    ProviderOutcome.Transient, Message: "Merchant Payme kassasi sozlanmagan."));

            var check = await _payme.CheckReceiptAsync(intent.ProviderReceiptId!, creds, ct);
            if (!check.IsSuccess)
                return new ProviderPoll(ToCall(check));

            var state = check.Result!.State;
            var pollState = state switch
            {
                PaymeReceiptStates.Held or PaymeReceiptStates.Paid => FundingPollState.Funded,
                PaymeReceiptStates.Cancelled => FundingPollState.Cancelled,
                _ => FundingPollState.Pending
            };

            return new ProviderPoll(ToCall(check), pollState, state);
        }

        protected override async Task<ProviderCall> CaptureAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, long amountTiyin, CancellationToken ct)
        {
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return new ProviderCall(ProviderOutcome.Transient, Message: "Merchant Payme kassasi sozlanmagan.");

            // Qisman capture: qolgan summa Payme tomonda avtomatik bo'shaydi.
            var call = await _payme.ConfirmHoldAsync(intent.ProviderReceiptId!, amountTiyin, creds, ct);
            var outcome = PaymeErrorClassifier.Classify(call);

            // "Holat mos emas" — chek allaqachon yechilganmi tekshiramiz.
            if (outcome == PaymeCallOutcome.AlreadyDone)
                outcome = await ReconcileAsync(intent, creds, PaymeReceiptStates.Paid, ct);

            return ToCall(call, outcome);
        }

        protected override async Task<ProviderCall> RefundAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
        {
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return new ProviderCall(ProviderOutcome.Transient, Message: "Merchant Payme kassasi sozlanmagan.");

            var call = await _payme.CancelReceiptAsync(intent.ProviderReceiptId!, creds, ct);
            var outcome = PaymeErrorClassifier.Classify(call);

            if (outcome == PaymeCallOutcome.AlreadyDone)
                outcome = await ReconcileAsync(intent, creds, PaymeReceiptStates.Cancelled, ct);

            return ToCall(call, outcome);
        }

        /// <summary>
        /// StateMismatch kelganda receipts.check bilan haqiqiy holatni aniqlaydi:
        /// kutilgan terminal holatda bo'lsa AlreadyDone (idempotent success), aks holda Transient.
        /// </summary>
        private async Task<PaymeCallOutcome> ReconcileAsync(
            PaymentIntentEntity intent, PaymeCredentials creds, int expectedState, CancellationToken ct)
        {
            var check = await _payme.CheckReceiptAsync(intent.ProviderReceiptId!, creds, ct);
            return check.IsSuccess && check.Result!.State == expectedState
                ? PaymeCallOutcome.AlreadyDone
                : PaymeCallOutcome.Transient;
        }

        // ── Payme natijasini neytral shaklga o'girish ───────────────

        private static ProviderCall ToCall<T>(PaymeApiCall<T> call, PaymeCallOutcome? outcome = null)
            where T : class
            => new(
                Map(outcome ?? PaymeErrorClassifier.Classify(call)),
                call.RequestBody,
                call.ResponseBody,
                call.Error?.Message ?? call.FailureMessage);

        private static ProviderOutcome Map(PaymeCallOutcome outcome) => outcome switch
        {
            PaymeCallOutcome.Success => ProviderOutcome.Success,
            PaymeCallOutcome.AlreadyDone => ProviderOutcome.AlreadyDone,
            PaymeCallOutcome.Transient => ProviderOutcome.Transient,
            _ => ProviderOutcome.Permanent
        };
    }
}
