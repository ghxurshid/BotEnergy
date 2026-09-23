# Telegram bot — tasdiqlash kodlarini yetkazish

**Bot:** [@BOTENERGY_bot](https://t.me/BOTENERGY_bot) (id `8920755197`)

Shu paytgacha OTP kodlari **hech qayerga yuborilmasdi** — faqat server logiga yozilardi
(`RedisOtpService.cs` dagi "SMS provider ulangunga qadar kod log'da ko'rinadi" izohi).
Telegram bot — birinchi haqiqiy yetkazish kanali.

---

## 1. Oqim

```
  ILOVA                         SERVER (AuthApi)                    TELEGRAM
    │                                 │                                │
    │ POST /api/Auth/TelegramLink     │                                │
    │  { userId }                     │  bir martalik token → Redis    │
    │ ◄── { url, isLinked }           │  (tg_link:{token} → userId)    │
    │                                 │                                │
    │ havola ochiladi ─────────────────────────────────────────────►   │
    │                                 │      /start {token}            │
    │                                 │ ◄────── getUpdates ────────────│
    │                                 │  token → userId eslab qolinadi │
    │                                 │  ──── "raqamni ulashing" ────► │
    │                                 │                                │
    │                                 │ ◄──── contact (raqam) ─────────│
    │                                 │  raqam profil raqamiga mos?    │
    │                                 │  ha → telegram_chat_id saqlanadi│
    │                                 │  ──── "hisob bog'landi" ─────► │
    │                                 │                                │
    │ Register / ResetPasswordRequest │  OTP yaratiladi                │
    │ ───────────────────────────────►│  ──── kod xabari ────────────► │
```

**Ilova qaysi profil ekanini havola ichidagi token orqali bildiradi** — server shu
token bo'yicha qaysi foydalanuvchiga kod yuborishni hal qiladi.

---

## 2. Xavfsizlik

Havolani kim bo'lsa ham yasab olishi mumkin (ro'yxatdan o'tish paytida ilovada
token yo'q), shuning uchun bog'lash **ikki qadamdan** iborat:

1. Havoladagi token — qaysi profil so'ralayotganini aytadi (bir martalik, 15 daqiqa).
2. Telegram'ning **o'zi tasdiqlagan telefon raqami** — foydalanuvchi "Telefon raqamni
   ulashish" tugmasini bosadi. Raqam profil raqamiga mos kelmasa bog'lanmaydi.

Shu sabab birovning `userId` si bilan havola yasab, uning kodlarini o'ziga burib
bo'lmaydi. Qo'shimcha himoya: boshqa odamning kontaktini yuborish rad etiladi
(`contact.user_id != from.id`), `telegram_chat_id` ustuni unikal — bitta chat
faqat bitta profilga bog'lanadi.

---

## 3. API

### `POST /auth/api/Auth/TelegramLink`
Autentifikatsiya talab qilmaydi (AuthApi'ning qolgan endpointlari kabi, IP bo'yicha
rate-limit ostida).

```json
// so'rov
{ "userId": 42 }

// javob
{
  "url": "https://t.me/BOTENERGY_bot?start=k3Jd9xQ2mVpA",
  "botUsername": "BOTENERGY_bot",
  "expiresAt": "2026-09-23T17:15:00",
  "isLinked": false
}
```

Xatolar: `404 USER_NOT_FOUND`, `409 TELEGRAM_NOT_CONFIGURED` (bot sozlanmagan).

`isLinked: true` bo'lsa profil allaqachon bog'langan — kodlar havolasiz ham keladi.

---

## 4. Serverda nima qo'shildi

| Fayl | Vazifasi |
|---|---|
| `Core/Domain/Options/TelegramOptions.cs` | `Telegram` bo'limi sozlamalari |
| `Core/Domain/Interfaces/Telegram/ITelegramBotClient.cs` | Bot API shartnomasi (`sendMessage`, `getUpdates`, kontakt so'rash) |
| `Core/Domain/Interfaces/Telegram/ITelegramLinkStore.cs` | bir martalik havola tokenlari ombori |
| `Core/Domain/Interfaces/Telegram/ITelegramGateway.cs` | havola yaratish, xabarlarni qayta ishlash, kod yuborish |
| `Infrastructure/CommonConfiguration/Telegram/TelegramBotClient.cs` | HTTP klient — `PaymeClient` andozasida, **istisno tashlamaydi** |
| `Infrastructure/CommonConfiguration/Redis/{Redis,InMemory,Resilient}TelegramLinkStore.cs` | `tg_link:{token}` → userId, `GETDEL` bilan atomik iste'mol |
| `Core/Application/Services/TelegramGateway.cs` | oqimning o'zi (havola, bog'lash, kod yuborish) |
| `Core/Application/Services/TelegramNotifyingOtpService.cs` | `IOtpService` dekoratori — kod yaratilishi bilan botga uzatadi |
| `WebApi/AuthApi/BackgroundServices/TelegramUpdatePollerService.cs` | `getUpdates` long polling |
| `WebApi/AuthApi/Controllers/AuthController.cs` | `POST /api/Auth/TelegramLink` |
| `Core/Domain/Entities/CustomerUserEntity.cs` + migratsiya `AddTelegramChatId` | `auth.customer_users.telegram_chat_id` (nullable, unikal indeks) |
| `Core/Domain/Guards/StopFactors.cs` | `TELEGRAM_NOT_CONFIGURED` (409) |

**Nega polling, webhook emas:** serverda ommaviy domen va TLS sertifikat yo'q,
Telegram esa webhook uchun HTTPS talab qiladi. Domen paydo bo'lgach webhook'ga
o'tish oson (`PaymeMerchantController` kabi `[AllowAnonymous]` endpoint + Gateway'da
`session-payme-merchant` uslubidagi route).

