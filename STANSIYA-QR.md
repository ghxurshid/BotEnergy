# Kolonka ekranidagi bir martalik QR — integratsiya hujjati

**Kimga:** zapravka (kolonka) dasturini yozadigan jamoaga.
**Nima uchun:** kolonka ekranida mijoz skanerlaydigan **unikal, qisqa muddatli** QR kodni
ko'rsatish va mijoz ulangach ekranni to'g'ri holatga o'tkazish.

Serverdagi o'zgarishlar allaqachon kiritilgan (quyida "Serverda nima qilindi" bo'limi).
Bu hujjat qurilma tomonida nima qilish kerakligini belgilaydi.

---

## 1. Nega bir martalik kod kerak

Ilgari ulanish ikki xil edi:

| Usul | Kim skanerlaydi | Kamchiligi |
|---|---|---|
| Pending token (eski) | kolonka readeri telefondagi QR ni | kolonkada reader bo'lishi shart |
| Seriya raqamli stiker | telefon kolonkadagi stikerni | stiker statik: rasmini olib, uzoqdan sessiya ochib qo'yish mumkin |

Bir martalik QR ikkala kamchilikni yopadi: **reader kerak emas**, kod esa har
safar yangi, qisqa muddatli va bir marta ishlatiladi.

---

## 2. Umumiy oqim

```
  KOLONKA                          SERVER                          MIJOZ TELEFONI
     │                                │                                  │
     │  MQTT: qr.issue                │                                  │
     ├───────────────────────────────►│                                  │
     │  ◄── { qr_payload, ttl }       │  kod Redis'ga yoziladi (TTL)     │
     │                                │                                  │
     │  ekranda QR chiziladi          │                                  │
     │                                │       QR skanerlanadi            │
     │                                │◄─────────────────────────────────┤
     │                                │  POST /api/Session/ConnectByQr   │
     │                                │  kod tekshiriladi va KUYDIRILADI │
     │                                │  sessiya ochiladi (Connected)    │
     │  ◄── MQTT: session.attached ───┤                                  │
     │  QR olib tashlanadi,           │  ──── sessiya ma'lumoti ────────►│
     │  "mijoz ulandi" ko'rsatiladi   │                                  │
     │                                │                                  │
     │  ◄── MQTT: process.start ──────┤  (mijoz to'lovni qilgach)        │
```

---

## 3. Qurilma tomonida nima qilish kerak

### 3.1. Kod so'rash — `qr.issue`

Kolonka **bo'sh (idle)** turganda serverdan kod so'raydi va ekranda QR chizadi.

**Topic:** `device/{serial}/request`
**Envelope:** loyihadagi standart (`id`, `type`, `timestamp`, `payload`, `hmac`) —
o'zgarishsiz, `session.connect` bilan bir xil imzolash qoidasi.

```json
{
  "id": 1042,
  "type": "qr.issue",
  "timestamp": 1758812345,
  "payload": { "ttl_seconds": 120 },
  "hmac": "…"
}
```

`ttl_seconds` — **ixtiyoriy**. Berilmasa server 120 soniya beradi; 600 dan katta
qiymat 600 ga qisqartiriladi.

**Javob** (`device/{serial}/response`, `id` bilan korrelyatsiya qilinadi):

```json
{
  "id": 1042,
  "type": "qr.issue",
  "timestamp": 1758812345,
  "payload": {
    "code": "SUCCESS",
    "message": "QR kod berildi.",
    "data": {
      "qr_payload": "BE1:Q8sK2mV9xLpA",
      "ttl_seconds": 120,
      "expires_at": 1758812465
    }
  },
  "hmac": "…"
}
```

**Ekranda aynan `qr_payload` matni QR sifatida chiziladi** — hech narsa qo'shilmaydi,
hech narsa olib tashlanmaydi. `BE1:` prefiksi ilovaga bu kodni oddiy stikerdan
ajratish imkonini beradi.

### 3.2. Kodni yangilab turish

- Kod `ttl_seconds` davomida amal qiladi (standart 120 s).
- Qurilma **muddati tugashidan ~10–15 soniya oldin** yangi kod so'raydi va ekranni yangilaydi.
- Har `qr.issue` so'rovi **yangi kod** qaytaradi va **avvalgisini bekor qiladi** —
  ya'ni bir vaqtda faqat bitta amaldagi kod bo'ladi. Shu sabab ekranda ko'rsatilgan
  kod bilan serverdagi kod doim bir xil bo'lishi uchun ekran faqat oxirgi javobdagi
  `qr_payload` ni chizishi kerak.

### 3.3. Mijoz ulanganda — `session.attached`

Server qurilmaga yuboradi (`server/{serial}/request`):

```json
{
  "id": 88123,
  "type": "session.attached",
  "timestamp": 1758812400,
  "payload": { "session_id": 512, "user_id": 42 },
  "hmac": "…"
}
```

Qurilma bu xabarni olgach:

1. Ekrandan QR ni olib tashlaydi.
2. "Mijoz ulandi / to'lov kutilmoqda" holatini ko'rsatadi.
3. **Yangi `qr.issue` so'ramaydi** — sessiya tugagunicha.

### 3.4. Sessiya tugaganda

