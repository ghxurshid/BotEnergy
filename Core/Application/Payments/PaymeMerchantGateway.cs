using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Payments;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// Payme Merchant API callback'lari. Payme bizga qo'ng'iroq qiladi, biz esa intent
    /// holatini o'zgartiramiz — barcha pul harakati YAGONA yo'l orqali
    /// (<c>ISessionPaymentStrategy.ConfirmFundingFromProviderAsync</c> /
    /// <c>ReverseFundingFromProviderAsync</c>), ya'ni balans va real-time eventlar
    /// polling oqimidagi bilan bir xil ishlaydi.
    ///
    /// Idempotentlik: Payme har bir chaqiruvni takrorlashi mumkin — barcha metodlar
    /// mavjud holatni qaytaradi, ikkinchi marta pul harakati qilmaydi.
    /// </summary>
    public class PaymeMerchantGateway : IPaymeMerchantGateway
    {
        private const string BasicLogin = "Paycom";

        private readonly IPaymentIntentRepository _intents;
        private readonly IPaymentSessionRepository _paymentSessions;
        private readonly IMerchantRepository _merchants;
        private readonly ISessionPaymentStrategyResolver _resolver;
        private readonly ITransactionRunner _tx;
        private readonly ILogger<PaymeMerchantGateway> _logger;

        public PaymeMerchantGateway(
            IPaymentIntentRepository intents,
            IPaymentSessionRepository paymentSessions,
            IMerchantRepository merchants,
            ISessionPaymentStrategyResolver resolver,
            ITransactionRunner tx,
            ILogger<PaymeMerchantGateway> logger)
        {
            _intents = intents;
            _paymentSessions = paymentSessions;
            _merchants = merchants;
            _resolver = resolver;
            _tx = tx;
            _logger = logger;
        }

        public async Task<PaymeRpcResponse> HandleAsync(
            long merchantId, string? authorizationHeader, PaymeRpcRequest request, CancellationToken ct = default)
        {
            var id = request.Id;

            try
            {
                var merchant = await _merchants.GetByIdAsync(merchantId);
                if (merchant is null || !merchant.IsActive || string.IsNullOrWhiteSpace(merchant.PaymeMerchantKey))
                    return PaymeRpcResponse.Fail(id, PaymeRpcErrors.NotAllowed());

                if (!IsAuthorized(authorizationHeader, merchant.PaymeMerchantKey!))
                {
                    _logger.LogWarning("[PAYME-MERCHANT] Auth rad etildi merchantId={MerchantId} method={Method}",
                        merchantId, request.Method);
                    return PaymeRpcResponse.Fail(id, PaymeRpcErrors.NotAllowed());
                }

                return request.Method switch
                {
                    "CheckPerformTransaction" => await CheckPerformAsync(id, request.Params, merchantId),
                    "CreateTransaction" => await CreateAsync(id, request.Params, merchantId),
                    "PerformTransaction" => await PerformAsync(id, request.Params, merchantId),
                    "CancelTransaction" => await CancelAsync(id, request.Params, merchantId),
                    "CheckTransaction" => await CheckAsync(id, request.Params, merchantId),
                    "GetStatement" => await StatementAsync(id, request.Params, merchantId),
                    _ => PaymeRpcResponse.Fail(id, PaymeRpcErrors.MethodNotFound(request.Method))
                };
            }
            catch (Exception ex)
            {
                // Payme HTTP 500 ni qayta-qayta uradi — xatoni JSON-RPC shaklida qaytaramiz.
                _logger.LogError(ex, "[PAYME-MERCHANT] method={Method} merchantId={MerchantId} ishlovida xato",
                    request.Method, merchantId);
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());
            }
        }

        // ═══════════════════════ Metodlar ═══════════════════════

        private async Task<PaymeRpcResponse> CheckPerformAsync(JsonElement id, JsonElement p, long merchantId)
        {
            var (intent, error) = await ResolveOrderAsync(p, merchantId);
            if (error is not null) return PaymeRpcResponse.Fail(id, error);

            var amountError = CheckAmount(p, intent!);
            if (amountError is not null) return PaymeRpcResponse.Fail(id, amountError);

            var payable = await IsPayableAsync(intent!);
            if (payable is not null) return PaymeRpcResponse.Fail(id, payable);

            return PaymeRpcResponse.Ok(id, new { allow = true });
        }

        private async Task<PaymeRpcResponse> CreateAsync(JsonElement id, JsonElement p, long merchantId)
        {
            var transactionId = GetString(p, "id");
            if (string.IsNullOrEmpty(transactionId))
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.InvalidRequest());

            // Takroriy CreateTransaction — mavjud tranzaksiyani o'zgarishsiz qaytaramiz.
            var existing = await _intents.GetByProviderTransactionIdAsync(transactionId);
            if (existing is not null)
            {
                if (existing.PaymentSession?.MerchantId != merchantId)
                    return PaymeRpcResponse.Fail(id, PaymeRpcErrors.TransactionNotFound());

                if (StateOf(existing) != PaymeTransactionStates.Created)
                    return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

                if (await CancelIfTimedOutAsync(existing))
                    return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

                return PaymeRpcResponse.Ok(id, new
                {
                    create_time = ToUnixMs(existing.ProviderCreatedAt),
                    transaction = existing.Id.ToString(),
                    state = PaymeTransactionStates.Created
                });
            }

            var (intent, error) = await ResolveOrderAsync(p, merchantId);
            if (error is not null) return PaymeRpcResponse.Fail(id, error);

            var amountError = CheckAmount(p, intent!);
            if (amountError is not null) return PaymeRpcResponse.Fail(id, amountError);

            var payable = await IsPayableAsync(intent!);
            if (payable is not null) return PaymeRpcResponse.Fail(id, payable);

            // Bitta buyurtmaga ikkinchi tranzaksiya ochilmaydi.
            if (!string.IsNullOrEmpty(intent!.ProviderTransactionId))
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

            intent.ProviderTransactionId = transactionId;
            intent.ProviderTransactionTime = GetInt64(p, "time");
            intent.ProviderCreatedAt = DateTime.Now;
            intent.ProviderState = PaymeTransactionStates.Created;
            await _intents.UpdateAsync(intent);

            await LogCallbackAsync(intent, $"CreateTransaction id={transactionId}");

            return PaymeRpcResponse.Ok(id, new
            {
                create_time = ToUnixMs(intent.ProviderCreatedAt),
                transaction = intent.Id.ToString(),
                state = PaymeTransactionStates.Created
            });
        }

        private async Task<PaymeRpcResponse> PerformAsync(JsonElement id, JsonElement p, long merchantId)
        {
            var (intent, error) = await ResolveTransactionAsync(p, merchantId);
            if (error is not null) return PaymeRpcResponse.Fail(id, error);

            // Takroriy Perform — o'sha javob.
            if (intent!.ProviderPerformedAt is not null)
                return PaymeRpcResponse.Ok(id, new
                {
                    transaction = intent.Id.ToString(),
                    perform_time = ToUnixMs(intent.ProviderPerformedAt),
                    state = PaymeTransactionStates.Performed
                });

            if (intent.ProviderCancelledAt is not null)
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

            if (await CancelIfTimedOutAsync(intent))
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

            var strategy = _resolver.ForMethod(PaymentMethod.Merchant);
            if (strategy is null)
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

            // Balansni oshirish va "bajarildi" vaqt tamg'asi BITTA tranzaksiyada bo'lishi shart:
            // orasida uzilsa, pul sessiyada bo'lib turib, CheckTransaction "hali bajarilmagan"
            // deb javob berardi va Payme uni bekor qilib yuborardi.
            var performed = await _tx.RunAsync(async () =>
            {
                if (!await strategy.ConfirmFundingFromProviderAsync(intent.Id, "PerformTransaction"))
                    return false;

                var fresh = await _intents.GetByIdAsync(intent.Id);
                if (fresh is null) return false;

                fresh.ProviderPerformedAt = DateTime.Now;
                fresh.ProviderState = PaymeTransactionStates.Performed;
                await _intents.UpdateAsync(fresh);
                return true;
            });

            if (!performed)
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

            intent = await _intents.GetByIdAsync(intent.Id);
            await LogCallbackAsync(intent!, $"PerformTransaction — {intent!.AmountTiyin} tiyin ta'minlandi");

            return PaymeRpcResponse.Ok(id, new
            {
                transaction = intent.Id.ToString(),
                perform_time = ToUnixMs(intent.ProviderPerformedAt),
                state = PaymeTransactionStates.Performed
            });
        }

        private async Task<PaymeRpcResponse> CancelAsync(JsonElement id, JsonElement p, long merchantId)
        {
            var (intent, error) = await ResolveTransactionAsync(p, merchantId);
            if (error is not null) return PaymeRpcResponse.Fail(id, error);

            var reason = GetInt32(p, "reason");

            // Takroriy Cancel — o'sha javob.
            if (intent!.ProviderCancelledAt is not null)
                return PaymeRpcResponse.Ok(id, CancelResult(intent));

            // Mablag' xizmatga ketgan bo'lsa qaytarib bo'lmaydi (Payme buni -31007 deb kutadi).
            if (intent.ConsumedTiyin > 0
                || intent.Status is PaymentIntentStatus.SettlePending or PaymentIntentStatus.Settled)
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotCancel());

            // Intent BIZNING tomonimizda allaqachon yopilgan bo'lsa (TTL, user cancel, sessiya
            // yakuni) — Payme uchun bu muvaffaqiyat: pul harakati bo'lmagan, faqat vaqt tamg'asini
            // qo'yamiz. Aks holda Payme cheksiz qayta urinardi.
            if (intent.Status is PaymentIntentStatus.Cancelled or PaymentIntentStatus.Expired)
                return PaymeRpcResponse.Ok(id, await StampCancelAsync(intent, reason));

            var strategy = _resolver.ForMethod(PaymentMethod.Merchant);
            if (strategy is null)
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());

            // Qaror faqat ProviderPerformedAt'ga qarab qilinmaydi: Perform paytida uzilish bo'lsa
            // pul sessiyada bo'lib, vaqt tamg'asi yozilmay qolishi mumkin. Shuning uchun HAQIQIY
            // to'lov holati ham hisobga olinadi — aks holda balans kamaytirilmay qolardi.
            var wasFunded = intent.ProviderPerformedAt is not null
                || intent.Status is PaymentIntentStatus.Funded
                or PaymentIntentStatus.PartiallyConsumed
                or PaymentIntentStatus.FullyConsumed;

            if (!wasFunded)
            {
                // Hali to'lanmagan — intent'ni yopamiz, pul harakati yo'q.
                if (!await _intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Cancelled))
                    return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotPerform());
            }
            else if (!await _tx.RunAsync(() =>
                         strategy.ReverseFundingFromProviderAsync(intent.Id, $"CancelTransaction reason={reason}")))
            {
                return PaymeRpcResponse.Fail(id, PaymeRpcErrors.CannotCancel());
            }

            var result = await StampCancelAsync(intent, reason);
            return PaymeRpcResponse.Ok(id, result);
        }

        private async Task<PaymeRpcResponse> CheckAsync(JsonElement id, JsonElement p, long merchantId)
        {
            var (intent, error) = await ResolveTransactionAsync(p, merchantId);
            if (error is not null) return PaymeRpcResponse.Fail(id, error);

            return PaymeRpcResponse.Ok(id, new
            {
                create_time = ToUnixMs(intent!.ProviderCreatedAt),
                perform_time = ToUnixMs(intent.ProviderPerformedAt),
                cancel_time = ToUnixMs(intent.ProviderCancelledAt),
                transaction = intent.Id.ToString(),
                state = StateOf(intent),
                reason = intent.ProviderCancelReason
            });
        }

        private async Task<PaymeRpcResponse> StatementAsync(JsonElement id, JsonElement p, long merchantId)
        {
            var from = FromUnixMs(GetInt64(p, "from")) ?? DateTime.Now.AddDays(-1);
            var to = FromUnixMs(GetInt64(p, "to")) ?? DateTime.Now;

            var intents = await _intents.GetForMerchantRangeAsync(merchantId, PaymentMethod.Merchant, from, to);

            var transactions = intents.Select(i => new
            {
                id = i.ProviderTransactionId,
                time = i.ProviderTransactionTime ?? ToUnixMs(i.ProviderCreatedAt),
                amount = i.AmountTiyin,
                account = new { order_id = i.ProviderOrderId },
                create_time = ToUnixMs(i.ProviderCreatedAt),
                perform_time = ToUnixMs(i.ProviderPerformedAt),
                cancel_time = ToUnixMs(i.ProviderCancelledAt),
                transaction = i.Id.ToString(),
                state = StateOf(i),
                reason = i.ProviderCancelReason
            }).ToList();

            return PaymeRpcResponse.Ok(id, new { transactions });
        }

        // ═══════════════════════ Yordamchi ═══════════════════════

        /// <summary>Bekor qilish vaqt tamg'asini qo'yadi va Payme javobini yasaydi.</summary>
        private async Task<object> StampCancelAsync(PaymentIntentEntity intent, int reason)
        {
            var fresh = await _intents.GetByIdAsync(intent.Id) ?? intent;

            fresh.ProviderCancelledAt ??= DateTime.Now;
            fresh.ProviderCancelReason = reason;
            fresh.ProviderState = StateOf(fresh);
            await _intents.UpdateAsync(fresh);

            await LogCallbackAsync(fresh, $"CancelTransaction reason={reason} → state={StateOf(fresh)}");
            return CancelResult(fresh);
        }

        private static object CancelResult(PaymentIntentEntity intent) => new
        {
            transaction = intent.Id.ToString(),
            cancel_time = ToUnixMs(intent.ProviderCancelledAt),
            state = StateOf(intent)
        };

        /// <summary>account.order_id bo'yicha intent topadi va merchant mosligini tekshiradi.</summary>
        private async Task<(PaymentIntentEntity? intent, PaymeRpcError? error)> ResolveOrderAsync(
            JsonElement p, long merchantId)
        {
            var orderId = GetOrderId(p);
            if (string.IsNullOrEmpty(orderId))
                return (null, PaymeRpcErrors.OrderNotFound());

            var intent = await _intents.GetByProviderOrderIdAsync(orderId);
            if (intent is null || intent.Method != PaymentMethod.Merchant)
                return (null, PaymeRpcErrors.OrderNotFound());

            if (intent.PaymentSession?.MerchantId != merchantId)
                return (null, PaymeRpcErrors.OrderNotFound());

            return (intent, null);
        }

        private async Task<(PaymentIntentEntity? intent, PaymeRpcError? error)> ResolveTransactionAsync(
            JsonElement p, long merchantId)
        {
            var transactionId = GetString(p, "id");
            if (string.IsNullOrEmpty(transactionId))
                return (null, PaymeRpcErrors.TransactionNotFound());

            var intent = await _intents.GetByProviderTransactionIdAsync(transactionId);
            if (intent is null || intent.PaymentSession?.MerchantId != merchantId)
                return (null, PaymeRpcErrors.TransactionNotFound());

            return (intent, null);
        }

        private static PaymeRpcError? CheckAmount(JsonElement p, PaymentIntentEntity intent)
            => GetInt64(p, "amount") == intent.AmountTiyin ? null : PaymeRpcErrors.InvalidAmount();

        /// <summary>Buyurtma hozir to'lanishi mumkinmi (holat + to'lov konteksti ochiqmi).</summary>
        private async Task<PaymeRpcError?> IsPayableAsync(PaymentIntentEntity intent)
        {
            if (intent.Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.WaitingForConfirmation))
                return PaymeRpcErrors.OrderNotPayable();

            var ps = intent.PaymentSession ?? await _paymentSessions.GetByIdAsync(intent.PaymentSessionId);
            return ps is null || ps.Status != PaymentSessionStatus.Active
                ? PaymeRpcErrors.OrderNotPayable()
                : null;
        }

        /// <summary>
        /// 12 soatlik muddat o'tgan bo'lsa tranzaksiyani bekor qiladi (Payme talabi, reason=4).
        /// true — bekor qilindi, chaqiruvchi -31008 qaytarishi kerak.
        /// </summary>
        private async Task<bool> CancelIfTimedOutAsync(PaymentIntentEntity intent)
        {
            if (intent.ProviderCreatedAt is null || intent.ProviderPerformedAt is not null)
                return false;

            if (DateTime.Now - intent.ProviderCreatedAt.Value <= PaymeMerchantLimits.TransactionTimeout)
                return false;

            await _intents.TryTransitionAsync(intent.Id, PaymentIntentStatus.Expired);

            intent.ProviderCancelledAt = DateTime.Now;
            intent.ProviderCancelReason = PaymeMerchantLimits.TimeoutCancelReason;
            intent.ProviderState = PaymeTransactionStates.CancelledBeforePerform;
            await _intents.UpdateAsync(intent);

            _logger.LogWarning("[PAYME-MERCHANT] intentId={IntentId} 12 soatlik muddat o'tdi — bekor qilindi.",
                intent.Id);
            return true;
        }

        private async Task LogCallbackAsync(PaymentIntentEntity intent, string message)
        {
            var ps = intent.PaymentSession ?? await _paymentSessions.GetByIdAsync(intent.PaymentSessionId);
            if (ps is null) return;

            await _intents.AddStepAsync(new PaymentIntentStepEntity
            {
                PaymentIntentId = intent.Id,
                PaymentSessionId = ps.Id,
                SessionId = ps.SessionId,
                MerchantId = ps.MerchantId,
                DeviceId = ps.DeviceId,
                UserId = ps.UserId,
                StepType = PaymentIntentStepType.ProviderCallback,
                Status = PaymentStepStatus.Info,
                Message = message,
                CorrelationId = ps.CorrelationId
            });
        }

        /// <summary>Bizning intent holatidan Payme tranzaksiya holatini hisoblaydi.</summary>
        private static int StateOf(PaymentIntentEntity intent)
        {
            if (intent.ProviderCancelledAt is not null)
                return intent.ProviderPerformedAt is not null
                    ? PaymeTransactionStates.CancelledAfterPerform
                    : PaymeTransactionStates.CancelledBeforePerform;

            if (intent.ProviderPerformedAt is not null)
                return PaymeTransactionStates.Performed;

            // Vaqt tamg'asi yozilmay qolgan, lekin mablag' sessiyaga tushgan holat ham
            // "bajarilgan" hisoblanadi — aks holda Payme uni bekor qilib yuborardi.
            if (intent.Status is PaymentIntentStatus.Funded
                or PaymentIntentStatus.PartiallyConsumed
                or PaymentIntentStatus.FullyConsumed
                or PaymentIntentStatus.SettlePending
                or PaymentIntentStatus.Settled)
                return PaymeTransactionStates.Performed;

            return PaymeTransactionStates.Created;
        }

        /// <summary>Basic Paycom:&lt;key&gt; — parol doimiy vaqtda solishtiriladi.</summary>
        private static bool IsAuthorized(string? header, string expectedKey)
        {
            if (string.IsNullOrWhiteSpace(header) ||
                !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return false;

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
            }
            catch (FormatException)
            {
                return false;
            }

            var separator = decoded.IndexOf(':');
            if (separator <= 0) return false;

            var login = decoded[..separator];
            var password = decoded[(separator + 1)..];

            if (!string.Equals(login, BasicLogin, StringComparison.Ordinal))
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(password),
                Encoding.UTF8.GetBytes(expectedKey));
        }

        // ── JSON o'qish (Payme params shakli qat'iy emas) ───────────

        private static string? GetString(JsonElement p, string name)
            => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static long GetInt64(JsonElement p, string name)
            => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.TryGetInt64(out var i)
                ? i
                : 0;

        private static int GetInt32(JsonElement p, string name)
            => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.TryGetInt32(out var i)
                ? i
                : 0;

        private static string? GetOrderId(JsonElement p)
        {
            if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("account", out var account))
                return null;

            if (account.ValueKind == JsonValueKind.Object &&
                account.TryGetProperty("order_id", out var oid))
                return oid.ValueKind == JsonValueKind.String ? oid.GetString() : oid.ToString();

            return null;
        }

        private static long? ToUnixMs(DateTime? value)
            => value is null
                ? null
                : new DateTimeOffset(
                    DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified),
                    TimeZoneInfo.Local.GetUtcOffset(value.Value)).ToUnixTimeMilliseconds();

        private static DateTime? FromUnixMs(long ms)
            => ms <= 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
    }
}
