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
    /// Payme Receipts API — chek mijozga yuboriladi, u Payme ilovasida to'laydi.
    /// Hold YO'Q: pul darhol yechiladi (<see cref="PaymentIntentKind.Charge"/>).
    ///
    /// Oqim: <c>receipts.create</c> (hold'siz) → <c>receipts.send(phone)</c> → mijoz to'laydi →
    /// watcher <c>receipts.check</c> bilan <c>Paid</c> holatini ko'radi va mablag'ni sessiya
    /// balansiga qo'shadi → dispense FIFO consume → sessiya yopilganda ishlatilmagan chek
    /// <c>receipts.cancel</c> bilan to'liq qaytariladi.
    ///
    /// CHEKLOV: Payme chekni QISMAN qaytarishni qo'llamaydi. Shuning uchun qisman ishlatilgan
    /// chek qoldig'i qaytarilmaydi — bu holat audit step'ida aniq yozib qo'yiladi
    /// (<c>SettlementTargetAssigned</c>) va admin ro'yxatida ko'rinadi. Hold kerak bo'lsa
    /// merchant <see cref="PaymentMethod.Subscribe"/> usulini tanlashi kerak.
    /// </summary>
    public class InvoicePaymentStrategy : PaymentStrategyBase
    {
        private readonly IPaymeClient _payme;
        private readonly IPaymeCredentialResolver _credResolver;
        private readonly IMerchantRepository _merchants;
        private readonly ICustomerUserRepository _users;

        public InvoicePaymentStrategy(
            PaymentStrategyDependencies deps,
            IPaymeClient payme,
            IPaymeCredentialResolver credResolver,
            IMerchantRepository merchants,
            ICustomerUserRepository users,
            ILogger<InvoicePaymentStrategy> logger)
            : base(deps, logger)
        {
            _payme = payme;
            _credResolver = credResolver;
            _merchants = merchants;
            _users = users;
        }

        public override PaymentStrategyProfile Profile { get; } = new(
            PaymentMethod.Invoice,
            PaymentIntentKind.Charge,
            // PartialRefund YO'Q — Payme chekni qisman qaytarmaydi.
            PaymentCapabilities.Refund,
            SettlementMode.RefundRemainderOnClose);

        protected override string CreatedMessage(PaymentIntentEntity intent)
            => "Chek yuborildi — Payme ilovasida to'lovni yakunlang.";

        // ── To'siqlar ───────────────────────────────────────────────

        protected override async Task<StopFactor?> ValidateCreateAsync(
            PaymentSessionEntity ps, CreatePaymentIntentDto dto)
        {
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return StopFactors.Merchant.PaymeNotConfigured;

            // Chek mijozga telefon orqali yetkaziladi — raqamsiz u to'lay olmaydi.
            if (string.IsNullOrWhiteSpace(await ResolvePhoneAsync(ps, dto)))
                return StopFactors.Payment.PhoneRequired;

            return null;
        }

        /// <summary>Merchant siyosati: ishlatilmagan mablag' qaytarilsinmi.</summary>
        protected override async Task<SettlementMode> ResolveSettlementModeAsync(PaymentSessionEntity ps)
        {
            var merchant = await _merchants.GetByIdAsync(ps.MerchantId);
            return merchant is { RefundUnusedFunds: false }
                ? SettlementMode.None
                : SettlementMode.RefundRemainderOnClose;
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
                intent.AmountTiyin, intent.ProviderOrderId, hold: false,
                description: $"BotEnergy sessiya #{ps.SessionId}", creds: creds, ct: ct);

            if (!create.IsSuccess)
                return new ProviderFunding(ToCall(create));

            var receiptId = create.Result!.Id;
            var phone = await ResolvePhoneAsync(ps, dto);

            // Yetkazish — bu usulning YAGONA kanali, shuning uchun xatosi ham audit'ga tushadi.
            var send = await _payme.SendReceiptAsync(receiptId, phone!, creds, ct);
            await LogStepAsync(intent, ps, PaymentIntentStepType.DeliveryRequested,
                send.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: send.RequestBody, responsePayload: send.ResponseBody,
                message: send.IsSuccess
                    ? $"Chek {Mask(phone!)} raqamiga yuborildi"
                    : $"Chekni yuborib bo'lmadi: {send.FailureMessage}");

            if (!send.IsSuccess)
            {
                // Mijozga yetib bormagan chek osilib qolmasin.
                await _payme.CancelReceiptAsync(receiptId, creds, ct);
                return new ProviderFunding(ToCall(send), ReceiptId: receiptId);
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
                // Hold'siz chekda to'langan holat — faqat Paid.
                PaymeReceiptStates.Paid => FundingPollState.Funded,
                PaymeReceiptStates.Cancelled => FundingPollState.Cancelled,
                _ => FundingPollState.Pending
            };

            return new ProviderPoll(ToCall(check), pollState, state);
        }

        /// <summary>
        /// Prepaid: pul allaqachon merchant hisobida — provider chaqiruvi shart emas,
        /// intent shunchaki Settled bo'ladi.
        /// </summary>
        protected override Task<ProviderCall> CaptureAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, long amountTiyin, CancellationToken ct)
            => Task.FromResult(ProviderCall.NoOp(
                $"Prepaid: {amountTiyin} tiyin allaqachon yechilgan, capture chaqiruvi shart emas."));

        protected override async Task<ProviderCall> RefundAsync(
            PaymentIntentEntity intent, PaymentSessionEntity ps, CancellationToken ct)
        {
            var creds = await _credResolver.ForMerchantAsync(ps.MerchantId);
            if (creds is null)
                return new ProviderCall(ProviderOutcome.Transient, Message: "Merchant Payme kassasi sozlanmagan.");

            var call = await _payme.CancelReceiptAsync(intent.ProviderReceiptId!, creds, ct);
            var outcome = PaymeErrorClassifier.Classify(call);

            if (outcome == PaymeCallOutcome.AlreadyDone)
            {
                var check = await _payme.CheckReceiptAsync(intent.ProviderReceiptId!, creds, ct);
                outcome = check.IsSuccess && check.Result!.State == PaymeReceiptStates.Cancelled
                    ? PaymeCallOutcome.AlreadyDone
                    : PaymeCallOutcome.Transient;
            }

            return ToCall(call, outcome);
        }

        // ── Yordamchi ───────────────────────────────────────────────

        /// <summary>So'rovdagi telefon, bo'lmasa foydalanuvchi profilidagi raqam.</summary>
        private async Task<string?> ResolvePhoneAsync(PaymentSessionEntity ps, CreatePaymentIntentDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.Phone))
                return dto.Phone!.Trim();

            var user = await _users.GetByIdAsync(ps.UserId);
            return string.IsNullOrWhiteSpace(user?.PhoneNumber) ? null : user!.PhoneNumber;
        }

        /// <summary>Audit'da to'liq raqam yotmasin.</summary>
        private static string Mask(string phone)
            => phone.Length <= 4 ? "****" : phone[..3] + "****" + phone[^2..];

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
