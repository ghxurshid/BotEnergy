using Domain.Entities;
using Domain.Guards;

namespace Domain.Payments
{
    /// <summary>
    /// To'lov kontekstini kafolatlash natijasi: kontekst yoki uni yaratishga to'sqinlik qilgan omil.
    /// </summary>
    public sealed record EnsurePaymentSessionResult(PaymentSessionEntity? PaymentSession, StopFactor? Stop)
    {
        public bool IsSuccess => PaymentSession is not null;

        public static EnsurePaymentSessionResult Ok(PaymentSessionEntity ps) => new(ps, null);
        public static EnsurePaymentSessionResult Blocked(StopFactor stop) => new(null, stop);
    }
}