**Nega AuthApi:** kodlar shu jarayonda yaratiladi va AuthApi bitta systemd nusxasida
ishlaydi. Agar kelajakda AuthApi bir nechta nusxada ishlasa, poller Redis lock bilan
himoyalanishi kerak (aks holda xabarlar ikki marta o'qiladi).

---

## 5. Sozlash (ISHGA TUSHIRISH UCHUN SHART)

Token sir — repozitoriyda saqlanmaydi. Serverda:

```bash
sudo nano /etc/botenergy/botenergy.env
# oxiriga qo'shing:
Telegram__BotToken=<BotFather bergan to'liq token>

sudo systemctl restart botenergy-AuthApi
```

Tekshirish:

```bash
journalctl -u botenergy-AuthApi -n 50 | grep TG
# kutiladi: [TG] Xabarlarni tinglash boshlandi.
```

Token berilmasa integratsiya **jim turadi**: kodlar avvalgidek faqat logga yoziladi,
`TelegramLink` esa `409 TELEGRAM_NOT_CONFIGURED` qaytaradi. Ro'yxatdan o'tish oqimi
buzilmaydi.

Sozlamalar: `Configuration.Production.json` → `Telegram` bo'limi
(`BotToken: "Env_Telegram__BotToken"`, `BotUsername: "BOTENERGY_bot"`,
`Enabled: true`, `LinkTtlMinutes: 15`).

---

## 6. Ilova tomoni

`bot_mijoz` da OTP ekranlarida (ro'yxatdan o'tish va parolni tiklash) "Kodni
Telegram orqali olish" tugmasi bor: u serverdan havola olib, Telegram ilovasida
botni ochadi.

---

## 7. MUHIM cheklov — karta tasdiqlash kodi

**Kartani tasdiqlash SMS kodi Telegram orqali kelmaydi.** U bizning kodimiz emas:
Payme kartaga bog'langan telefon raqamiga o'zi yuboradi (`cards.get_verify_code`),
va bu kod serverga hech qachon ko'rinmaydi — biz uni qayta yo'naltira olmaymiz.

Bot orqali keladigan kodlar:

| Kod | Kanal |
|---|---|
| Ro'yxatdan o'tish (Register) | ✅ Telegram |
| Parolni tiklash (ResetPassword) | ✅ Telegram |
| Kartani tasdiqlash (Payme) | ❌ faqat SMS — Payme yuboradi |

---

## 8. Keyingi qadamlar (ixtiyoriy)

- SMS provayderi (Eskiz/Play Mobile) qo'shilsa, `TelegramNotifyingOtpService` kabi
  ikkinchi dekorator yozilib, Telegram bo'lmaganda SMS yuborilsin.
- Domen va sertifikat paydo bo'lgach polling o'rniga webhook.
- Botga sessiya/to'lov bildirishnomalari (`IPushNotificationService` hozir faqat
  logga yozadi — uning Telegram implementatsiyasi tayyor mantiqdan foydalanishi mumkin).
