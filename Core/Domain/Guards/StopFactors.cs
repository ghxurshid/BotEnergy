using Domain.Enums;

namespace Domain.Guards
{
    /// <summary>
    /// Loyihadagi BARCHA to'sqinlik omillarining yagona katalogi.
    ///
    /// Nega bitta joyda: bir xil sabab (masalan "qurilma oflayn") loyihaning o'nlab joyida
    /// uchraydi — matn har joyda alohida yozilsa, mijoz ilovasi ularni farqlay olmaydi va
    /// bir xil holat turlicha tushuntiriladi. Bu yerda kod ham, matn ham, HTTP status ham
    /// bitta manbadan keladi.
    ///
    /// Kod nomlash: UPPER_SNAKE, obyekt_sabab tartibida (<c>DEVICE_OFFLINE</c>,
    /// <c>SESSION_HAS_ACTIVE_PROCESS</c>). Kodlar mijoz shartnomasining bir qismi —
    /// o'zgartirilmaydi, faqat yangisi qo'shiladi.
    ///
    /// Status tanlash qoidasi:
    ///  400 — kiritilgan qiymat noto'g'ri;      403 — huquq/doira yo'q;
    ///  404 — obyekt yo'q;                      409 — obyekt holati amalga yo'l bermaydi;
    ///  422 — tashqi tomon rad etdi;            502/503 — tashqi bog'liqlik javob bermayapti.
    /// </summary>
    public static class StopFactors
    {
        // ─────────────────────────── Qurilma ───────────────────────────

        public static class Device
        {
            public static readonly StopFactor NotFound =
                new("DEVICE_NOT_FOUND", "Qurilma topilmadi.", 404);

            public static readonly StopFactor Inactive =
                new("DEVICE_INACTIVE", "Qurilma faol emas — amalni bajarish uchun avval uni faollashtiring.", 409);

            /// <summary>
            /// Eng ko'p uchraydigan to'siq: buyruq brokerga ketadi, lekin qurilma uni olmaydi.
            /// Shuning uchun buyruq YUBORILMAYDI — foydalanuvchi "yuborildi" degan yolg'on javob olmasin.
            /// </summary>
            public static StopFactor Offline(string serial, DateTime? lastSeenAt) => new(
                "DEVICE_OFFLINE",
                lastSeenAt is null
                    ? $"Qurilma {serial} oflayn — buyruqni bajarib bo'lmaydi."
                    : $"Qurilma {serial} oflayn (oxirgi aloqa: {lastSeenAt:dd.MM.yyyy HH:mm}) — buyruqni bajarib bo'lmaydi.",
                503);

            public static readonly StopFactor NotAttachedToSession =
                new("DEVICE_NOT_ATTACHED", "Sessiyaga qurilma ulanmagan.", 409);

            public static readonly StopFactor NoStation =
                new("DEVICE_NO_STATION", "Qurilma stansiyaga biriktirilmagan.", 409);

            /// <summary>Qurilmadan kelgan hisobot boshqa qurilmaning jarayoniga tegishli.</summary>
            public static readonly StopFactor NotBoundToProcess =
                new("DEVICE_NOT_BOUND_TO_PROCESS", "Qurilma bu jarayonga tegishli emas.", 403);

            public static readonly StopFactor LockedByOtherUser =
                new("DEVICE_LOCKED", "Qurilma boshqa foydalanuvchi tomonidan band qilingan.", 409);

            public static readonly StopFactor HasActiveSession =
                new("DEVICE_HAS_ACTIVE_SESSION",
                    "Qurilmada faol sessiya bor — avval u yakunlanishi kerak.", 409);

            public static readonly StopFactor HasOpenCashSession =
                new("DEVICE_HAS_OPEN_CASH_SESSION",
                    "Qurilmada tugallanmagan naqd sessiya bor — avval uni yakunlang.", 409);

            public static StopFactor HasCash(decimal balance) => new(
                "DEVICE_HAS_CASH",
                $"Qurilma boxida {balance:N0} UZS naqd pul bor — avval inkassatsiya qiling.",
                409);

