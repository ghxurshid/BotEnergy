using Domain.Dtos;
using Domain.Dtos.Base;

namespace Domain.Interfaces
{
    /// <summary>
    /// UserApi-spetsifik composition servis — app ishga tushgandagi yagona "/Bootstrap"
    /// endpointi uchun. Profil + balans + aktiv sessiya snapshotini bitta javobda yig'adi.
    /// AuthApi/AdminApi da kerak emas.
    /// </summary>
    public interface IBootstrapService
    {
        Task<GenericDto<BootstrapResultDto>> GetAsync(long userId);
    }
}
