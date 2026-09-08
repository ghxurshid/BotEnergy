namespace Domain.Interfaces.Payme
{
    /// <summary>
    /// Payme Subscribe API karta tokeni. PAN va CVV HECH QACHON qaytarilmaydi —
    /// faqat maskalangan raqam va token.
    /// </summary>
    public class PaymeCard
    {
        /// <summary>Karta tokeni — to'lov shu bilan qilinadi (sirli qiymat).</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Maskalangan raqam (860600******1234).</summary>
        public string Number { get; set; } = string.Empty;

        /// <summary>Amal muddati (MMYY).</summary>
        public string? Expire { get; set; }

        /// <summary>Token qayta-qayta to'lovga yaroqlimi (save=true bilan yaratilganda true).</summary>
        public bool Recurrent { get; set; }

        /// <summary>SMS kod bilan tasdiqlanganmi. false bo'lsa to'lovga ishlatib bo'lmaydi.</summary>
        public bool Verify { get; set; }
    }

    /// <summary>cards.get_verify_code javobi.</summary>
    public class PaymeVerifyCodeRequest
    {
        /// <summary>Kod yuborildimi.</summary>
        public bool Sent { get; set; }

        /// <summary>Kod yuborilgan telefon (maskalangan).</summary>
        public string? Phone { get; set; }

        /// <summary>Keyingi urinishgacha kutish (ms).</summary>
        public long Wait { get; set; }
    }

    /// <summary>cards.remove javobi.</summary>
    public class PaymeCardRemoval
    {
        public bool Success { get; set; }
    }
}