            public static readonly StopFactor HasOpenCollection =
                new("DEVICE_HAS_OPEN_COLLECTION",
                    "Bu qurilmada tugallanmagan inkassatsiya bor. Avval uni yakunlang yoki bekor qiling.", 409);

            public static StopFactor SerialTaken(string serial) => new(
                "DEVICE_SERIAL_TAKEN", $"'{serial}' seriya raqamli qurilma allaqachon mavjud.", 409);

            public static readonly StopFactor OutOfScope =
                new("DEVICE_OUT_OF_SCOPE", "Bu qurilma sizning doirangizga tegishli emas.", 403);
        }

        // ─────────────────────────── Stansiya ───────────────────────────

        public static class Station
        {
            public static readonly StopFactor NotFound =
                new("STATION_NOT_FOUND", "Stansiya topilmadi.", 404);

            public static readonly StopFactor Inactive =
                new("STATION_INACTIVE", "Stansiya faol emas — unda amal bajarilmaydi.", 409);

            public static StopFactor HasDevices(int count) => new(
                "STATION_HAS_DEVICES",
                $"Stansiyaga {count} ta qurilma biriktirilgan — avval ularni ko'chiring yoki o'chiring.",
                409);

            public static readonly StopFactor HasActiveSession =
                new("STATION_HAS_ACTIVE_SESSION",
                    "Stansiya qurilmalarida faol sessiya bor — avval ular yakunlanishi kerak.", 409);

            public static readonly StopFactor OutOfScope =
                new("STATION_OUT_OF_SCOPE", "Bu stansiya sizning doirangizga tegishli emas.", 403);
        }

        // ─────────────────────────── Merchant ───────────────────────────

        public static class Merchant
        {
            public static readonly StopFactor NotFound =
                new("MERCHANT_NOT_FOUND", "Merchant topilmadi.", 404);

            public static readonly StopFactor Inactive =
                new("MERCHANT_INACTIVE", "Merchant faol emas — unga tegishli amallar bajarilmaydi.", 409);

            public static StopFactor HasStations(int count) => new(
                "MERCHANT_HAS_STATIONS",
                $"Merchantda {count} ta stansiya bor — avval ularni o'chiring.", 409);

            public static StopFactor HasUsers(int count) => new(
                "MERCHANT_HAS_USERS",
                $"Merchantga {count} ta operator biriktirilgan — avval ularni o'chiring.", 409);

            public static readonly StopFactor HasActiveSession =
                new("MERCHANT_HAS_ACTIVE_SESSION",
                    "Merchant qurilmalarida faol sessiya bor — avval ular yakunlanishi kerak.", 409);

            public static readonly StopFactor PaymeNotConfigured =
                new("MERCHANT_PAYME_NOT_CONFIGURED",
                    "Merchant uchun Payme sozlanmagan — administratorga murojaat qiling.", 409);

            public static readonly StopFactor OutOfScope =
                new("MERCHANT_OUT_OF_SCOPE", "Bu merchant sizning doirangizga tegishli emas.", 403);

            public static readonly StopFactor PhoneTaken =
                new("MERCHANT_PHONE_TAKEN", "Bu telefon raqam bilan merchant allaqachon mavjud.", 409);

            public static readonly StopFactor InnTaken =
                new("MERCHANT_INN_TAKEN", "Bu INN bilan merchant allaqachon mavjud.", 409);
        }

        // ─────────────────────────── Tashkilot ───────────────────────────

        public static class Organization
        {
            public static readonly StopFactor NotFound =
                new("ORGANIZATION_NOT_FOUND", "Tashkilot topilmadi.", 404);

            public static readonly StopFactor Inactive =
                new("ORGANIZATION_INACTIVE", "Tashkilot faol emas — unga tegishli amallar bajarilmaydi.", 409);

            public static StopFactor HasUsers(int count) => new(
                "ORGANIZATION_HAS_USERS",
                $"Tashkilotda {count} ta foydalanuvchi bor — avval ularni o'chiring.", 409);

            public static StopFactor HasBalance(decimal balance) => new(
                "ORGANIZATION_HAS_BALANCE",
                $"Tashkilot balansida {balance:N0} UZS bor — avval hisob-kitob qiling.", 409);

