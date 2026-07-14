namespace Domain.Exceptions
{
    /// <summary>
    /// Telefon raqam canonical formatga (998XXXXXXXXX) to'g'ri kelmaganda DB yozuv choke-point'ida
    /// otiladi. Odatda API validatsiya filtrlar buni oldindan 400 bilan ushlaydi; bu — oxirgi himoya
    /// (seed / ichki chaqiruv / filtri yo'q endpoint uchun).
    /// </summary>
    public sealed class InvalidPhoneNumberException : Exception
    {
        public InvalidPhoneNumberException(string message) : base(message) { }
    }
}
