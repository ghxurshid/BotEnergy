# HTTPS (TLS) ga ko'chish rejasi

Hozirgi holat: barcha ilovalar serverga **shifrlanmagan HTTP** orqali murojaat
qiladi (`http://51.38.127.221:5001…5007`). Karta raqami, parol va OTP kodlari
ochiq matnda uzatilmoqda. Bu **darhol tuzatilishi kerak** — ham xavfsizlik, ham
Google Play talabi (Data safety) uchun.

---

## Nima tayyor

| Qadam | Holat |
|---|---|
| Ilovalarda manzillar bitta joyga yig'ildi (`ApiConfig`) | ✅ 5 ta ilovada bajarildi |
| Server uchun nginx + Let's Encrypt skripti | ✅ [`deploy/tls-setup.sh`](deploy/tls-setup.sh) |
| Gateway (5008) barcha servislarni birlashtiradi | ✅ allaqachon ishlayapti |
| Domen (DNS) | ❌ `botep.uz` hech qayerga yo'naltirilmagan |
| Sertifikat | ❌ domen tayyor bo'lgach olinadi |

---

## 1-qadam. DNS (siz qilasiz)

Domen qayd etilgan joyda **A-yozuv** qo'shing:

```
api.botep.uz.   A   51.38.127.221
```

Tekshirish: `getent hosts api.botep.uz` — natijada 51.38.127.221 chiqishi kerak
(tarqalish 5 daqiqadan bir necha soatgacha davom etishi mumkin).

## 2-qadam. Serverda TLS (bitta buyruq)

```bash
cd ~/botenergy-repo/deploy   # yoki skript joylashgan papka
sudo DOMAIN=api.botep.uz EMAIL=botenergyuz@gmail.com ./tls-setup.sh
```

Skript: nginx + certbot o'rnatadi, sertifikat oladi, 443-portni Gateway'ga
(127.0.0.1:5008) ulaydi, HSTS va boshqa xavfsizlik sarlavhalarini qo'yadi,
WebSocket (SignalR) ni to'g'ri uzatadi, avtomatik yangilanishni tekshiradi.

Natija: `https://api.botep.uz/health/live` → `200 Healthy`.

## 3-qadam. Ilovalarni o'tkazish (men qilaman)

Har ilovadagi `ApiConfig` faylida **uch qator**:

```dart
static const String scheme = 'https';
static const String host   = 'api.botep.uz';
static const bool viaGateway = true;
```

`viaGateway = true` bo'lganda manzillar portsiz, Gateway prefikslari bilan
quriladi: `/auth`, `/user`, `/session`, `/admin`, `/billing`.

So'ng: `flutter test` → `flutter build` → qurilmalarda tekshirish.

## 4-qadam. Portlarni yopish

Hamma ilova HTTPS'ga o'tgani tasdiqlangach:

```bash
sudo ./firewall.sh lockdown     # 5001–5007 tashqaridan yopiladi
```

Shundan keyin serverga faqat 443 (va 22) orqali kiriladi.

## 5-qadam. Android tomonida cleartext'ni taqiqlash

`AndroidManifest.xml` da:

```xml
<application android:usesCleartextTraffic="false" ... >
```

Bu tasodifan `http://` qolib ketgan joyni darhol ko'rsatadi.

---

## Nega Gateway orqali

- Bitta domen, bitta sertifikat — har servis uchun alohida sertifikat shart emas.
- Ichki portlar (5001–5007) umuman tashqariga chiqmaydi.
- Rate limiting va audit bitta kirish nuqtasida.
- Telegram **webhook**ni ishlatish imkoni paydo bo'ladi (hozir long polling).

## Ko'chishdan keyin tekshiriladigan ro'yxat

- [ ] `https://api.botep.uz/health/live` → 200
- [ ] Sertifikat haqiqiy (brauzerda ogohlantirish yo'q), TLS 1.2+
- [ ] Login → token → profil (bot_mijoz)
- [ ] Karta ro'yxati va karta qo'shish
- [ ] QR → sessiya → to'lov → quyish (SignalR ulanishi ham)
- [ ] Operator/Shahobcha/Admin ilovalari kirish va asosiy ro'yxatlar
- [ ] Telegram bot havolasi va kod yuborish
- [ ] Qurilmalar (MQTT) — 1883/8883 alohida, nginx ularga tegmaydi
- [ ] `firewall.sh lockdown` dan keyin ham hammasi ishlayapti