            public static readonly StopFactor OutOfScope =
                new("ORGANIZATION_OUT_OF_SCOPE", "Bu tashkilot sizning doirangizga tegishli emas.", 403);

            public static readonly StopFactor NotLinked =
                new("ORGANIZATION_NOT_LINKED", "Corporate foydalanuvchining tashkiloti biriktirilmagan.", 409);
        }

        // ─────────────────────────── Mahsulot ───────────────────────────

        public static class Product
        {
            public static readonly StopFactor NotFound =
                new("PRODUCT_NOT_FOUND", "Mahsulot topilmadi.", 404);

            public static readonly StopFactor Inactive =
                new("PRODUCT_INACTIVE", "Mahsulot faol emas.", 409);

            public static readonly StopFactor DeviceMismatch =
                new("PRODUCT_DEVICE_MISMATCH", "Mahsulot ushbu qurilmaga tegishli emas.", 409);

            public static StopFactor TypeNotAllowed(DeviceType deviceType, ProductType productType, string allowed) => new(
                "PRODUCT_TYPE_NOT_ALLOWED",
                $"{deviceType} uchun '{productType}' tanlash mumkin emas. Ruxsat etilgan: {allowed}.",
                400);

            public static readonly StopFactor InUse =
                new("PRODUCT_IN_USE",
                    "Mahsulot hozir tugallanmagan jarayonda ishlatilmoqda — avval jarayon yakunlansin.", 409);

            public static readonly StopFactor OutOfScope =
                new("PRODUCT_OUT_OF_SCOPE", "Bu mahsulot sizning doirangizga tegishli emas.", 403);
        }

        // ─────────────────────────── Sessiya ───────────────────────────

        public static class Session
        {
            public static readonly StopFactor NotFound =
                new("SESSION_NOT_FOUND", "Sessiya topilmadi.", 404);

            public static readonly StopFactor NotOwned =
                new("SESSION_NOT_OWNED", "Bu sessiya sizga tegishli emas.", 403);

            /// <summary>Qurilma yuborgan sessiya tokeni serverdagi bilan mos kelmadi.</summary>
            public static readonly StopFactor TokenMismatch =
                new("SESSION_TOKEN_MISMATCH", "Sessiya tokeni mos kelmadi.", 403);

            public static readonly StopFactor Closed =
                new("SESSION_CLOSED", "Sessiya allaqachon yopilgan.", 409);

            public static readonly StopFactor Settling =
                new("SESSION_SETTLING", "Sessiya hisob-kitob qilinmoqda — biroz kuting.", 409);

            public static readonly StopFactor Paused =
                new("SESSION_PAUSED", "Sessiya pauzada (qurilma bilan aloqa yo'q) — amalni bajarib bo'lmaydi.", 409);

            public static readonly StopFactor NotConnected =
                new("SESSION_NOT_CONNECTED", "Sessiya qurilmaga ulanmagan yoki yopilgan.", 409);

            public static readonly StopFactor AlreadyActive =
                new("SESSION_ALREADY_ACTIVE", "Sizda allaqachon faol sessiya bor. Avval uni yoping.", 409);

            public static readonly StopFactor HasActiveProcess =
                new("SESSION_HAS_ACTIVE_PROCESS", "Avval jarayonni to'xtating, keyin sessiyani yoping.", 409);

            public static readonly StopFactor NoPaymentContext =
                new("SESSION_NO_PAYMENT_CONTEXT", "Sessiyada to'lov konteksti yo'q.", 404);

            public static readonly StopFactor PaymentContextClosed =
                new("SESSION_PAYMENT_CONTEXT_CLOSED",
                    "To'lov konteksti aktiv emas (hisob-kitob boshlangan).", 409);
        }

        // ─────────────────────────── Jarayon ───────────────────────────

        public static class Process
        {
            public static readonly StopFactor NotFound =
                new("PROCESS_NOT_FOUND", "Jarayon topilmadi.", 404);

            public static readonly StopFactor NotOwned =
                new("PROCESS_NOT_OWNED", "Bu jarayon sizga tegishli emas.", 403);

