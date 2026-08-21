using System.Collections.Concurrent;
using Domain.Helpers;
using Domain.Interfaces.Bank;
using Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommonConfiguration.Payments.Bank
{
    /// <summary>
    /// <see cref="ICardPayoutClient"/> ning soxta implementatsiyasi — haqiqiy bank
    /// integratsiyasi kelguncha ishlatiladi.
    ///
    /// Xulq-atvori <c>Bank:Fake</c> sozlamalari bilan boshqariladi, shu sababli
    /// <c>PayoutFailed</c> va watcher qayta urinishlarini haqiqiy bank'siz sinab bo'ladi.
    ///
    /// O'tkazmalar xotirada saqlanadi (<see cref="GetPayoutStatusAsync"/> ishlashi uchun) —
    /// servis qayta ishga tushsa yo'qoladi, bu soxta klient uchun maqbul.
    /// </summary>
    public sealed class FakeCardPayoutClient : ICardPayoutClient
    {
        private static readonly ConcurrentDictionary<string, CardPayout> Payouts = new();

        // Bir orderId uchun bitta natija — takroriy payout ikkinchi o'tkazma yasamaydi
        // (haqiqiy bank ham idempotentlikni order_id bo'yicha ta'minlaydi).
        private static readonly ConcurrentDictionary<string, string> OrderToRef = new();

        private readonly FakeBankOptions _options;
        private readonly ILogger<FakeCardPayoutClient> _logger;
        private readonly Random _random = new();

        public FakeCardPayoutClient(IOptions<BankOptions> options, ILogger<FakeCardPayoutClient> logger)
        {
            _options = options.Value.Fake;
            _logger = logger;
        }

        public async Task<CardPayoutCall<CardVerification>> VerifyCardAsync(string pan, CancellationToken ct = default)
        {
            await DelayAsync(ct);

            var normalized = CardNumberHelper.Normalize(pan);
            var masked = CardNumberHelper.Mask(normalized);

            if (!CardNumberHelper.IsValid(normalized))
            {
                // Log'ga faqat maska — to'liq PAN hech qachon yozilmaydi.
                _logger.LogWarning("[FAKE-BANK] Karta yaroqsiz: {Masked}", masked);
                return CardPayoutCall<CardVerification>.Rejected("INVALID_CARD", "Karta raqami yaroqsiz.");
            }

            if (!string.IsNullOrEmpty(_options.RejectCardPrefix) &&
                normalized.StartsWith(_options.RejectCardPrefix, StringComparison.Ordinal))
            {
                _logger.LogWarning("[FAKE-BANK] Karta rad etildi (test prefiksi): {Masked}", masked);
                return CardPayoutCall<CardVerification>.Rejected("CARD_BLOCKED", "Karta bloklangan yoki mavjud emas.");
            }

            // Token — PAN'dan qayta tiklab bo'lmaydigan sun'iy qiymat.
            var token = "tok_" + Guid.NewGuid().ToString("N");

            _logger.LogInformation("[FAKE-BANK] Karta tasdiqlandi: {Masked}", masked);
            return CardPayoutCall<CardVerification>.Success(new CardVerification(masked, token, HolderName: null));
        }

        public async Task<CardPayoutCall<CardPayout>> PayoutAsync(
            string cardToken, decimal amountUzs, string orderId, CancellationToken ct = default)
        {
            await DelayAsync(ct);

            if (amountUzs <= 0)
                return CardPayoutCall<CardPayout>.Rejected("INVALID_AMOUNT", "Summa noldan katta bo'lishi kerak.");

            // Idempotentlik: shu orderId bo'yicha o'tkazma allaqachon bo'lgan bo'lsa — o'shani qaytaramiz.
            if (OrderToRef.TryGetValue(orderId, out var existingRef) &&
                Payouts.TryGetValue(existingRef, out var existing))
            {
                _logger.LogInformation(
                    "[FAKE-BANK] Takroriy payout orderId={OrderId} → mavjud ref={Ref}", orderId, existingRef);
                return CardPayoutCall<CardPayout>.Success(existing);
            }

            if (_options.FailureRate > 0 && _random.NextDouble() < _options.FailureRate)
            {
                _logger.LogWarning("[FAKE-BANK] Payout sun'iy tarmoq xatosi orderId={OrderId}", orderId);
                return CardPayoutCall<CardPayout>.Transport(
                    CardPayoutFailureKind.Network, "Bank bilan aloqa uzildi (sun'iy xato).");
            }

            var providerRef = "pay_" + Guid.NewGuid().ToString("N");
            var payout = new CardPayout(providerRef, CardPayoutState.Succeeded);

            Payouts[providerRef] = payout;
            OrderToRef[orderId] = providerRef;

            _logger.LogInformation(
                "[FAKE-BANK] Payout OK orderId={OrderId} ref={Ref} amount={Amount}",
                orderId, providerRef, amountUzs);

            return CardPayoutCall<CardPayout>.Success(payout);
        }

        public async Task<CardPayoutCall<CardPayout>> GetPayoutStatusAsync(
            string providerRef, CancellationToken ct = default)
        {
            await DelayAsync(ct);

            if (Payouts.TryGetValue(providerRef, out var payout))
                return CardPayoutCall<CardPayout>.Success(payout);

            return CardPayoutCall<CardPayout>.Rejected("NOT_FOUND", "O'tkazma topilmadi.");
        }

        private Task DelayAsync(CancellationToken ct)
            => _options.DelayMs > 0 ? Task.Delay(_options.DelayMs, ct) : Task.CompletedTask;
    }
}
