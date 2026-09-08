using System.Text.RegularExpressions;
using Domain.Dtos.Base;
using Domain.Dtos.Payment;
using Domain.Entities;
using Domain.Enums;
using Domain.Guards;
using Domain.Interfaces;
using Domain.Interfaces.Payme;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Payments
{
    /// <summary>
    /// Saqlangan kartalar (Payme Subscribe API). PAN faqat provider'ga uzatiladi —
    /// hech qayerda saqlanmaydi va logga tushmaydi; bizda faqat token + maskalangan raqam qoladi.
    /// </summary>
    public partial class CustomerCardService : ICustomerCardService
    {
        private readonly ICustomerCardRepository _cards;
        private readonly IPaymeClient _payme;
        private readonly IPaymeCredentialResolver _credResolver;
        private readonly ILogger<CustomerCardService> _logger;

        public CustomerCardService(
            ICustomerCardRepository cards,
            IPaymeClient payme,
            IPaymeCredentialResolver credResolver,
            ILogger<CustomerCardService> logger)
        {
            _cards = cards;
            _payme = payme;
            _credResolver = credResolver;
            _logger = logger;
        }

        [GeneratedRegex(@"^\d{16}$")]
        private static partial Regex CardNumberPattern();

        [GeneratedRegex(@"^\d{4}$")]
        private static partial Regex ExpirePattern();

        public async Task<GenericDto<AddCardResultDto>> AddAsync(AddCardDto dto, CancellationToken ct = default)
        {
            var number = (dto.Number ?? string.Empty).Replace(" ", string.Empty).Trim();
            var expire = (dto.Expire ?? string.Empty).Replace("/", string.Empty).Trim();

            PaymeCredentials? creds = null;

            var stop = await StopFactorCheck.For(StopActions.CardAdd)
                .StopIf(!CardNumberPattern().IsMatch(number) || !ExpirePattern().IsMatch(expire),
                        StopFactors.Payment.CardDataInvalid)
                .StopIfAsync(async () =>
                {
                    creds = await _credResolver.ForMerchantAsync(dto.MerchantId);
                    return creds is null;
                }, StopFactors.Merchant.PaymeNotConfigured)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<AddCardResultDto>.Blocked(stop);

            var create = await _payme.CreateCardAsync(number, expire, creds, ct);
            if (!create.IsSuccess)
            {
                _logger.LogWarning("[CARD] cards.create rad etildi userId={UserId} merchantId={MerchantId}: {Msg}",
                    dto.UserId, dto.MerchantId, create.FailureMessage);
                return GenericDto<AddCardResultDto>.Blocked(RejectionOf(create));
            }

            var providerCard = create.Result!;

            // Payme bir karta uchun bir xil token qaytaradi — takroriy qo'shishda yangi yozuv ochmaymiz.
            var card = await _cards.GetByTokenAsync(dto.MerchantId, providerCard.Token);
            if (card is null)
            {
                card = await _cards.CreateAsync(new CustomerCardEntity
                {
                    UserId = dto.UserId,
                    MerchantId = dto.MerchantId,
                    Provider = PaymentProvider.Payme,
                    Token = providerCard.Token,
                    MaskedNumber = providerCard.Number,
                    Expire = providerCard.Expire ?? expire,
                    IsVerified = providerCard.Verify
                });
            }
            else if (card.UserId != dto.UserId)
            {
                // Bir karta ikki hisobda: token allaqachon boshqa foydalanuvchida.
                return GenericDto<AddCardResultDto>.Blocked(StopFactors.Payment.CardNotOwned);
            }
            else if (card.IsVerified)
            {
                // Allaqachon tasdiqlangan kartani qayta qo'shish — hech narsani buzmaymiz.
                // (Provider javobidagi verify=false ni ko'r-ko'rona yozsak, ishlayotgan asosiy
                // kartaning tasdig'i bekor bo'lib, mijoz to'lay olmay qolardi.)
                return GenericDto<AddCardResultDto>.Success(new AddCardResultDto
                {
                    Card = MapItem(card),
                    VerificationSent = false,
                    ResultMessage = "Bu karta allaqachon qo'shilgan va to'lovga tayyor."
                });
            }
            else
            {
                card.MaskedNumber = providerCard.Number;
                card.Expire = providerCard.Expire ?? expire;
                card.IsVerified = providerCard.Verify;
                await _cards.UpdateAsync(card);
            }

            if (card.IsVerified)
                return GenericDto<AddCardResultDto>.Success(new AddCardResultDto
                {
                    Card = MapItem(card),
                    VerificationSent = false,
                    ResultMessage = "Karta qo'shildi va to'lovga tayyor."
                });

            var codeResult = await RequestCodeAsync(card, creds!, ct);
            return GenericDto<AddCardResultDto>.Success(new AddCardResultDto
            {
                Card = MapItem(card),
                VerificationSent = codeResult.Sent,
                VerificationPhone = codeResult.Phone,
                ResultMessage = codeResult.Sent
                    ? "Karta qo'shildi — telefoningizga kelgan kod bilan tasdiqlang."
                    : "Karta qo'shildi, lekin kod yuborilmadi — qayta urinib ko'ring."
            });
        }

        public async Task<GenericDto<CardItemDto>> VerifyAsync(VerifyCardDto dto, CancellationToken ct = default)
        {
            var card = await _cards.GetByIdAsync(dto.CardId);
            PaymeCredentials? creds = null;

            var stop = await StopFactorCheck.For(StopActions.CardVerify)
                .StopIf(string.IsNullOrWhiteSpace(dto.Code), StopFactors.Payment.VerificationCodeEmpty)
                .StopIf(card is null, StopFactors.Payment.CardNotFound)
                .StopIf(() => card!.UserId != dto.UserId, StopFactors.Payment.CardNotOwned)
                .StopIf(() => card!.IsVerified, StopFactors.Payment.CardAlreadyVerified)
                .StopIfAsync(async () =>
                {
                    creds = await _credResolver.ForMerchantAsync(card!.MerchantId);
                    return creds is null;
                }, StopFactors.Merchant.PaymeNotConfigured)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<CardItemDto>.Blocked(stop);

            var verify = await _payme.VerifyCardAsync(card!.Token, dto.Code.Trim(), creds, ct);
            if (!verify.IsSuccess)
                return GenericDto<CardItemDto>.Blocked(RejectionOf(verify));

            card.IsVerified = verify.Result!.Verify;
            card.VerifiedAt = card.IsVerified ? DateTime.Now : null;

            // Merchantdagi birinchi tasdiqlangan karta avtomatik asosiy bo'ladi.
            if (card.IsVerified)
            {
                var existing = await _cards.GetUsableAsync(dto.UserId, card.MerchantId);
                card.IsDefault = existing is null || existing.Id == card.Id;
            }

            await _cards.UpdateAsync(card);

            if (!card.IsVerified)
                return GenericDto<CardItemDto>.Blocked(StopFactors.Payment.CardNotVerified);

            _logger.LogInformation("[CARD] Tasdiqlandi cardId={CardId} userId={UserId} merchantId={MerchantId}",
                card.Id, card.UserId, card.MerchantId);

            return GenericDto<CardItemDto>.Success(MapItem(card));
        }

        public async Task<GenericDto<AddCardResultDto>> ResendCodeAsync(long cardId, long userId, CancellationToken ct = default)
        {
            var card = await _cards.GetByIdAsync(cardId);
            PaymeCredentials? creds = null;

            var stop = await StopFactorCheck.For(StopActions.CardResendCode)
                .StopIf(card is null, StopFactors.Payment.CardNotFound)
                .StopIf(() => card!.UserId != userId, StopFactors.Payment.CardNotOwned)
                .StopIf(() => card!.IsVerified, StopFactors.Payment.CardAlreadyVerified)
                .StopIfAsync(async () =>
                {
                    creds = await _credResolver.ForMerchantAsync(card!.MerchantId);
                    return creds is null;
                }, StopFactors.Merchant.PaymeNotConfigured)
                .ResultAsync();

            if (stop is not null)
                return GenericDto<AddCardResultDto>.Blocked(stop);

            var codeResult = await RequestCodeAsync(card!, creds!, ct);
            return GenericDto<AddCardResultDto>.Success(new AddCardResultDto
            {
                Card = MapItem(card!),
                VerificationSent = codeResult.Sent,
                VerificationPhone = codeResult.Phone,
                ResultMessage = codeResult.Sent ? "Kod qayta yuborildi." : "Kod yuborilmadi — birozdan so'ng urinib ko'ring."
            });
        }

        public async Task<GenericDto<List<CardItemDto>>> GetMyAsync(long userId, long? merchantId = null)
        {
            var cards = await _cards.GetForUserAsync(userId, merchantId);
            return GenericDto<List<CardItemDto>>.Success(cards.Select(MapItem).ToList());
        }

        public async Task<GenericDto<CardResultDto>> SetDefaultAsync(long cardId, long userId)
        {
            var card = await _cards.GetByIdAsync(cardId);

            var stop = StopFactorCheck.For(StopActions.CardSetDefault)
                .StopIf(card is null, StopFactors.Payment.CardNotFound)
                .StopIf(() => card!.UserId != userId, StopFactors.Payment.CardNotOwned)
                .StopIf(() => !card!.IsVerified, StopFactors.Payment.CardNotVerified)
                .Result();

            if (stop is not null)
                return GenericDto<CardResultDto>.Blocked(stop);

            if (!await _cards.SetDefaultAsync(userId, card!.MerchantId, cardId))
                return GenericDto<CardResultDto>.Blocked(StopFactors.Payment.CardNotFound);

            return GenericDto<CardResultDto>.Success(new CardResultDto
            {
                CardId = cardId,
                ResultMessage = "Asosiy karta o'zgartirildi."
            });
        }

        public async Task<GenericDto<CardResultDto>> DeleteAsync(long cardId, long userId, CancellationToken ct = default)
        {
            var card = await _cards.GetByIdAsync(cardId);

            var stop = StopFactorCheck.For(StopActions.CardDelete)
                .StopIf(card is null, StopFactors.Payment.CardNotFound)
                .StopIf(() => card!.UserId != userId, StopFactors.Payment.CardNotOwned)
                .Result();

            if (stop is not null)
                return GenericDto<CardResultDto>.Blocked(stop);

            // Provider tomonda tokenni bekor qilamiz. Xato bo'lsa ham lokal yozuvni o'chiramiz —
            // aks holda foydalanuvchi kartani ro'yxatdan olib tashlay olmay qolardi.
            var creds = await _credResolver.ForMerchantAsync(card!.MerchantId);
            if (creds is not null)
            {
                var removal = await _payme.RemoveCardAsync(card.Token, creds, ct);
                if (!removal.IsSuccess)
                    _logger.LogWarning("[CARD] cards.remove muvaffaqiyatsiz cardId={CardId}: {Msg}",
                        cardId, removal.FailureMessage);
            }

            await _cards.DeleteAsync(cardId);

            return GenericDto<CardResultDto>.Success(new CardResultDto
            {
                CardId = cardId,
                ResultMessage = "Karta o'chirildi."
            });
        }

        // ── Yordamchi ───────────────────────────────────────────────

        private async Task<PaymeVerifyCodeRequest> RequestCodeAsync(
            CustomerCardEntity card, PaymeCredentials creds, CancellationToken ct)
        {
            var call = await _payme.GetCardVerifyCodeAsync(card.Token, creds, ct);
            if (call.IsSuccess)
                return call.Result!;

            _logger.LogWarning("[CARD] cards.get_verify_code muvaffaqiyatsiz cardId={CardId}: {Msg}",
                card.Id, call.FailureMessage);
            return new PaymeVerifyCodeRequest { Sent = false };
        }

        /// <summary>
        /// Provider xatosini mijozga tushunarli to'siqqa aylantiradi: tarmoq/timeout — 502,
        /// providerning o'z rad javobi — 402 (matni bilan).
        /// </summary>
        private static StopFactor RejectionOf<T>(PaymeApiCall<T> call) where T : class
            => PaymeErrorClassifier.Classify(call) == PaymeCallOutcome.Transient
                ? StopFactors.Payment.ProviderUnavailable
                : StopFactors.Payment.ProviderRejected(call.Error?.Message ?? call.FailureMessage);

        private static CardItemDto MapItem(CustomerCardEntity c) => new()
        {
            CardId = c.Id,
            MerchantId = c.MerchantId,
            MaskedNumber = c.MaskedNumber,
            Expire = c.Expire,
            IsVerified = c.IsVerified,
            IsDefault = c.IsDefault,
            CreatedDate = c.CreatedDate,
            LastUsedAt = c.LastUsedAt
        };
    }
}