            public static readonly StopFactor AlreadyEnded =
                new("PROCESS_ALREADY_ENDED", "Jarayon allaqachon yakunlangan.", 409);

            public static readonly StopFactor NotPaused =
                new("PROCESS_NOT_PAUSED", "Jarayon pauzada emas.", 409);

            public static readonly StopFactor NotPausable =
                new("PROCESS_NOT_PAUSABLE", "Jarayon pauza qilish uchun mos holatda emas.", 409);

            public static readonly StopFactor AlreadyActive =
                new("PROCESS_ALREADY_ACTIVE", "Sessiyada hali tugamagan jarayon mavjud.", 409);

            /// <summary>"No hold = no fuel" — tasdiqlangan hold bo'lmasa jarayon boshlanmaydi.</summary>
            public static readonly StopFactor NoFunding =
                new("PROCESS_NO_FUNDING",
                    "Tasdiqlangan Hold mavjud emas — avval to'lovni bloklang (hold invoice yarating).", 409);

            public static readonly StopFactor FundingTooSmall =
                new("PROCESS_FUNDING_TOO_SMALL", "Hold balansi yetarli emas — yangi invoice yarating.", 409);

            /// <summary>
            /// Jarayonni boshqarish buyrug'i (stop/pause/resume) oflayn qurilmaga yetib bormaydi.
            /// Kodi <c>DEVICE_OFFLINE</c> — mijoz uni boshqa oflayn holatlar bilan bir xil ishlaydi;
            /// matni esa aynan qaysi buyruq to'silganini va keyin nima bo'lishini aytadi.
            /// </summary>
            public static StopFactor DeviceOffline(string serial, string command, string? note = null) => new(
                "DEVICE_OFFLINE",
                $"Qurilma {serial} oflayn — {command} buyrug'i yetib bormaydi."
                    + (string.IsNullOrEmpty(note) ? string.Empty : " " + note),
                503);
        }

        // ─────────────────────────── Naqd (bill acceptor) ───────────────────────────

        public static class Cash
        {
            public static readonly StopFactor SessionNotFound =
                new("CASH_SESSION_NOT_FOUND", "Naqd sessiya topilmadi.", 404);

            public static readonly StopFactor NotAccepting =
                new("CASH_SESSION_NOT_ACCEPTING", "Sessiya naqd qabul qilish holatida emas.", 409);

            public static readonly StopFactor Finished =
                new("CASH_SESSION_FINISHED", "Sessiya yakunlangan.", 409);

            public static readonly StopFactor Empty =
                new("CASH_SESSION_EMPTY", "Hech qanday pul qabul qilinmagan.", 400);

            /// <summary>Bill acceptor pulni qaytara olmaydi — pul solingan bo'lsa bekor qilish yo'q.</summary>
            public static readonly StopFactor HasMoney =
                new("CASH_SESSION_HAS_MONEY",
                    "Pul qabul qilingan — sessiyani bekor qilib bo'lmaydi, kartaga o'tkazing.", 409);

            public static readonly StopFactor DenominationRejected =
                new("CASH_DENOMINATION_REJECTED", "Kupyura nominali qabul qilinmaydi.", 400);

            public static readonly StopFactor LimitExceeded =
                new("CASH_LIMIT_EXCEEDED", "Sessiya bo'yicha maksimal summa oshib ketdi.", 409);

            public static readonly StopFactor InvalidBillSequence =
                new("CASH_INVALID_BILL_SEQ", "bill_seq noldan katta bo'lishi kerak.", 400);

            public static StopFactor CardInvalid(string message) =>
                new("CASH_CARD_INVALID", message, 400);

            public static StopFactor CardRejected(string message) =>
                new("CASH_CARD_REJECTED", message, 422);

            public static readonly StopFactor BankUnavailable =
                new("BANK_UNAVAILABLE", "Bank bilan aloqa yo'q. Birozdan keyin urinib ko'ring.", 503);

            public static readonly StopFactor RetryNotNeeded =
                new("CASH_RETRY_NOT_NEEDED", "Sessiya qayta urinishga muhtoj emas.", 409);

