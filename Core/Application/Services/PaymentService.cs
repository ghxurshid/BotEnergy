using Domain.Dtos;
using Domain.Dtos.Base;
using Domain.Dtos.Payment;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Payme;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// QR orqali tashqi to'lov tizimi (Payme) bilan balansni to'ldirish.
    /// Har bir qadam (validate / receipt-create / pay / credit) PaymentTransactionStep sifatida
    /// audit'ga yoziladi — natijasi qanday bo'lishidan qat'iy nazar.
    /// Provider chaqiruvlari throw qilmaydi (PaymeApiCall wrapper'i orqali);
    /// shuning uchun tashqi try/catch faqat kutilmagan ichki istisnolar uchun.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentTransactionRepository _repo;
        private readonly ICustomerUserRepository _userRepo;
        private readonly IPaymeClient _payme;
        private readonly IBillingService _billing;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IPaymentTransactionRepository repo,
            ICustomerUserRepository userRepo,
            IPaymeClient payme,
            IBillingService billing,
            ILogger<PaymentService> logger)
        {
            _repo = repo;
            _userRepo = userRepo;
            _payme = payme;
            _billing = billing;
            _logger = logger;
        }

        public async Task<GenericDto<QrTopUpResultDto>> ProcessQrTopUpAsync(QrTopUpRequestDto request, CancellationToken ct = default)
        {
            // 1. Sodda input validatsiyasi (permission tekshiruvi controller'da bo'lib o'tadi)
            if (request.Amount <= 0)
                return GenericDto<QrTopUpResultDto>.Error(400, "To'ldirish miqdori 0 dan katta bo'lishi kerak.");

            if (string.IsNullOrWhiteSpace(request.PaymeToken))
                return GenericDto<QrTopUpResultDto>.Error(400, "Payme tokeni bo'sh.");

            // 2. Idempotency tekshiruvi — agar shu kalit bilan tranzaksiya bor bo'lsa, qaytadan ishga tushirmaymiz
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = await _repo.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existing is not null)
                    return await BuildResultFromExistingAsync(existing);
            }

            // 3. Foydalanuvchini olish (Organization avtomatik LegalUser uchun yuklanadi)
            var user = await _userRepo.GetByIdAsync(request.InitiatedByUserId);
            if (user is null)
                return GenericDto<QrTopUpResultDto>.Error(404, "Foydalanuvchi topilmadi.");
            if (user.IsBlocked)
                return GenericDto<QrTopUpResultDto>.Error(403, "Foydalanuvchi bloklangan.");

            // 4. PayeeType bo'yicha balans egasini aniqlash
            long? userPayeeId = null;
            long? organizationPayeeId = null;

            if (request.PayeeType == PaymentPayeeType.User)
            {
                if (user.Type != CustomerUserType.Natural)
                    return GenericDto<QrTopUpResultDto>.Error(400, "Shaxsiy balansga to'lov faqat jismoniy foydalanuvchilar uchun.");
                userPayeeId = user.Id;
            }
            else
            {
                if (user.Type != CustomerUserType.Corporate || user.OrganizationId is null)
                    return GenericDto<QrTopUpResultDto>.Error(400, "Tashkilot to'lovi uchun siz biriktirilgan tashkilotga ega corporate foydalanuvchi bo'lishingiz kerak.");
                organizationPayeeId = user.OrganizationId;
            }

            // 5. PaymentTransaction yarat (Pending)
            var providerOrderId = Guid.NewGuid().ToString("N");
            var tx = new PaymentTransactionEntity
            {
                PayeeType = request.PayeeType,
                UserId = userPayeeId,
                OrganizationId = organizationPayeeId,
                InitiatedByUserId = user.Id,
                Amount = request.Amount,
                Currency = "UZS",
                Status = PaymentStatus.Pending,
                Provider = PaymentProvider.Payme,
                ProviderOrderId = providerOrderId,
                DeviceSerial = request.DeviceSerial,
                SessionId = request.SessionId,
                IdempotencyKey = request.IdempotencyKey
            };
            tx = await _repo.CreateAsync(tx);

            await LogStepAsync(tx.Id, PaymentStepType.Initiated, PaymentStepStatus.Info,
                message: $"PayeeType={request.PayeeType}, Amount={request.Amount} UZS");
            await LogStepAsync(tx.Id, PaymentStepType.Validated, PaymentStepStatus.Success);

            // 6. Payme'ga receipt yaratish
            var amountTiyin = ToTiyin(request.Amount);

            await LogStepAsync(tx.Id, PaymentStepType.ReceiptCreateRequested, PaymentStepStatus.Info,
                message: $"amount={amountTiyin} tiyin, order_id={providerOrderId}");

            var createCall = await _payme.CreateReceiptAsync(amountTiyin, providerOrderId, ct);

            await LogStepAsync(tx.Id, PaymentStepType.ReceiptCreated,
                createCall.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: createCall.RequestBody,
                responsePayload: createCall.ResponseBody,
                message: createCall.FailureMessage);

            if (!createCall.IsSuccess)
            {
                await MarkFailedAsync(tx, $"Receipt yaratish: {createCall.FailureMessage}");
                return GenericDto<QrTopUpResultDto>.Error(502, "Payme bilan bog'lanishda xatolik.");
            }

            tx.ProviderReceiptId = createCall.Result!.Id;
            tx.Status = PaymentStatus.ReceiptCreated;
            await _repo.UpdateAsync(tx);

            // 7. Payme'da to'lovni amalga oshirish
            await LogStepAsync(tx.Id, PaymentStepType.PayRequested, PaymentStepStatus.Info,
                message: $"receipt_id={tx.ProviderReceiptId}");

            tx.Status = PaymentStatus.Paying;
            await _repo.UpdateAsync(tx);

            var payCall = await _payme.PayReceiptAsync(tx.ProviderReceiptId, request.PaymeToken, ct);

            await LogStepAsync(tx.Id, PaymentStepType.PayResponded,
                payCall.IsSuccess ? PaymentStepStatus.Success : PaymentStepStatus.Error,
                requestPayload: payCall.RequestBody,
                responsePayload: payCall.ResponseBody,
                message: payCall.FailureMessage);

            if (!payCall.IsSuccess)
            {
                await MarkFailedAsync(tx, $"To'lov: {payCall.FailureMessage}");
                return GenericDto<QrTopUpResultDto>.Error(402, payCall.Error?.Message ?? "Payme to'lovi rad etildi.");
            }

            tx.ProviderState = payCall.Result!.State;

            // Payme spec: state == 4 muvaffaqiyatli to'lov
            if (payCall.Result.State != 4)
            {
                await MarkFailedAsync(tx, $"Payme state={payCall.Result.State} (4 kutilgan edi)");
                return GenericDto<QrTopUpResultDto>.Error(402, $"To'lov yakunlanmadi. Payme holati: {payCall.Result.State}");
            }

            // 8. Balansni to'ldirish — BillingService.TopUpAsync user tipini avtomatik aniqlaydi
            //    (NaturalUser → o'z balansiga, LegalUser → org balansiga)
            var topUp = await _billing.TopUpAsync(new TopUpBalanceDto
            {
                UserId = user.Id,
                Amount = request.Amount
            });

            if (!topUp.IsSuccess)
            {
                // Critical: Payme allaqachon to'lashdi, lekin ichki balans yangilanmadi.
                // Tx Failed deb belgilaymiz, lekin reconciliation uchun alohida log qoldiramiz.
                _logger.LogCritical(
                    "Payme paid receipt {ReceiptId} but balance update failed for tx {TxId}: {Error}",
                    tx.ProviderReceiptId, tx.Id, topUp.ErrorObj?.ErrorMessage);

                await LogStepAsync(tx.Id, PaymentStepType.Failed, PaymentStepStatus.Error,
                    message: $"Payme paid, balance update failed: {topUp.ErrorObj?.ErrorMessage}");
                await MarkFailedAsync(tx, "Balansni yangilashda ichki xatolik (manual reconciliation kerak).");
                return GenericDto<QrTopUpResultDto>.Error(500, "Balansni yangilashda xatolik. Operatorga murojaat qiling.");
            }

            await LogStepAsync(tx.Id, PaymentStepType.BalanceCredited, PaymentStepStatus.Success,
                message: $"NewBalance={topUp.Result!.NewBalance:N2}");

            tx.Status = PaymentStatus.Succeeded;
            tx.CompletedAt = DateTime.Now;
            await _repo.UpdateAsync(tx);

            return GenericDto<QrTopUpResultDto>.Success(new QrTopUpResultDto
            {
                TransactionId = tx.Id,
                Status = tx.Status,
                NewBalance = topUp.Result.NewBalance,
                ProviderState = tx.ProviderState,
                ResultMessage = $"{request.Amount:N0} UZS muvaffaqiyatli to'ldirildi."
            });
        }

        public async Task<GenericDto<ReverseTransactionResultDto>> ReverseAsync(long transactionId, long performedByUserId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return GenericDto<ReverseTransactionResultDto>.Error(400, "Reverse sababi kiritilishi shart.");

            var tx = await _repo.GetByIdAsync(transactionId);
            if (tx is null)
                return GenericDto<ReverseTransactionResultDto>.Error(404, "To'lov topilmadi.");

            if (tx.Status != PaymentStatus.Succeeded)
                return GenericDto<ReverseTransactionResultDto>.Error(400,
                    $"Faqat Succeeded holatdagi to'lovlarni reverse qilish mumkin (joriy: {tx.Status}).");

            // Balansga reverse — qaysi user/org bo'lishidan qat'iy nazar, InitiatedByUserId orqali
            // BillingService.TopUpAsync(-amount) yo'q, shuning uchun user yoki org balansidan to'g'ridan-to'g'ri ayiramiz
            var user = await _userRepo.GetByIdAsync(tx.InitiatedByUserId);
            if (user is null)
                return GenericDto<ReverseTransactionResultDto>.Error(404, "Tegishli foydalanuvchi topilmadi.");

            decimal newBalance;
            if (tx.PayeeType == PaymentPayeeType.User && user.Type == CustomerUserType.Natural)
            {
                user.Balance -= tx.Amount;
                newBalance = user.Balance;
                await _userRepo.UpdateAsync(user);
            }
            else if (tx.PayeeType == PaymentPayeeType.Organization &&
                     user.Type == CustomerUserType.Corporate &&
                     user.Organization is not null)
            {
                user.Organization.Balance -= tx.Amount;
                newBalance = user.Organization.Balance;
                await _userRepo.UpdateAsync(user);
            }
            else
            {
                return GenericDto<ReverseTransactionResultDto>.Error(500,
                    "Balans egasi turi to'g'ri kelmadi (entity ma'lumotlari buzilgan bo'lishi mumkin).");
            }

            if (newBalance < 0)
            {
                _logger.LogWarning(
                    "Reverse natijasida balans manfiy: tx={TxId} payee={Payee} newBalance={Balance}",
                    tx.Id, tx.PayeeType, newBalance);
            }

            await LogStepAsync(tx.Id, PaymentStepType.Reversed, PaymentStepStatus.Info,
                message: $"Reversed by user {performedByUserId}. Reason: {reason}. NewBalance={newBalance:N2}");

            tx.Status = PaymentStatus.Reversed;
            tx.FailureReason = $"Reversed by admin {performedByUserId}: {reason}";
            tx.CompletedAt = DateTime.Now;
            await _repo.UpdateAsync(tx);

            return GenericDto<ReverseTransactionResultDto>.Success(new ReverseTransactionResultDto
            {
                TransactionId = tx.Id,
                Status = tx.Status,
                NewBalance = newBalance,
                ResultMessage = $"To'lov bekor qilindi. {tx.Amount:N0} UZS balansdan ayirildi."
            });
        }

        private async Task<GenericDto<QrTopUpResultDto>> BuildResultFromExistingAsync(PaymentTransactionEntity existing)
        {
            // Idempotent replay: avvalgi natijani qaytarish
            decimal currentBalance = 0;
            if (existing.Status == PaymentStatus.Succeeded)
                currentBalance = await _billing.GetAvailableBalanceAsync(existing.InitiatedByUserId);

            return GenericDto<QrTopUpResultDto>.Success(new QrTopUpResultDto
            {
                TransactionId = existing.Id,
                Status = existing.Status,
                NewBalance = currentBalance,
                ProviderState = existing.ProviderState,
                ResultMessage = existing.Status == PaymentStatus.Succeeded
                    ? "Avvalgi to'lov natijasi (idempotent qayta yuborish)."
                    : $"Avvalgi to'lov holati: {existing.Status}"
            });
        }

        private async Task MarkFailedAsync(PaymentTransactionEntity tx, string reason)
        {
            tx.Status = PaymentStatus.Failed;
            tx.FailureReason = reason;
            tx.CompletedAt = DateTime.Now;
            await _repo.UpdateAsync(tx);
        }

        private Task LogStepAsync(
            long transactionId,
            PaymentStepType stepType,
            PaymentStepStatus status,
            string? requestPayload = null,
            string? responsePayload = null,
            string? message = null)
        {
            return _repo.AddStepAsync(new PaymentTransactionStepEntity
            {
                PaymentTransactionId = transactionId,
                StepType = stepType,
                Status = status,
                RequestPayload = requestPayload,
                ResponsePayload = responsePayload,
                Message = message,
                OccurredAt = DateTime.Now
            });
        }

        private static long ToTiyin(decimal uzs) => (long)Math.Round(uzs * 100m, MidpointRounding.AwayFromZero);
    }
}
