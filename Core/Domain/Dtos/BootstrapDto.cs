using Domain.Dtos.Session;

namespace Domain.Dtos
{
    /// <summary>
    /// App ishga tushganda single-call resume: profil + balans + aktiv sessiya snapshoti.
    /// Klient bu javob asosida UI ni qayta tiklaydi va keyin SignalR ga ulanadi.
    /// </summary>
    public class BootstrapResultDto
    {
        public required GetUserDto User { get; set; }
        public CurrentSessionDto? ActiveSession { get; set; }
        public DateTime ServerTime { get; set; }
    }
}