            /// <summary>Box ochiq turganda kupyura qabul qilish hisobni buzadi.</summary>
            public static readonly StopFactor BoxOpen =
                new("CASH_BOX_OPEN", "Qurilma boxi inkassatsiya uchun ochilgan — naqd qabul qilinmaydi.", 409);
        }

        // ─────────────────────────── Inkassatsiya ───────────────────────────

        public static class Incassation
        {
            public static readonly StopFactor NotManage =
                new("INCASSATION_MANAGE_ONLY",
                    "Inkassatsiya faqat platforma xodimlari uchun — merchant tomoni o'z qurilmasidan pul yig'a olmaydi.",
                    403);

            public static readonly StopFactor NotFound =
                new("COLLECTION_NOT_FOUND", "Inkassatsiya topilmadi.", 404);

            public static readonly StopFactor AlreadyFinished =
                new("COLLECTION_FINISHED", "Inkassatsiya allaqachon yakunlangan.", 409);

            public static readonly StopFactor NegativeAmount =
                new("COLLECTION_NEGATIVE_AMOUNT", "Sanalgan summa manfiy bo'lishi mumkin emas.", 400);

            public static readonly StopFactor CommandUndelivered =
                new("COLLECTION_COMMAND_UNDELIVERED",
                    "Qurilmaga buyruq yuborilmadi. Qurilma onlayn ekanini tekshiring.", 503);
        }

        // ─────────────────────────── Foydalanuvchi ───────────────────────────

        public static class User
        {
            public static readonly StopFactor NotFound =
                new("USER_NOT_FOUND", "Foydalanuvchi topilmadi.", 404);

            /// <summary>Login, sessiya ochish, balans to'ldirish — hammasi shu bitta sabab bilan to'siladi.</summary>
            public static readonly StopFactor Blocked =
                new("USER_BLOCKED", "Akkaunt bloklangan — amal bajarilmaydi.", 403);

            public static readonly StopFactor NotVerified =
                new("USER_NOT_VERIFIED", "Akkaunt tasdiqlanmagan — amal bajarilmaydi.", 403);

            public static readonly StopFactor Deleted =
                new("USER_DELETED", "Akkaunt o'chirilgan.", 403);

            public static readonly StopFactor RegistrationIncomplete =
                new("USER_REGISTRATION_INCOMPLETE", "Foydalanuvchi hali ro'yxatdan to'liq o'tmagan.", 409);

            public static readonly StopFactor HasActiveSession =
                new("USER_HAS_ACTIVE_SESSION",
                    "Foydalanuvchining faol sessiyasi bor — avval u yakunlanishi kerak.", 409);

            public static readonly StopFactor SelfAction =
                new("USER_SELF_ACTION", "Bu amalni o'zingizga nisbatan bajara olmaysiz.", 409);

            public static readonly StopFactor LastManage =
                new("USER_LAST_MANAGE",
                    "Bu tizimdagi oxirgi Manage administrator — uni o'chirib yoki bloklab bo'lmaydi.", 409);

            public static readonly StopFactor PasswordAlreadySet =
                new("USER_PASSWORD_ALREADY_SET", "Parol allaqachon o'rnatilgan.", 409);

            public static readonly StopFactor OutOfScope =
                new("USER_OUT_OF_SCOPE", "Bu foydalanuvchi sizning doirangizga tegishli emas.", 403);

            public static StopFactor PhoneTaken(string what) => new(
                "USER_PHONE_TAKEN", $"Bu telefon raqam bilan {what} allaqachon mavjud.", 409);

            public static StopFactor MailTaken(string what) => new(
                "USER_MAIL_TAKEN", $"Bu elektron pochta bilan {what} allaqachon mavjud.", 409);

            public static readonly StopFactor AlreadyBlocked =
                new("USER_ALREADY_BLOCKED", "Foydalanuvchi allaqachon bloklangan.", 409);

            public static readonly StopFactor NotBlocked =
                new("USER_NOT_BLOCKED", "Foydalanuvchi bloklanmagan.", 409);
        }

        // ─────────────────────────── Rol ───────────────────────────

        public static class Role
        {
            public static readonly StopFactor NotFound =
                new("ROLE_NOT_FOUND", "Rol topilmadi.", 404);