- Server `session.close` xabarini yuboradi (bu allaqachon mavjud, o'zgarmagan).
- Quridma idle holatiga qaytadi va **yana `qr.issue`** so'rab, ekranda yangi QR chizadi.

### 3.5. Xatolar

`qr.issue` javobidagi `code` `SUCCESS` bo'lmasligi mumkin:

| `code` | Ma'nosi | Qurilma nima qiladi |
|---|---|---|
| `DEVICE_NOT_FOUND` | seriya raqami bazada yo'q | ekranda "qurilma ro'yxatdan o'tmagan", 30 s dan keyin qayta urinish |
| `DEVICE_INACTIVE` | qurilma faol emas | "xizmat ko'rsatilmayapti", 60 s dan keyin qayta urinish |
| `DEVICE_NO_STATION` | qurilma stansiyaga biriktirilmagan | operatorga xabar, 60 s dan keyin qayta urinish |
| `STATION_INACTIVE` | stansiya faol emas | "stansiya yopiq", 60 s dan keyin qayta urinish |
| `HMAC_INVALID`, `TIMESTAMP_SKEW`, `REPLAY_REJECTED` | envelope xatosi | vaqtni sinxronlash / kalitni tekshirish (mavjud qoidalar) |
| javob umuman kelmasa | broker yoki server bilan aloqa yo'q | 10 s dan keyin qayta urinish, ekranda "aloqa yo'q" |

---

## 4. Serverda nima qilindi (ma'lumot uchun)

| Fayl | O'zgarish |
|---|---|
| `Core/Domain/Interfaces/IDeviceQrStore.cs` | bir martalik kodlar ombori interfeysi (`Set` / `Get` / `Consume` / `InvalidateForDevice`) |
| `Infrastructure/CommonConfiguration/Redis/RedisDeviceQrStore.cs` | Redis implementatsiyasi. Kalitlar: `device_qr:code:{code}`, `device_qr:device:{deviceId}`. Iste'mol `GETDEL` bilan — **atomik** |
| `…/InMemoryDeviceQrStore.cs`, `…/ResilientDeviceQrStore.cs` | Redis yiqilganda zaxira (pending sessiya bilan bir xil andoza) |
| `Core/Application/Services/SessionService.cs` | `IssueDeviceQrAsync` (kod berish) va `ConnectByQrAsync` (kod bo'yicha sessiya ochish) |
| `WebApi/SessionApi/Mqtt/Handlers/QrIssueHandler.cs` | `qr.issue` MQTT handleri |
| `WebApi/SessionApi/Mqtt/Handlers/MqttHandlerTypes.cs` | yangi turlar: `qr.issue` (device→server), `session.attached` (server→device) |
| `WebApi/SessionApi/Messaging/MqttDeviceCommandPublisher.cs` | `PublishSessionAttachedAsync` |
| `WebApi/SessionApi/Controllers/SessionController.cs` | `POST /api/Session/ConnectByQr { code }` |
| `Core/Domain/Guards/StopFactors.cs` | `SESSION_QR_INVALID` (409) |

**Ma'lumotlar bazasi o'zgarmadi — migratsiya yo'q.** Kodlar faqat Redis'da yashaydi.

### Kod qanday hosil qilinadi

- 9 bayt kriptografik tasodifiy son (`RandomNumberGenerator`) → base64url → **12 belgi**.
- Kod qurilmaga bog'lanadi: `{deviceId, serialNumber, expiresAt}`.
- Taxmin qilish ehtimoli amalda nolga teng (72 bit), ustiga muddat 2 daqiqa va bir martalik.

### Server tekshiruvlari (`ConnectByQr`)

Tartib bilan: kod bor va muddati o'tmagan → foydalanuvchi bloklanmagan → qurilma
bor va faol → stansiyaga biriktirilgan va stansiya faol → mijozda boshqa aktiv
sessiya yo'q. Hammasi o'tgandagina sessiya ochiladi va **shundan keyin** kod kuydiriladi
(tekshiruvdan o'tmasa kod saqlanib qoladi, mijoz qayta urinib ko'radi).

---

## 5. Ilova (mijoz telefoni) tomoni — ma'lumot uchun

- QR matni `BE1:` bilan boshlansa → `POST /api/Session/ConnectByQr { code }`.
- Aks holda (eski stiker) → `POST /api/Session/ConnectByDevice { serialNumber }`.
- Sessiya ochilgach ilova darhol yoqilg'i turi + to'lov oynasini ochadi.

---

## 6. Sinov ro'yxati (qurilma tomoni uchun)

- [ ] Idle holatda `qr.issue` yuboriladi va ekranda QR chiziladi
- [ ] QR matni javobdagi `qr_payload` bilan **aynan** bir xil
- [ ] Muddat tugashidan oldin yangi kod so'raladi, ekran yangilanadi
- [ ] Eski QR ni skanerlash `SESSION_QR_INVALID` beradi (ilovada "yangi kodni skanerlang")
- [ ] Bir QR ni ikki telefon skanerlasa — faqat bittasida sessiya ochiladi
- [ ] `session.attached` kelganda QR olib tashlanadi va yangi kod so'ralmaydi
- [ ] `session.close` kelganda idle holatga qaytiladi va yangi QR chiziladi
- [ ] Server javob bermasa ekranda "aloqa yo'q" va qayta urinish ishlaydi
- [ ] Qurilma o'chib-yonganda ham oqim avtomatik tiklanadi

---

## 7. Eski oqimlar

`session.connect` (kolonka readeri telefondagi QR ni o'qishi) **o'zgarmadi va
ishlayveradi**. Readerli kolonkalar eski usulda, readersiz kolonkalar esa ekrandagi
QR bilan ishlaydi — ikkalasi bir xil natijaga olib keladi.
