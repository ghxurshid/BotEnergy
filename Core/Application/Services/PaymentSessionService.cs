using Domain.Dtos.Base;
using Domain.Dtos.PaymentSession;
using Domain.Entities;
using Domain.Helpers;
using Domain.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class PaymentSessionService : IPaymentSessionService
    {
        private readonly IPaymentSessionRepository _paymentSessionRepo;
        private readonly IHoldInvoiceRepository _invoiceRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly ILogger<PaymentSessionService> _logger;

        public PaymentSessionService(
            IPaymentSessionRepository paymentSessionRepo,
            IHoldInvoiceRepository invoiceRepo,
            ISessionRepository sessionRepo,
            ILogger<PaymentSessionService> logger)
        {
            _paymentSessionRepo = paymentSessionRepo;
            _invoiceRepo = invoiceRepo;
            _sessionRepo = sessionRepo;
            _logger = logger;
        }

        public async Task<PaymentSessionEntity> CreateForSessionAsync(long sessionId, long deviceId, long userId, long merchantId)
        {
            // Idempotent: session_id unique — takror connect'da mavjudini qaytaramiz.
            var existing = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (existing is not null)
                return existing;

            var paymentSession = new PaymentSessionEntity
            {
                SessionId = sessionId,
                DeviceId = deviceId,
                UserId = userId,
                MerchantId = merchantId
            };

            paymentSession = await _paymentSessionRepo.CreateAsync(paymentSession);

            _logger.LogInformation(
                "[PAY-SESSION] Yaratildi paymentSessionId={PaymentSessionId} sessionId={SessionId} merchantId={MerchantId}",
                paymentSession.Id, sessionId, merchantId);

            return paymentSession;
        }

        public async Task<GenericDto<PaymentSessionDto>> GetForSessionAsync(long sessionId, long userId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session is null)
                return GenericDto<PaymentSessionDto>.Error(404, "Sessiya topilmadi.");
            if (session.UserId != userId)
                return GenericDto<PaymentSessionDto>.Error(403, "Bu sessiya sizga tegishli emas.");

            var paymentSession = await _paymentSessionRepo.GetBySessionIdAsync(sessionId);
            if (paymentSession is null)
                return GenericDto<PaymentSessionDto>.Error(404, "Sessiyada to'lov konteksti yo'q.");

            var invoices = await _invoiceRepo.GetByPaymentSessionAsync(paymentSession.Id);
            var available = paymentSession.HoldBalanceTiyin - paymentSession.ConsumedTiyin;

            return GenericDto<PaymentSessionDto>.Success(new PaymentSessionDto
            {
                PaymentSessionId = paymentSession.Id,
                SessionId = sessionId,
                Status = paymentSession.Status,
                HoldBalanceTiyin = paymentSession.HoldBalanceTiyin,
                ConsumedTiyin = paymentSession.ConsumedTiyin,
                AvailableTiyin = available,
                AvailableUzs = Money.ToUzs(available),
                Invoices = invoices.Select(MapItem).ToList()
            });
        }

        internal static HoldInvoiceItemDto MapItem(HoldInvoiceEntity i) => new()
        {
            InvoiceId = i.Id,
            SequenceNo = i.SequenceNo,
            Status = i.Status,
            AmountTiyin = i.AmountTiyin,
            ConsumedTiyin = i.ConsumedTiyin,
            ProviderReceiptId = i.ProviderReceiptId,
            CreatedDate = i.CreatedDate,
            HoldAt = i.HoldAt,
            SettledAt = i.SettledAt,
            FailureReason = i.FailureReason
        };
    }
}
