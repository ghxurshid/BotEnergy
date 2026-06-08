# BotEnergy — Funksional Promptlar

Bu hujjat platformaning **hozirgi koddan ekstrakt qilingan** asosiy biznes-jarayonlarini human-readable, mustaqil prompt sifatida yozadi. Har bir bo'lim — alohida, biri-biriga bog'liq emas. Maqsad: o'qib chiqib, kerakli joyini tuzatish, keyin shu spec asosida kodni tekshirish/yangilash.

> Format konventsiyalari: "must" (shart), "may" (ixtiyoriy), "should" (kutilgan default). Numerik konstantalar — koddan olingan haqiqiy qiymatlar. "QR" — JSON formatdagi `{ userId, sessionToken }`. Vaqt — lokal vaqt (PostgreSQL `timestamp without time zone`, `DateTime.Now`).

---

## 1) Auth — Autentifikatsiya

Maqsad: foydalanuvchilar **ikki guruhga** bo'lingan. **Customer** — jismoniy shaxs (`CustomerUser/Natural`) o'zi ro'yxatdan o'tadi; tashkilot xodimi (`CustomerUser/Corporate`) ni admin yaratadi (4-bo'lim). **Platform** — Manage/Merchant (`PlatformUser`) ni Manage yaratadi, **self-register yo'q** (2-bo'lim). Hamma auth endpointlari **ochiq** (`[Authorize]` yo'q), JWT keyingi APIlar uchun. Ikki yuza: `/api/Auth/*` (customer) va `/api/PlatformAuth/{Login,RefreshToken}` (platform).

### 1.1 Customer ro'yxat (4 qadam) — faqat jismoniy shaxs (Natural) uchun
1. **Register** (`/api/Auth/Register`) — `{ phoneId, phoneNumber, mail? }`. Telefon bo'yicha:
   - Allaqachon `IsVerified=true` → "Allaqachon ro'yxatdan o'tilgan, login qiling".
   - Mavjud lekin OTP tasdiqlanmagan → yangi OTP generatsiya.
   - Yangi → `CustomerUserEntity` (Type=Natural) yaratiladi, **default global Natural rol avtomatik biriktiriladi**, OTP generatsiya.
   OTP konsolga chiqariladi (SMS integratsiya yo'q). **Test OTP: `123456`** har doim qabul qilinadi.
2. **Verify** — `{ userId, otpCode }` → `IsOtpVerified=true`. Allaqachon tasdiqlangan bo'lsa 400.
3. **SetPassword** — `{ userId, password }` (min 6 belgi). Parol SHA-256+salt bilan saqlanadi. `IsVerified=true`. Bu qadamda **access + refresh token juftligi qaytadi**.
4. **Login** — `{ phoneNumber, password }` → parol mos + `IsVerified=true` + `IsBlocked=false`. Yangi token juftligi, `LastLoginDate` yangilanadi.

### 1.1b Platform login (Manage/Merchant)
- `POST /api/PlatformAuth/Login` `{ phoneNumber, password }` — self-register yo'q; userni Manage yaratadi va parol o'rnatadi. `POST /api/PlatformAuth/RefreshToken` `{ refreshToken }`.

### 1.2 Token
- **Access token** — 15 daqiqa, claimlar: `nameid` (userId), `unique_name` (telefon), `UserGroup` (Platform|Customer), `UserSubType` (Manage|Merchant|Natural|Corporate), Merchant→`MerchantId`, Corporate→`OrganizationId`, `Permission` (har biri alohida claim), `exp`.
- **Refresh token** — 7 kun, qiymat guruh prefiksi bilan: `c:` (customer) / `p:` (platform). Redis'da kalit. **Token rotation**: har refresh'da eski revoke, yangi juftlik. Redis yo'q bo'lsa in-memory fallback.
- **RefreshToken** — har yuza faqat o'z prefiksli tokenini qabul qiladi; eski o'chiriladi, `IsBlocked/IsDeleted` tekshiriladi, yangi juftlik.

### 1.3 Parolni tiklash (3 qadam)
1. **ResetPasswordRequest** — `{ phoneNumber }` → reset uchun OTP yuboriladi.
2. **ResetPasswordVerify** — `{ userId, otpCode }`.
3. **ResetPasswordSet** — `{ userId, newPassword }`. OTP "iste'mol qilinadi" (bir martalik).

### 1.4 Xato javoblari
- `404` foydalanuvchi topilmadi (telefon yo'q yoki `IsDeleted=true`).
- `400` parol noto'g'ri / ro'yxat tugallanmagan / OTP eskirgan.
- `403` foydalanuvchi bloklangan.

---

## 2) Platform admin / Platform user

Platform admin — eng yuqori darajadagi rol. **Tizim sozlash** uchun ishlatiladi: merchant'lar va organization'larni qo'shish, rollar va permission'larni boshqarish, har qanday merchant/organization doirasida CRUD qila olish.

### 2.1 Merchant boshqaruvi
- **CRUD** `MerchantEntity` ustida (`phoneNumber`, `inn`, `bankAccount`, `companyName`, `isActive`).
- `phoneNumber` unikal — dublikatda 409.
- Delete — soft delete (`IsDeleted=true`, `IsActive=false`).
- Permission'lar: `MerchantAdmin.Register|GetAll|GetById|Update|Delete`.

### 2.2 Organization boshqaruvi
- **CRUD** `OrganizationEntity` (`name`, `inn`, `address`, `phoneNumber`, `balance`).
- `Name` va `Inn` — yaratilgandan keyin **read-only**, faqat `Address`, `PhoneNumber`, `IsActive` yangilanadi.
- Permission'lar: `OrganizationAdmin.Create|GetAll|GetById|Update|Delete`.

### 2.3 Station / Device / Product boshqaruvi (super-admin)
Platform admin **istalgan merchant'ga** quyi resurslar yaratishi mumkin (Merchant admin esa faqat o'ziniki — qarang 3-bo'lim):
- Station (Merchant ichida), Device (Station ichida), Product (Device ichida).
- Ierarxiya validatsiyasi: parent topilishi va `IsActive=true` bo'lishi shart.
- Device `serialNumber` unikal; yaratilganda `SecretKey` (32-belgi hex) avtomatik generatsiya — qurilma MQTT envelope HMAC uchun ishlatadi.
- Product turi device turiga **mos** kelishi shart (DeviceType→ProductType mapping bor: `FUEL_DISPENSER → Petrol/Diesel/Methane/Propane`, `WASH_BOX → Water/Foam/Wax`, `CHARGER → Electricity`, `WATER_DISPENSER → PurifiedWater/ColdWater/HotWater`, `VACUUM_CLEANER → VacuumService`, `VENDING_MACHINE → Coffee/Tea/ColdDrink/Snack`).

### 2.4 Platform foydalanuvchi yaratish (`/api/User/Create`, faqat Manage)
Manage **Manage** yoki **Merchant** platform user yarata oladi:
- `{ phoneId, mail, phoneNumber, roleId, type, merchantId? }`. `type` — enum **raqam**: `0=Manage`, `1=Merchant`.
- `type=Manage` → rol global bo'lishi shart (`role.MerchantId == null`).
- `type=Merchant` → `merchantId` majburiy, merchant `IsActive`, rol shu merchantga tegishli (`role.MerchantId == merchantId`).
- Scope: Merchant operator (UserAdmin.* ruxsati bo'lsa) faqat o'z `MerchantId` operatorlarini yaratadi.
- Yangi user `IsOtpVerified=true`, `IsVerified=false` — keyin `SetPassword`. Telefon unikal (409).
- Corporate userlar bu yerda emas — `/api/CorporateUser/*` (4-bo'lim).

### 2.5 Platform Role / Permission boshqaruvi (`/api/Role/*`)
- **GetAllowedPermissions** (`?kind=PlatformManage|PlatformMerchant`) — `RoleKind` uchun ruxsat etilgan permission'lar.
- **CreateRole** — `{ name, description, isActive, merchantId?, permissionIds[] }`. `merchantId` null → PlatformManage (global) rol; to'ldirilsa → PlatformMerchant. Har permission `PermissionScopes.IsAllowedFor(kind, ...)` bilan tekshiriladi.
- **Update** — yangi permission ro'yxati bilan: yo'q bo'lganlar `IsDeleted=true`, yangilari `PlatformRolePermissionEntity`.
- Corporate rollar `/api/CorporateRole/*` da. Permission claim'lari JWT'ga login/refresh paytida yoziladi.

### 2.6 Foydalanuvchi statusi (admin)
- **Block / Unblock** — `IsBlocked` flag. Bloklangan user login/refresh/session yaratolmaydi.
- **SetPassword** (birinchi marta, `IsOtpVerified=true && IsVerified=false` shartda), **ResetPassword** (`IsVerified=true` shartda).
- **Delete** — soft delete.

### 2.7 Balans (admin)
- `POST /api/Balance/TopUp` — `{ userId, amount }` (`amount > 0`). Customer user: `Natural` → `user.Balance`, `Corporate` → `organization.Balance`.

### 2.8 Audit / to'lov boshqaruvi (admin)
- Barcha to'lov tranzaksiyalari ro'yxati (filtr: `status`, `from/to` sana, paginatsiya).
- Bitta tranzaksiya tafsiloti + audit step'lar.
- **Reverse** — muvaffaqiyatli to'lovni qo'lda bekor qilish: `{ reason }` shart, balansdan ayiriladi, `Status=Reversed`, `Reversed` step yoziladi.

### 2.9 Hisobotlar
- Merchant savdo hisoboti (har merchant/station bo'yicha), Organization usage hisoboti — qarang 7-bo'lim.

---

## 3) Merchant admin / Merchant user

**Merchant** — sotuvchi biznes (yoqilg'i kompaniyasi, avtomoyka tarmog'i, va h.k.). Ierarxiya: `Merchant → Station → Device → Product`. Operatorlar — **PlatformUser/Merchant** (subtip `Merchant`), `MerchantId` orqali butun biznesga scoped (avvalgidek bitta Station'ga emas).

### 3.1 Merchant operator (o'z biznesi doirasida)
- O'z `MerchantId` biznesi uchun **Station/Device/Product CRUD**:
  - Station yaratishda — `Merchant.IsActive=true` + caller `MerchantId == dto.MerchantId` (`MerchantAdmin.Register` Manage permission'i bo'lsa cheklov yo'q).
  - Device yaratishda — Station `IsActive=true` + caller PlatformUser/Merchant bo'lsa `station.MerchantId == caller.MerchantId` shart.
  - Product yaratishda — Device `IsActive=true` + parent Station orqali merchant tekshiriladi + Product type device turiga mos.
- O'z biznesi ichida yangi Merchant operator yaratish (`UserAdmin.Create` + `type=Merchant, merchantId`).
- Soft delete; physical delete yo'q.

### 3.2 Scope (AccessScope)
- JWT'da `UserGroup=Platform, UserSubType=Merchant, MerchantId=<id>`. Scope servislari: Manage → cheklovsiz (`IsManage`); Merchant → `CanAccessMerchant(id)` (faqat `MerchantId`); read/GetAll merchant bo'yicha filtrlanadi.
- Hisobot — qaysi station, qaysi qurilma necha so'm tushum keltirgani (qarang 7-bo'lim).

### 3.3 Hisobotlar (Merchant tomonidan)
- `MerchantReport.Sales` — qaysi davrda merchant qaysi station'da nimani sotgani (sahifalab).
- `MerchantReport.SalesExport` — Excel (xlsx) eksport, paginatsiyasiz.
- Filtrlar: `merchantId` (shart), `stationId?` (bo'sh = barcha station'lar), `from`/`to`.
- Davr 1 yildan oshmasligi tekshiriladi.

### 3.4 Cheklovlar
- Merchant operator **customer/organization tomon ishlay olmaydi** (alohida guruh).
- `MerchantAdmin.Register` / `OrganizationAdmin.*` / `Balance.TopUp` — `ManageOnly`, Merchant rolga biriktirilmaydi → boshqa merchant'larga ham aralasholmaydi.

---

## 4) Corporate (tashkilot) admin / Corporate user

**Organization** — iste'molchi tashkilot. **Bitta umumiy balansi** bor; uning **Corporate** customer userlari (`CustomerUser/Corporate`) shu balansdan foydalanadi. Organization yozuvini Manage yaratadi (`/api/Organization/*`).

### 4.1 Corporate admin (`/api/CorporateUser/*`, `/api/CorporateRole/*`)
- O'z tashkiloti doirasida **Corporate user CRUD** (Manage istalgan org uchun; corp-admin faqat o'z org'i — `AccessScope.CanAccessOrganization`). Gating: `CustomerAdmin.*`.
- Corporate user yaratish: `{ phoneId, mail, phoneNumber, roleId, organizationId }`. Rol shu tashkilotning `CustomerRole`'i bo'lishi shart.
- Corporate rol: `/api/CorporateRole/Create` `{ name, description, isActive, organizationId, permissionIds }` (CorporateAllowed to'plamdan); `AllowedPermissions` endpoint mavjud.
- Corporate user parolini o'rnatish/reset, block/unblock/delete.
- Tashkilot balansini to'ldirish (admin BillingApi yoki Payme — 6-bo'lim).
- Tashkilot ma'lumotlarini yangilash (`/api/Organization/Update`): `Address`, `PhoneNumber`, `IsActive` (Name/Inn read-only).

### 4.2 Corporate user (xodim)
- Mobile app orqali kiradi (customer login) va xizmatdan foydalanadi.
- **Balans**: o'z `user.Balance` emas, `organization.Balance` ko'rinadi va shu yerdan yechiladi.
- Sessiya/jarayon endpointlari Natural bilan **bir xil** — farq faqat balans manbai (`CustomerUserType` orqali aniqlanadi).

### 4.3 Hisobotlar (Organization)
- `OrganizationReport.Usage` — tashkilot xodimlari qaysi davrda, qaysi qurilmada qancha xizmat olgan (sahifalab).
- `OrganizationReport.UsageExport` — Excel eksport.
- Filtr: `organizationId` (shart) + davr.
- Tashkilot doirasidan tashqari ma'lumot **ko'rsatilmaydi**.

### 4.4 To'lov tarixi (Corporate user ham, corp-admin ham)
- O'z shaxsiy tarixi: `Payment.GetMyTransactions` (faqat shu user initiate qilgan to'lovlar).
- Tashkilot tarixi: `Payment.GetOrganizationTransactions` — caller Corporate user bo'lishi shart, **URL parametri sifatida `organizationId` qabul qilinmaydi** (caller profilidan olinadi) — boshqa tashkilot ma'lumotini ko'rishni oldini olish uchun.

---

## 5) Sessiya oqimi va boshqaruvi

Sessiya — bitta mobil foydalanuvchi va bitta IoT qurilma o'rtasidagi vaqtli aloqa. Bir foydalanuvchida bir vaqtda **faqat bitta** aktiv sessiya bo'la oladi.

### 5.1 Cold-start (Bootstrap)
- Mobile app har ishga tushganda: `GET /api/Session/Bootstrap` → `{ user, activeSession?, serverTime }` bitta javobda.
- `activeSession` mavjud bo'lsa app darhol **resume holatga** o'tadi: device ma'lumotlari, mahsulotlar ro'yxati va aktiv jarayon (agar bor bo'lsa) — barchasi shu javobda.

### 5.2 Sessiya yaratish (Pending → QR)
- `POST /api/Session/Create` — body bo'sh, `userId` JWT'dan. `Idempotency-Key` qo'llab-quvvatlanadi.
- DB'da aktiv sessiya bor bo'lsa **409** — avval yopish kerak.
- Pending cache'da (Redis) `sessionToken` saqlanadi, TTL 30 daqiqa. DB'ga **hali yozilmaydi**. Mavjud pending bo'lsa shu token idempotent qaytariladi.
- Response: `{ userId, sessionToken, idleAfter }`. App `sessionToken`'ni **QR sifatida** ko'rsatadi (`{ userId, sessionToken }` JSON).

### 5.3 Qurilma ulanishi (QR skanerlanadi)
- Qurilma QR'ni o'qib MQTT'da yuboradi: `device/{serial}/connect` topic, payload `{ user_id, session_token }`. Xabar HMAC-SHA256 envelope ichida — `id` (qurilma counter), `hmac_key = SHA-256("BOT-ENERGY-MQTT-HMAC:" + device.SecretKey)`.
- Server tekshiradi: pending cache'da token bormi, qurilma `IsActive=true` ekanmi, userning DB-da boshqa aktiv sessiyasi yo'qmi.
- O'tsa: sessiya **DB'ga `Connected` statusda yoziladi**, pending cache tozalanadi. SignalR orqali app'ga `DeviceConnected` event keladi:
  ```
  { session_id, device_id, serial_number, device_type, model?, products: [{ productId, name, type, unit, price }], connected_at }
  ```
- Server qurilmaga `server/{serial}/connect_ack` (correlation: device request id'sini echo qiladi) — `{ ok, code, data: { session_id } | null }`.
- Xato kodlari: `NO_PENDING_SESSION`, `TOKEN_MISMATCH`, `DEVICE_UNKNOWN`, `ACTIVE_SESSION_EXISTS`, `INVALID_PAYLOAD`, `INTERNAL_ERROR`.

### 5.4 Sessiyani ko'rish / yopish
- `GET /api/Session/Current` — joriy aktiv sessiya (yo'q bo'lsa `null`). Mahsulotlar va aktiv jarayon ham shu javobda.
- `GET /api/Session/{id}` — bitta sessiya tafsiloti (faqat egasi). Soft-delete'lar yashirin.
- `POST /api/Session/Close` — `{ sessionId }`. Caller egasi bo'lishi shart. Aktiv jarayonlar **avval to'xtatiladi va balansdan yechiladi** (qarang Process), keyin sessiya `Closed`+`UserClosed` bo'ladi. Qurilma device-lock'i bo'shatiladi.
- `POST /api/Session/Heartbeat/{id}` — app foreground'da 30 soniyada bir marta. `LastActivityAt` yangilanadi (atomic SQL UPDATE).

### 5.5 Sessiya statusi
```
Created → Connected → InProcess → Closed
```
- `Created` — pending cache, DB'da yo'q.
- `Connected` — qurilma ulangan, jarayon hali yo'q.
- `InProcess` — birinchi telemetry kelganda (Start emas, telemetry'ga ko'ra) avtomatik o'tadi.
- `Closed` — yopilgan. `CloseReason`: `UserClosed | DeviceLost | Timeout`.

### 5.6 Process (jarayon) boshqaruvi

Bitta sessiyada bir vaqtda **faqat bitta tugamagan jarayon** bo'la oladi.

#### Start
- `POST /api/Process/Start` — `{ sessionId, productId, requestedAmount? }`. `Idempotency-Key`.
- Sessiya `Connected` yoki `InProcess` bo'lishi shart, jarayon faolligi shart.
- Mahsulot device'ga tegishli bo'lishi shart (`product.DeviceId == session.DeviceId`).
- **Balans tekshiruvi**: `maxAmount = balance / price`. Limit = `min(requestedAmount, maxAmount)`. `<= 0` bo'lsa 400 "Balans yetarli emas".
- **Device lock** (Redis, `device:lock:{serial}` → userId, TTL 30 min). Lock egasi shu user bo'lsa o'tadi, boshqa user bo'lsa 409.
- `ProductProcessEntity` yaratiladi: `Status=Started`, `RequestedAmount=limit`, `PricePerUnit`, `Unit`, `StartedAt`. Sessiya `InProcess`'ga o'tadi.
- RabbitMQ → DeviceApi → MQTT: `server/{serial}/command`:
  ```
  { type:"start", process_id, product_id, amount, product_name, unit, price_per_unit }
  ```
- SignalR `ProcessStarted`:
  ```
  { process_id, product_id, product_name, unit, price_per_unit, requested_amount, started_at }
  ```

#### Pause / Resume
- `POST /api/Process/Pause/{processId}` va `Resume/{processId}` — egalik tekshiriladi.
- Pause faqat `Started/InProcess`'dan ishlaydi; Resume faqat `Paused`'dan.
- Statusni o'zgartiradi, MQTT command yuboradi (`pause`/`resume`), SignalR `ProcessUpdated` (`status`, `paused_at?`, `total_given`, `current_cost`).

#### Stop (foydalanuvchi)
- `POST /api/Process/Stop/{processId}` yoki `/processes/{id}/stop`.
- MQTT `stop`, status `Ended` + `EndReason=UserStopped`, `EndedAt`. Balansdan yechiladi (`GivenAmount * PricePerUnit`, lekin balansdan **ortiqcha emas** — `Math.Min`). Device lock bo'shatiladi.
- SignalR `ProcessEnded` (`process_id`, `end_reason`, `total_given`, `total_cost`, `ended_at`).

#### Auto-complete (limit yetganda)
- Telemetry kelganda `total_given >= requestedAmount` bo'lsa server **avtomatik**: `EndReason=Completed`, MQTT stop, balansdan yechiladi, SignalR `ProcessEnded`.

#### Device tomonidan tugatish
- Qurilma `device/{serial}/event` topic'ga `{ type: "completed|stopped|out_of_resource", session_token, process_id, total_given }` yuboradi.
- Server: `ReportDeviceFinishedAsync` → idempotent (allaqachon `Ended` bo'lsa qayta yechmaydi). `EndReason` mapping: `completed→Completed`, `stopped→UserStopped`, `out_of_resource→OutOfResource`, boshqa → `DeviceError`.

### 5.7 Telemetriya

- Qurilma har tick (default ~1s) `device/{serial}/telemetry` topic'ga `{ session_token, process_id, sequence, total_given }` yuboradi. Envelope+HMAC.
- **`total_given`** — jarayon boshidan beri qurilma jami bergan miqdor (**cumulative**, delta emas). Misol: t1=56, t2=59 → orada 3 unit berilgan.
- **Real-time oqim:** `MqttBridge.HandleTelemetryAsync` → **to'g'ridan-to'g'ri** `ProcessService.ReportTelemetryAsync` (RabbitMQ orqali emas). Boshqa qurilma eventlari (connect, finished, payment_qr) RabbitMQ orqali, lekin telemetry latency uchun broker hop o'tkazib yuborildi.
- Server `ReportTelemetryAsync`:
  - SessionToken va SerialNumber `process.Session` bilan mos kelishi shart (aks holda 403).
  - **Atomic + idempotent SQL UPDATE** — `GivenAmount = total_given` (SET, increment emas). Sequence asosida duplikat/eski xabar o'tkazib yuboriladi.
  - Birinchi telemetry: `Status=Started → InProcess`.
  - SignalR `ProcessUpdated`: `{ process_id, total_given, current_cost, product_id, unit, price_per_unit }`.

### 5.8 Heartbeat va Status (qurilma tomondan)

- **Heartbeat** — qurilma har ~30s da `device/{serial}/heartbeat` topic'ga `{}` yuboradi. Server `LastSeenAt`'ni yangilab turadi. Hech qanday SignalR push yo'q.
- **Status** — qurilma har ~60s da `device/{serial}/status` topic'ga **plain JSON** (envelope yo'q): `{ ts, online: true }`. Diagnostika uchun.

### 5.9 Idle / offline cleanup (background)

- **Idle timeout (30 min)**: `IdleSessionCleanerService` background — `LastActivityAt + 30min < now` bo'lgan barcha aktiv sessiyalar avtomatik yopiladi (`Timeout` reason). Aktiv jarayonlar avval yakunlanadi va balansdan yechiladi.
- **Device offline (90s)**: `LastSeenAt + 90s < now` bo'lgan qurilmalar `IsOnline=false` qilinadi va ularga ulangan barcha aktiv sessiyalar `DeviceLost` reason bilan yopiladi (qurilmaga stop komandasi yuborilmaydi — u zaten o'lgan). SignalR `SessionUpdated` push + push-notification deep-link bilan.

### 5.10 SignalR
- Hub: `/hubs/session` (JWT shart).
- Grouplar: (a) `sessionToken` — sessiyani kuzatadiganlar (mobile + admin dashboard); (b) `user:{userId}` — auto-join, cross-device push uchun (masalan, `PaymentSucceeded`).
- Eventlar (har biri **server → client**): `DeviceConnected`, `ProcessStarted`, `ProcessUpdated`, `ProcessEnded`, `SessionUpdated`, `SessionClosed`, `PaymentSucceeded`.
- Hub'da hech qanday state-modifying RPC yo'q — barcha buyruqlar REST orqali.

---

## 6) Billing va Payment

Ikkita alohida tushuncha:
- **Billing** — balansni ko'rish va manual to'ldirish (admin tomonidan).
- **Payment** — Payme orqali QR balans to'ldirish (foydalanuvchi o'zi).

### 6.1 Billing — Balans
- `GET /api/UserBalance/GetMyBalance` (UserApi, JWT shart, `[SkipPermissionCheck]`). Customer: `Natural` → `user.Balance`, `Corporate` → `organization.Balance`.
- `POST /api/Balance/TopUp` (BillingApi) — admin uchun: `{ userId, amount }` (`amount > 0`), permission `Balance.TopUp`.
- **Balansdan yechish** (`DeductForProcessAsync`) automatik chaqiriladi process tugaganda. Idempotent (`IsBalanceDeducted` flag). Balansdan ortiq yechmaydi (cost > balans bo'lsa balans 0 ga tushadi va shuncha yechiladi).

### 6.2 Payment — Payme QR top-up
- `POST /api/Payment/QrTopUp` (mobile app):
  ```
  { payeeType: "User" | "Organization", amount, paymeToken, sessionId? }
  ```
  - `Idempotency-Key` header tavsiya etiladi.
  - PayeeType bo'yicha alohida permission: `Payment.TopUpSelf` yoki `Payment.TopUpOrganization`.
  - Server Payme bilan: `receipts.create` → `receipts.pay`. `state=4` (paid) bo'lsa balans to'ldiriladi.
  - `payeeType=Organization` bo'lsa caller Corporate user bo'lishi shart va tashkilot caller'niki bo'ladi.
- Response:
  ```
  { transactionId, status, amount, newBalance, message? }
  ```
  - `status`: `Pending | Succeeded | Failed | Reversed`.
- Muvaffaqiyatda SignalR `PaymentSucceeded` event'i `user:{userId}` group'ga (boshqa device'da ham balans yangilanadi).
- Status kodlari: `402 PaymentRequired` (Payme rad etdi), `502 BadGateway` (Payme bog'lana olmadi), `500` (Payme to'lashdi lekin balans yangilanmadi — manual reconciliation kerak).

### 6.3 Tarix
- **Foydalanuvchi**: `GET /api/Payment/MyTransactions?status=&pageNumber=&pageSize=` — faqat o'zi initiate qilgan.
- **Corporate admin**: `GET /api/Payment/OrganizationTransactions?status=&pageNumber=&pageSize=` — caller Corporate user bo'lishi shart, tashkilot ID URL'da emas, profildan olinadi.

### 6.4 Admin audit
- `GET /api/PaymentAdmin/All?status=&from=&to=&pageNumber=&pageSize=` — barcha to'lovlar ro'yxati.
- `GET /api/PaymentAdmin/ById/{transactionId}` — step'lar bilan.
- `GET /api/PaymentAdmin/Steps/{transactionId}` — to'liq audit step'lar (vaqt bo'yicha): `Created → ReceiptCreated → ReceiptPaid → BalanceUpdated → (Succeeded | Reversed | Failed)`.
- `POST /api/PaymentAdmin/Reverse/{transactionId}` — `{ reason }` shart. Faqat `Succeeded` holatdan. Balansdan ayiriladi, `Status=Reversed`, audit'ga `Reversed` step.

### 6.5 Eski stub (deprecated)
- `POST /api/Payment/create` va `/verify` — placeholder qaytaradi (`qrCode: "generated-payment-qr"`, `paid: true`). Yangi integratsiya `QrTopUp` orqali.

---

## 7) Hisobotlar (Reports)

Hammada bir xil model: davr filtri + paginatsiya yoki Excel eksport. Davr 1 yildan oshmasligi shart (`ArgumentException`).

### 7.1 Foydalanuvchi shaxsiy hisoboti
- `GET /api/Report/MyUsage?from=&to=&pageNumber=&pageSize=` — caller'ning xizmatlardan foydalanish tarixi.
- `GET /api/Report/MyUsageExport?from=&to=` — Excel (.xlsx), butun davr, paginatsiyasiz. Response body'ga **stream** qilinadi (xotirada to'liq buferlanmaydi).
- Permission: `Report.MyUsage`, `Report.MyUsageExport`.
- Har bir satr: sessiya/jarayon ma'lumoti, qurilma, mahsulot, miqdor, narx, sana.

### 7.2 Tashkilot hisoboti (OrgAdmin)
- `GET /api/OrganizationReport/Usage?organizationId=&from=&to=&pageNumber=&pageSize=`.
- `GET /api/OrganizationReport/UsageExport?organizationId=&from=&to=`.
- Tashkilotning **barcha Corporate** xodimlari foydalanishlarini birlashtiradi. Foydalanuvchi ustuni qo'shiladi.
- Permission: `OrganizationReport.Usage`, `OrganizationReport.UsageExport`.

### 7.3 Merchant savdo hisoboti (Merchant admin)
- `GET /api/MerchantReport/Sales?merchantId=&stationId=&from=&to=&pageNumber=&pageSize=` — `stationId` bo'sh bo'lsa merchant'ning barcha station'lari.
- `GET /api/MerchantReport/SalesExport?merchantId=&stationId=&from=&to=`.
- Har bir satr: station, qurilma, mahsulot, foydalanuvchi (anonimizatsiya yo'q), miqdor, summa, sana.
- Permission: `MerchantReport.Sales`, `MerchantReport.SalesExport`.

### 7.4 Umumiy
- Faqat `Ended` jarayonlar va `IsBalanceDeducted=true` bo'lganlari hisoblanadi (haqiqiy savdo).
- Excel eksport: `IAsyncEnumerable` orqali stream, fayl nomi format: `{scope}-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx`.
- Davr xato (`from > to` yoki >1 yil) → 400.

---

## Umumiy kontrakt

- **JSON case**: API javoblari camelCase; SignalR payload'lari **snake_case** (`process_id`, `total_given`); MQTT envelope payload'lari ham snake_case.
- **Vaqt**: Server `DateTime.Now` (lokal), PostgreSQL `timestamp without time zone`. UTC ga o'zgartirish — alohida loyiha qarori.
- **Soft delete**: barcha entity'larda `IsDeleted` global query filter; `DELETE FROM` hech qachon ishlatilmaydi.
- **Idempotency**: `[Idempotent]` filter — `Idempotency-Key` header bilan. Redis'da `idem:{userId}:{action}:{key}` 24h, reservation lock 30s.
- **Permission check**: `PermissionFilter` har so'rovda. `[SkipPermissionCheck]` yoki `[RequirePermission(name)]`. Default: `{Controller}.{Action}` matching.
- **Xato format**: `{ message: "..." }` HTTP status bilan. Validation'lar 400, ownership 403, topilmadi 404, dublikat 409, Payme rad 402, gateway 502.