            public static readonly StopFactor OutOfScope =
                new("ROLE_OUT_OF_SCOPE", "Bu rol sizning doirangizga tegishli emas.", 403);

            public static StopFactor InUse(int userCount) => new(
                "ROLE_IN_USE",
                $"Rol {userCount} ta foydalanuvchiga biriktirilgan — avval ularga boshqa rol bering.",
                409);

            public static StopFactor PermissionNotAllowed(string permission, string roleKind) => new(
                "ROLE_PERMISSION_NOT_ALLOWED",
                $"'{permission}' permissioni '{roleKind}' rolga biriktirilmaydi.", 400);

            public static readonly StopFactor MerchantMismatch =
                new("ROLE_MERCHANT_MISMATCH", "Tanlangan rol ushbu merchantga tegishli bo'lishi kerak.", 409);

            public static readonly StopFactor OrganizationMismatch =
                new("ROLE_ORGANIZATION_MISMATCH", "Tanlangan rol ushbu tashkilotga tegishli bo'lishi kerak.", 409);
        }

        // ─────────────────────────── To'lov / Hold ───────────────────────────

        public static class Payment
        {
            public static readonly StopFactor AmountNotPositive =
                new("PAYMENT_AMOUNT_INVALID", "Summa 0 dan katta bo'lishi kerak.", 400);

            public static readonly StopFactor ProviderUnavailable =
                new("PAYME_UNAVAILABLE", "Payme bilan bog'lanishda xatolik.", 502);

            public static readonly StopFactor NotFound =
                new("PAYMENT_NOT_FOUND", "To'lov topilmadi.", 404);

            public static StopFactor InvoiceLimit(int max) => new(
                "HOLD_INVOICE_LIMIT",
                $"Bir sessiyada ko'pi bilan {max} ta aktiv invoice bo'lishi mumkin.", 409);

            public static readonly StopFactor InvoiceNotFound =
                new("HOLD_INVOICE_NOT_FOUND", "Invoice topilmadi.", 404);

            public static readonly StopFactor InvoiceNotOwned =
                new("HOLD_INVOICE_NOT_OWNED", "Bu invoice sizga tegishli emas.", 403);

            public static readonly StopFactor InvoiceStateChanged =
                new("HOLD_INVOICE_STATE_CHANGED", "Invoice holati o'zgargan — qayta urinib ko'ring.", 409);

            public static readonly StopFactor PaymentContextNotFound =
                new("PAYMENT_CONTEXT_NOT_FOUND", "To'lov konteksti topilmadi.", 404);

            /// <summary>Mablag'ning bir qismi allaqachon xizmatga ketgan — bekor qilish uni qaytarmaydi.</summary>
            public static readonly StopFactor InvoicePartiallyConsumed =
                new("HOLD_INVOICE_PARTIALLY_CONSUMED",
                    "Invoice mablag'i qisman ishlatilgan — bekor qilib bo'lmaydi, sessiyani yakunlang.", 409);

            /// <summary>Holat mashinasi bu o'tishga ruxsat bermaydi (masalan Hold'dan to'g'ridan Cancelled).</summary>
            public static StopFactor InvoiceTransitionNotAllowed(object currentStatus, string target, string? hint = null) => new(
                "HOLD_INVOICE_TRANSITION_NOT_ALLOWED",
                $"Joriy holat ({currentStatus}) \"{target}\" amaliga ruxsat bermaydi."
                    + (string.IsNullOrEmpty(hint) ? string.Empty : " " + hint),
                409);

            public static readonly StopFactor InvoiceRetryNotApplicable =
                new("HOLD_INVOICE_RETRY_NOT_APPLICABLE", "Faqat Failed holatdagi invoice qayta urinishga yaroqli.", 409);
        }

        // ─────────────────────────── Umumiy ───────────────────────────

        public static class Access
        {
            public static readonly StopFactor Denied =
                new("ACCESS_DENIED", "Bu amalni bajarish huquqingiz yo'q.", 403);

            public static readonly StopFactor ManageOnly =
                new("ACCESS_MANAGE_ONLY", "Bu amal faqat platforma administratori uchun.", 403);
        }
    }
}
