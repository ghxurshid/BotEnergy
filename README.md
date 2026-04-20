# BotEnergy Platform

IoT qurilmalarni boshqarish va foydalanuvchilarga xizmat ko'rsatish platformasi. Foydalanuvchilar mobil ilova orqali turli xil qurilmalarga (yoqilg'i dispenseri, avtomoyka, zaryadka stansiyasi va boshqalar) ulanib, real-time rejimda xizmatdan foydalanadi.

---

## Arxitektura

```
                    +------------------+
                    |   Mobile App     |
                    +--------+---------+
                             |
              +--------------+--------------+
              |              |              |
         +----v----+   +-----v-----+  +-----v-----+
         | AuthApi |   |  UserApi  |  | BillingApi|
         | :5002   |   |  :5006   |  |  :5003    |
         +---------+   +----+-----+  +-----------+
                             |
                        SignalR (real-time)
                        RabbitMQ (commands)
                             |
         +-------------------+-------------------+
         |                                       |
   +-----v-----+                          +------v------+
   | AdminApi  |                          |  DeviceApi  |
   |  :5001    |                          |   :5004     |
   +-----------+                          +------+------+
                                                 |
                                            MQTT Bridge
                                                 |
                                          +------v------+
                                          | IoT Devices |
                                          +-------------+
```

### Tech Stack

| Komponent | Texnologiya |
|-----------|-------------|
| Runtime | .NET 8 |
| Database | PostgreSQL |
| ORM | Entity Framework Core |
| Cache & Locks | Redis (StackExchange.Redis) |
| Message Broker | RabbitMQ |
| IoT Protocol | MQTT (MQTTnet) |
| Real-time | SignalR |
| Auth | JWT (Access + Refresh Token) |
| API Docs | Swagger / Swashbuckle |

### Loyiha strukturasi

```
BotEnergy/
  Core/
    Domain/           # Entity, DTO, Interface, Enum, Constants
    Application/      # Service (biznes logika)
  Infrastructure/
    Persistence/      # EF Core DbContext, Repository, Migration
    CommonConfiguration/  # JWT, Redis, RabbitMQ, Filters, Middleware
    External/         # Tashqi integratsiyalar
  WebApi/
    AdminApi/         # Admin panel API (port 5001)
    AuthApi/          # Autentifikatsiya API (port 5002)
    BillingApi/       # Balans va to'lov API (port 5003)
    DeviceApi/        # Qurilma API + MQTT Bridge (port 5004)
    PaymentApi/       # To'lov tizimi API (port 5005)
    UserApi/          # Foydalanuvchi API + SignalR + RabbitMQ Consumer (port 5006)
```

---

## Entity ierarxiyasi

```
MerchantEntity (Sotuvchi kompaniya)
  └── StationEntity (Stansiya/filial)
        └── DeviceEntity (IoT qurilma)
              └── ProductEntity (Mahsulot/xizmat)

OrganizationEntity (Iste'molchi tashkilot)
  └── LegalUserEntity (Yuridik foydalanuvchi)

UserEntity (abstract)
  ├── NaturalUserEntity   (Jismoniy shaxs — o'z balansi bor)
  ├── LegalUserEntity     (Yuridik shaxs — Organization balansi)
  └── MerchantUserEntity  (Merchant xodimi — Station ga biriktirilgan)

RoleEntity
  └── RolePermissionEntity → PermissionEntity

UsageSessionEntity (Xizmat sessiyasi)
  ├── → UserEntity
  ├── → DeviceEntity
  └── → ProductEntity
```

### Soft Delete

Barcha entity larda `IsDeleted` maydoni mavjud. EF Core Global Query Filter orqali `WHERE IsDeleted = false` avtomatik qo'shiladi. Hech qayerda fizik o'chirish (DELETE) ishlatilmaydi.

---

## 1. AuthApi — Autentifikatsiya

**Port:** `5002` | **Full Base URL:** `http://localhost:5002/api/Auth`
**Authorize:** Yo'q (barcha endpoint lar ochiq)

### 1.1 Ro'yxatdan o'tish (4 qadamli flow)

#### Qadam 1: Register

```
POST /api/Auth/Register
```

**Request:**
```json
{
  "phoneId": "device-uuid-123",
  "phoneNumber": "+998901234567",
  "mail": "user@example.com"
}
```

**Logika:**
1. Telefon raqam bo'yicha user qidiriladi
2. Agar topilsa va `IsVerified=true` → "Siz allaqachon ro'yxatdan o'tgansiz, login qiling"
3. Agar topilsa va `IsOtpVerified=true` → "OTP tasdiqlangan, parol o'rnating"
4. Agar topilsa, lekin OTP tasdiqlanmagan → yangi OTP generatsiya qilinadi
5. Agar yangi user → `NaturalUserEntity` yaratiladi, OTP yuboriladi

**Response (201):**
```json
{
  "isSuccess": true,
  "result": {
    "userId": 1,
    "resultMessage": "Ro'yxatdan o'tish boshlandi. OTP kod yuborildi."
  }
}
```

> OTP test rejimda konsolga chiqariladi. Test OTP kodi: `123456` (har doim qabul qilinadi).

---

#### Qadam 2: Verify OTP

```
POST /api/Auth/Verify
```

**Request:**
```json
{
  "userId": 1,
  "otpCode": "123456"
}
```

**Logika:**
1. User ID bo'yicha topiladi
2. Agar allaqachon `IsOtpVerified=true` → "OTP allaqachon tasdiqlangan"
3. OTP tekshiriladi (`123456` test kodi har doim ishlaydi)
4. `IsOtpVerified = true` ga o'zgartiriladi

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "resultMessage": "OTP tasdiqlandi. Endi parol o'rnating."
  }
}
```

---

#### Qadam 3: Set Password

```
POST /api/Auth/SetPassword
```

**Request:**
```json
{
  "userId": 1,
  "password": "MySecurePass123"
}
```

**Logika:**
1. User topiladi, `IsOtpVerified=true` va `IsVerified=false` bo'lishi shart
2. Parol hash + salt yaratiladi (`PasswordHelper.CreatePassword`)
3. `IsVerified = true` ga o'zgartiriladi
4. JWT Access Token (15 daqiqa) va Refresh Token (7 kun) generatsiya qilinadi
5. Refresh token Redis da saqlanadi (key: `refresh:{token}`, value: userId, TTL: 7 kun)

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "accessTokenExpiration": "2026-04-20T13:15:00"
  }
}
```

---

#### Qadam 4: Login

```
POST /api/Auth/Login
```

**Request:**
```json
{
  "phoneNumber": "+998901234567",
  "password": "MySecurePass123"
}
```

**Logika:**
1. Telefon raqam bo'yicha user qidiriladi (global filter: `IsDeleted=false`)
2. `PasswordHash` mavjudligi tekshiriladi (yo'q bo'lsa — ro'yxatdan to'liq o'tmagan)
3. Parol hash tekshiriladi
4. `IsVerified=true` va `IsBlocked=false` bo'lishi shart
5. `LastLoginDate` va `LastActiveDate` yangilanadi
6. Yangi token juftligi generatsiya qilinadi

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "accessTokenExpiration": "2026-04-20T13:15:00"
  }
}
```

**Xato holatlari:**
| Kod | Xabar | Sabab |
|-----|-------|-------|
| 400 | "Parol noto'g'ri" | Hash mos kelmadi |
| 400 | "Foydalanuvchi bloklangan" | `IsBlocked=true` |
| 400 | "Ro'yxatdan o'tish tugallanmagan" | `IsVerified=false` |
| 404 | "Foydalanuvchi topilmadi" | Telefon raqam bazada yo'q yoki `IsDeleted=true` |

---

### 1.2 Token yangilash

```
POST /api/Auth/RefreshToken
```

**Request:**
```json
{
  "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Logika (Token Rotation):**
1. Redis dan `refresh:{token}` kaliti bo'yicha `userId` olinadi
2. Agar topilmasa → "Token yaroqsiz yoki muddati tugagan"
3. User topiladi: `IsBlocked=false`, `IsDeleted=false` tekshiriladi
4. **Eski token revoke qilinadi** (Redis dan o'chiriladi)
5. **Yangi token juftligi** generatsiya qilinadi (rotation)

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "resultMessage": "Token muvaffaqiyatli yangilandi.",
    "accessToken": "eyJhbGciOiJIUzI1NiIs...(yangi)",
    "refreshToken": "new-refresh-token-uuid",
    "accessTokenExpiration": "2026-04-20T13:15:00"
  }
}
```

> Har bir refresh token faqat **bir marta** ishlatilishi mumkin. Qayta ishlatishga urinish 400 xato qaytaradi.

---

### 1.3 Parolni tiklash (3 qadamli flow)

#### Qadam 1: So'rov

```
POST /api/Auth/ResetPasswordRequest
```

**Request:**
```json
{
  "phoneNumber": "+998901234567"
}
```

**Logika:**
1. User topiladi va `IsVerified=true` bo'lishi shart
2. OTP generatsiya qilinadi (`OtpPurpose.ResetPassword`)

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "userId": 1,
    "resultMessage": "OTP kod yuborildi."
  }
}
```

#### Qadam 2: OTP tasdiqlash

```
POST /api/Auth/ResetPasswordVerify
```

**Request:**
```json
{
  "userId": 1,
  "otpCode": "123456"
}
```

#### Qadam 3: Yangi parol o'rnatish

```
POST /api/Auth/ResetPasswordSet
```

**Request:**
```json
{
  "userId": 1,
  "newPassword": "NewSecurePass456"
}
```

**Logika:**
1. OTP tasdiqlangan bo'lishi shart (`IsOtpVerifiedAsync`)
2. Yangi parol hash + salt yaratiladi
3. OTP verification consume qilinadi (bir martalik)

---

### JWT Token tuzilishi

Access Token (15 daqiqa) ichidagi claim lar:

```json
{
  "nameid": "1",
  "unique_name": "+998901234567",
  "Permission": [
    "StationAdmin.Create",
    "DeviceAdmin.Register",
    "Session.Create"
  ],
  "exp": 1745150100
}
```

| Claim | Tavsif |
|-------|--------|
| `nameid` | User ID |
| `unique_name` | Telefon raqam |
| `Permission` | User role ga tegishli barcha permission lar (bir nechta bo'lishi mumkin) |

---

## 2. AdminApi — Boshqaruv paneli

**Port:** `5001` | **Full Base URL:** `http://localhost:5001/api/{Controller}/{Action}`
**Authorize:** Barcha endpoint lar JWT talab qiladi
**Permission:** Har bir endpoint alohida permission talab qiladi

### Permission tekshiruv mexanizmi

`PermissionFilter` (IAsyncActionFilter) har bir so'rovda ishlaydi:
1. `[SkipPermissionCheck]` atributi bormi → o'tkazib yuboradi
2. JWT dan `Permission` claim lari ajratiladi
3. `[RequirePermission("...")]` yoki standart `{Controller}.{Action}` pattern bo'yicha tekshiriladi
4. Permission yo'q bo'lsa → `403 Forbidden`

---

### 2.1 Merchant boshqaruvi

**Merchant** — bu sotuvchi kompaniya (yoqilg'i kompaniyasi, avtomoyka tarmog'i va h.k.)

#### Create

```
POST /api/Merchant/Register
Permission: MerchantAdmin.Register
```

**Request:**
```json
{
  "phoneNumber": "+998901112233",
  "inn": "123456789",
  "bankAccount": "20208000123456789001",
  "companyName": "EcoFuel LLC",
  "isActive": true
}
```

**Logika:**
1. Telefon raqam bo'yicha dublikat tekshiriladi → 409 Conflict
2. `MerchantEntity` yaratiladi

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "id": 1,
    "resultMessage": "Merchant muvaffaqiyatli ro'yxatdan o'tkazildi."
  }
}
```

#### GetAll

```
GET /api/Merchant/GetAll
Permission: MerchantAdmin.GetAll
```

**Response:**
```json
{
  "isSuccess": true,
  "result": [
    {
      "id": 1,
      "phoneNumber": "+998901112233",
      "inn": "123456789",
      "bankAccount": "20208000123456789001",
      "companyName": "EcoFuel LLC",
      "isActive": true,
      "createdDate": "2026-04-20T10:00:00"
    }
  ]
}
```

#### GetById

```
GET /api/Merchant/{id}
Permission: MerchantAdmin.GetById
```

#### Update

```
PUT /api/Merchant/{id}
Permission: MerchantAdmin.Update
```

**Request:**
```json
{
  "phoneNumber": "+998909998877"
}
```

> Faqat `PhoneNumber` yangilanadi. `Inn`, `BankAccount`, `CompanyName` — o'zgarmas.

#### Delete (Soft)

```
DELETE /api/Merchant/{id}
Permission: MerchantAdmin.Delete
```

**Logika:** `IsDeleted=true`, `IsActive=false` ga o'zgartiriladi. DB dan fizik o'chirilmaydi.

---

### 2.2 Station boshqaruvi

**Station** — Merchant ga tegishli filial/punkt. Ierarxiya: `Merchant → Station → Device → Product`

#### Create

```
POST /api/Station/Create
Permission: StationAdmin.Create
```

**Request:**
```json
{
  "name": "Yunusobod filiali",
  "location": "Toshkent, Yunusobod tumani",
  "merchantId": 1
}
```

**Logika:**
1. `Merchant` topiladi (global filter: `IsDeleted=false`)
2. **Merchant faolligi** tekshiriladi (`IsActive=true` bo'lishi shart)
3. **Access control:**
   - `MerchantAdmin.Register` permission bor → cheklov yo'q
   - `MerchantUserEntity` → faqat o'z merchant iga tegishli station yaratishi mumkin
   - Boshqa user type → 403

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "id": 1,
    "resultMessage": "Stansiya muvaffaqiyatli yaratildi."
  }
}
```

**Xato holatlari:**
| Kod | Xabar | Sabab |
|-----|-------|-------|
| 404 | "Merchant topilmadi" | ID noto'g'ri yoki o'chirilgan |
| 400 | "Merchant faol emas" | `IsActive=false` |
| 403 | "Faqat o'z merchantingizga..." | Caller boshqa merchant ga tegishli |

#### GetAll / GetById / GetByMerchant

```
GET /api/Station/GetAll            → Permission: StationAdmin.GetAll
GET /api/Station/{id}              → Permission: StationAdmin.GetById
GET /api/Station/GetByMerchant/by-merchant/{merchantId} → Permission: StationAdmin.GetByMerchant
```

**GetAll/GetByMerchant Response:**
```json
{
  "isSuccess": true,
  "result": [
    {
      "id": 1,
      "name": "Yunusobod filiali",
      "location": "Toshkent, Yunusobod tumani",
      "merchantId": 1,
      "merchantName": "EcoFuel LLC",
      "isActive": true,
      "createdDate": "2026-04-20T10:00:00"
    }
  ]
}
```

#### Update / Delete

```
PUT /api/Station/{id}     → Permission: StationAdmin.Update
DELETE /api/Station/{id}  → Permission: StationAdmin.Delete
```

**Update Request:**
```json
{
  "name": "Yangi nom",
  "location": "Yangi manzil",
  "isActive": false
}
```

---

### 2.3 Device boshqaruvi

**Device** — Station ga o'rnatilgan IoT qurilma (yoqilg'i dispenseri, moyka mashina, zaryadka va h.k.)

#### Register

```
POST /api/Device/Register
Permission: DeviceAdmin.Register
```

**Request:**
```json
{
  "serialNumber": "WASH-001-2026",
  "deviceType": "WASH_BOX",
  "stationId": 1,
  "model": "WashMaster 3000",
  "firmwareVersion": "2.1.0",
  "isOnline": false,
  "isActive": true
}
```

**DeviceType enum qiymatlari:**

| Qiymat | Tavsif |
|--------|--------|
| `FUEL_DISPENSER` | Yoqilg'i dispenseri (benzin, dizel, metan, propan) |
| `WASH_BOX` | Avtomoyka |
| `CHARGER` | Elektr zaryadka stansiyasi |
| `WATER_DISPENSER` | Suv avtomati |
| `VACUUM_CLEANER` | Avto changyutgich |
| `VENDING_MACHINE` | Vending mashina |

**Logika:**
1. Station topiladi va **faolligi** tekshiriladi
2. **Access control:**
   - `MerchantAdmin.Register` permission → cheklov yo'q
   - `MerchantUserEntity` → station o'z merchant iga tegishli bo'lishi shart
   - `LegalUserEntity` → station o'z organization iga tegishli bo'lishi shart
3. `SerialNumber` unikalligi tekshiriladi → 409 agar mavjud
4. `SecretKey` avtomatik generatsiya qilinadi (qurilma autentifikatsiyasi uchun)

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "id": 1,
    "resultMessage": "Qurilma muvaffaqiyatli ro'yxatdan o'tkazildi."
  }
}
```

#### GetAll / GetById / GetByStation

```
GET /api/Device/GetAll           → Permission: DeviceAdmin.GetAll
GET /api/Device/{id}             → Permission: DeviceAdmin.GetById
GET /api/Device/GetByStation/by-station/{stationId} → Permission: DeviceAdmin.GetByStation
```

**Item Response:**
```json
{
  "id": 1,
  "serialNumber": "WASH-001-2026",
  "deviceType": "WASH_BOX",
  "model": "WashMaster 3000",
  "firmwareVersion": "2.1.0",
  "stationId": 1,
  "stationName": "Yunusobod filiali",
  "isOnline": true,
  "isActive": true,
  "createdDate": "2026-04-20T10:00:00"
}
```

#### Update / Delete

```
PUT /api/Device/{id}     → Permission: DeviceAdmin.Update
DELETE /api/Device/{id}  → Permission: DeviceAdmin.Delete
```

**Update Request:**
```json
{
  "model": "WashMaster 3000 Pro",
  "firmwareVersion": "2.2.0",
  "isOnline": true,
  "isActive": true
}
```

---

### 2.4 Product boshqaruvi

**Product** — Device ga biriktirilgan mahsulot yoki xizmat turi.

#### Ruxsat etilgan product turlarini olish

```
GET /api/Product/GetAllowedTypes?deviceType=WASH_BOX
Permission: ProductAdmin.GetAllowedTypes
```

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "deviceType": "WASH_BOX",
    "allowedProductTypes": ["Water", "Foam", "Wax"]
  }
}
```

**DeviceType → ProductType mapping:**

| DeviceType | Ruxsat etilgan ProductType lar |
|------------|-------------------------------|
| `FUEL_DISPENSER` | Petrol, Diesel, Methane, Propane |
| `WASH_BOX` | Water, Foam, Wax |
| `CHARGER` | Electricity |
| `WATER_DISPENSER` | PurifiedWater, ColdWater, HotWater |
| `VACUUM_CLEANER` | VacuumService |
| `VENDING_MACHINE` | Coffee, Tea, ColdDrink, Snack |

#### Create

```
POST /api/Product/Create
Permission: ProductAdmin.Create
```

**Request:**
```json
{
  "name": "Yuqori bosimli suv",
  "description": "Yuqori bosimli suv bilan yuvish",
  "productType": "Water",
  "unit": "Second",
  "price": 50.00,
  "deviceId": 1,
  "isActive": true
}
```

**UnitType enum:**

| Qiymat | Tavsif |
|--------|--------|
| `Liter` | Litr (yoqilg'i, suv) |
| `CubicMeter` | Kub metr (gaz) |
| `KilowattHour` | kWh (elektr) |
| `Second` | Soniya (moyka, changyutgich) |
| `Piece` | Dona (vending) |

**Logika:**
1. Device topiladi (global filter + `IsActive=true` tekshiruvi)
2. `ProductType` device type ga mos kelishi tekshiriladi (`DeviceTypeProductMap`)
3. **Access control** (station orqali Merchant/Organization tekshiruvi)
4. Product yaratiladi

#### GetAll / GetById / GetByDevice

```
GET /api/Product/GetAll                    → Permission: ProductAdmin.GetAll
GET /api/Product/{id}                      → Permission: ProductAdmin.GetById
GET /api/Product/GetByDevice/by-device/{deviceId} → Permission: ProductAdmin.GetByDevice
```

**Item Response:**
```json
{
  "id": 1,
  "name": "Yuqori bosimli suv",
  "description": "Yuqori bosimli suv bilan yuvish",
  "type": "Water",
  "unit": "Second",
  "price": 50.00,
  "isActive": true,
  "deviceId": 1,
  "deviceSerialNumber": "WASH-001-2026",
  "createdDate": "2026-04-20T10:00:00"
}
```

#### Update / Delete

```
PUT /api/Product/{id}     → Permission: ProductAdmin.Update
DELETE /api/Product/{id}  → Permission: ProductAdmin.Delete
```

---

### 2.5 Organization boshqaruvi

**Organization** — iste'molchi tashkilot. LegalUser lar shu tashkilotga tegishli bo'ladi va tashkilot balansi orqali xizmatlardan foydalanadi.

#### Create

```
POST /api/Organization/Create
Permission: OrganizationAdmin.Create
```

**Request:**
```json
{
  "name": "TechCorp LLC",
  "inn": "987654321",
  "address": "Toshkent, Mirzo Ulug'bek tumani",
  "phoneNumber": "+998712345678",
  "balance": 1000000.00,
  "isActive": true
}
```

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "id": 1,
    "resultMessage": "Tashkilot muvaffaqiyatli yaratildi."
  }
}
```

#### GetAll / GetById

```
GET /api/Organization/GetAll  → Permission: OrganizationAdmin.GetAll
GET /api/Organization/{id}    → Permission: OrganizationAdmin.GetById
```

**Item Response:**
```json
{
  "id": 1,
  "name": "TechCorp LLC",
  "inn": "987654321",
  "address": "Toshkent, Mirzo Ulug'bek tumani",
  "phoneNumber": "+998712345678",
  "balance": 1000000.00,
  "isActive": true,
  "createdDate": "2026-04-20T10:00:00"
}
```

#### Update

```
PUT /api/Organization/{id}
Permission: OrganizationAdmin.Update
```

**Request:**
```json
{
  "address": "Yangi manzil",
  "phoneNumber": "+998711112233",
  "isActive": true
}
```

> `Name` va `Inn` o'zgarmas — faqat `Address`, `PhoneNumber`, `IsActive` yangilanadi.

#### Delete

```
DELETE /api/Organization/{id}
Permission: OrganizationAdmin.Delete
```

---

### 2.6 User boshqaruvi (Admin)

Admin tomonidan `LegalUserEntity` yoki `MerchantUserEntity` yaratish.

> `NaturalUserEntity` faqat o'zi ro'yxatdan o'tadi (AuthApi orqali).

#### Create

```
POST /api/User/Create
Permission: UserAdmin.Create
```

**LegalUser yaratish (Organization ga):**
```json
{
  "phoneId": "device-uuid-456",
  "mail": "employee@techcorp.uz",
  "phoneNumber": "+998901234568",
  "roleId": 2,
  "organizationId": 1
}
```

**MerchantUser yaratish (Station ga):**
```json
{
  "phoneId": "device-uuid-789",
  "mail": "operator@ecofuel.uz",
  "phoneNumber": "+998901234569",
  "roleId": 3,
  "stationId": 1
}
```

**Logika:**
1. Telefon raqam unikalligi → 409
2. Role mavjudligi → 404
3. Agar `OrganizationId` va `StationId` ikkalasi ham berilsa → `StationId = null` (Organization ustunlik qiladi)
4. **OrganizationId berilsa:**
   - Organization topiladi va `IsActive` tekshiriladi
   - `OrganizationAdmin.Create` permission yo'q bo'lsa, caller faqat o'z organization iga qo'sha oladi
   - `LegalUserEntity` yaratiladi
5. **StationId berilsa:**
   - Station topiladi va `IsActive` tekshiriladi
   - Station `MerchantId` bo'lishi shart
   - `MerchantAdmin.Register` permission yo'q bo'lsa, caller faqat o'z merchant iga qo'sha oladi
   - `MerchantUserEntity` yaratiladi
6. User `IsOtpVerified=true`, `IsVerified=false` holda yaratiladi (parol hali o'rnatilmagan)

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "id": 5,
    "resultMessage": "Foydalanuvchi muvaffaqiyatli yaratildi."
  }
}
```

#### Set Password / Reset Password

```
PUT /api/User/{id}/SetPassword      → Permission: UserAdmin.SetPassword
PUT /api/User/{id}/ResetPassword    → Permission: UserAdmin.ResetPassword
```

**SetPassword Request:**
```json
{
  "password": "SecurePass123"
}
```

**Logika:** `IsOtpVerified=true` va `IsVerified=false` bo'lishi shart. Parol o'rnatilgach `IsVerified=true`.

**ResetPassword Request:**
```json
{
  "newPassword": "NewSecurePass456"
}
```

**Logika:** `IsVerified=true` bo'lishi shart (allaqachon ro'yxatdan o'tgan).

#### Block / Unblock / Delete

```
PUT /api/User/{id}/Block      → Permission: UserAdmin.Block
PUT /api/User/{id}/Unblock    → Permission: UserAdmin.Unblock
DELETE /api/User/{id}         → Permission: UserAdmin.Delete
```

**Block qilingan user:**
- Login qila olmaydi
- Refresh token ishlamaydi
- Sessiya yaratolmaydi

#### GetAll / GetById

```
GET /api/User/GetAll   → Permission: UserAdmin.GetAll
GET /api/User/{id}     → Permission: UserAdmin.GetById
```

**Item Response:**
```json
{
  "id": 5,
  "phoneNumber": "+998901234568",
  "mail": "employee@techcorp.uz",
  "userType": "LegalEntity",
  "isVerified": true,
  "isBlocked": false,
  "roleId": 2,
  "roleName": "Station Operator",
  "balance": 1000000.00,
  "createdDate": "2026-04-20T10:00:00",
  "lastLoginDate": "2026-04-20T12:00:00"
}
```

> `balance` — NaturalUser uchun o'z balansi, LegalUser uchun Organization balansi qaytariladi.

---

### 2.7 Role va Permission boshqaruvi

#### Ruxsat etilgan permission larni olish

```
GET /api/Role/GetAllowedPermissions
Permission: Role.GetAllowedPermissions
```

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "permissions": [
      { "id": 1, "name": "StationAdmin.Create" },
      { "id": 2, "name": "DeviceAdmin.Register" },
      { "id": 3, "name": "Session.Create" }
    ]
  }
}
```

#### Create Role

```
POST /api/Role/CreateRole
Permission: Role.CreateRole
```

**Request:**
```json
{
  "name": "Station Operator",
  "description": "Stansiya operatori — qurilma va mahsulotlarni boshqaradi",
  "isActive": true,
  "permissionIds": [1, 2, 5, 10]
}
```

**Logika:**
1. Role yaratiladi
2. `PermissionIds` berilsa — har bir ID mavjudligi tekshiriladi
3. Mavjud permission lar `RolePermissionEntity` orqali bog'lanadi

#### Update Role (Permission lar bilan)

```
PUT /api/Role/{id}
Permission: Role.Update
```

**Request:**
```json
{
  "name": "Senior Operator",
  "isActive": true,
  "permissionIds": [1, 2, 5, 10, 15, 20]
}
```

**Logika:**
1. Mavjud role permission lari olinadi
2. Yangi ro'yxatda yo'q bo'lganlar `IsDeleted=true` ga o'zgartiriladi
3. Yangi qo'shilganlar `RolePermissionEntity` sifatida yaratiladi

#### GetAll / GetById / GetPermissions / Delete

```
GET /api/Role/GetAll              → Permission: Role.GetAll
GET /api/Role/{id}                → Permission: Role.GetById
GET /api/Role/{roleId}/Permissions → Permission: Role.GetPermissions
DELETE /api/Role/{id}             → Permission: Role.Delete
```

---

## 3. UserApi — Foydalanuvchi ilovasi

**Port:** `5006` | **Full Base URL:** `http://localhost:5006/api/{Controller}/{Action}`
**Authorize:** JWT talab qilinadi

### 3.1 Profil boshqaruvi

#### Mening profilim

```
GET /api/User/Me
Permission: Yo'q (SkipPermissionCheck)
```

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "id": 1,
    "phoneId": "device-uuid-123",
    "mail": "user@example.com",
    "phoneNumber": "+998901234567",
    "balance": 50000.00,
    "isVerified": true,
    "isBlocked": false,
    "lastLoginDate": "2026-04-20T12:00:00",
    "lastActiveDate": "2026-04-20T12:30:00",
    "createdDate": "2026-04-15T09:00:00"
  }
}
```

#### Profilni yangilash

```
PUT /api/User/UpdateMe
Permission: Yo'q (SkipPermissionCheck)
```

**Request:**
```json
{
  "mail": "newemail@example.com",
  "phoneId": "new-device-uuid"
}
```

---

### 3.2 Qurilmaga ulanish

#### Qurilma mahsulotlarini ko'rish

```
GET /api/DeviceConnection/GetProducts/{serialNumber}
Permission: Yo'q (SkipPermissionCheck)
```

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "deviceType": "WASH_BOX",
    "products": [
      {
        "id": 1,
        "name": "Yuqori bosimli suv",
        "type": "Water",
        "unit": "Second",
        "price": 50.00
      },
      {
        "id": 2,
        "name": "Ko'pik",
        "type": "Foam",
        "unit": "Second",
        "price": 80.00
      }
    ]
  }
}
```

---

### 3.3 Sessiya boshqaruvi (Asosiy biznes flow)

Bu bo'lim platformaning eng muhim qismi — foydalanuvchi IoT qurilmadan xizmat olish jarayoni.

#### To'liq sessiya flow diagrammasi

```
  User (Mobile App)                    Server                     Device (IoT)
       |                                |                            |
  1.   |--- POST /Session/Create ------>|                            |
       |<-- sessionToken (QR) ---------|                            |
       |                                |                            |
  2.   |    [User QR ko'rsatadi]        |                            |
       |                                |<--- MQTT session/connected-|
       |                                |     (device QR skanerladi) |
       |<-- SignalR: DeviceConnected --|                            |
       |    (product info, price)       |                            |
       |                                |                            |
  3.   |--- POST /Session/SetQuantity ->|                            |
       |    (requestedQuantity)         |-- Redis: lock device ----->|
       |                                |-- RabbitMQ: Start -------->|
       |                                |     --> MQTT command/start |
       |<-- limitQuantity, price -------|                            |
       |                                |                            |
  4.   |                                |<--- MQTT telemetry --------|
       |<-- SignalR: ProgressUpdate ---|     (quantity += N)        |
       |    (delivered so far)          |                            |
       |                                |<--- MQTT telemetry --------|
       |<-- SignalR: ProgressUpdate ---|                            |
       |                                |                            |
  5a.  |--- POST /Session/Close ------->|                            |
       |    (user to'xtatdi)            |-- balance -= cost -------->|
       |<-- SignalR: SessionClosed -----|                            |
       |                                |                            |
  5b.  |                                |<--- MQTT session/completed-|
       |                                |     (device o'zi to'xtadi) |
       |                                |-- balance -= cost -------->|
       |<-- SignalR: SessionCompleted --|                            |
```

#### Qadam 1: Sessiya yaratish

```
POST /api/Session/Create
Permission: Session.Create
```

**Request:** `{}` (bo'sh body — userId JWT dan olinadi)

**Logika:**
1. JWT dan `userId` olinadi
2. User topiladi (global filter: `IsDeleted=false`)
3. **`IsBlocked` tekshiriladi** → 403 agar bloklangan
4. `SessionToken` generatsiya qilinadi (GUID, QR kod uchun)
5. Sessiya `Pending` holatida yaratiladi
6. 30 daqiqa muddat beriladi (idle timeout)

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "sessionId": 1,
    "sessionToken": "a1b2c3d4e5f67890abcdef1234567890",
    "expiresAt": "2026-04-20T13:30:00",
    "message": "Sessiya yaratildi. Qurilma QR kodni skanerlab ulanishini kuting."
  }
}
```

> `sessionToken` — QR kod sifatida mobil ilovada ko'rsatiladi.

#### Qadam 2: Qurilma ulanishi (Avtomatik — MQTT orqali)

Qurilma QR kodni skanerlaydi va MQTT orqali server ga signal yuboradi:

```
MQTT Topic: station/{serialNumber}/session/connected
Payload: { "session_token": "...", "device_token": "..." }
```

**Server logikasi (MqttBridge → RabbitMQ → DeviceEventConsumer → SessionService):**

1. `device_token` orqali qurilma autentifikatsiyasi tekshiriladi
2. Sessiya `sessionToken` bo'yicha topiladi
3. Status `Pending` bo'lishi shart
4. Qurilmaning birinchi aktiv `Product` i sessiyaga yoziladi
5. Status → `DeviceConnected`
6. SignalR orqali foydalanuvchiga xabar yuboriladi:

**SignalR Event: `DeviceConnected`**
```json
{
  "device_id": 1,
  "serial_number": "WASH-001-2026",
  "product_id": 1,
  "product_name": "Yuqori bosimli suv",
  "unit": "Second",
  "price_per_unit": 50.00,
  "connected_at": "2026-04-20T13:01:30"
}
```

#### Qadam 3: Miqdor belgilash va start

```
POST /api/Session/SetQuantity
Permission: Session.SetQuantity
```

**Request:**
```json
{
  "sessionId": 1,
  "requestedQuantity": 100
}
```

> `requestedQuantity` ixtiyoriy. Berilmasa — balansdagi maksimal miqdor hisoblanadi.

**Logika:**
1. Sessiya topiladi, status `DeviceConnected` bo'lishi shart
2. User balansi tekshiriladi:
   - `NaturalUserEntity` → `natural.Balance`
   - `LegalUserEntity` → `legal.Organization.Balance`
3. `maxQuantity = balance / pricePerUnit`
4. `limit = min(requestedQuantity, maxQuantity)`
5. Agar `limit <= 0` → "Balans yetarli emas"
6. Status → `InProgress`
7. **Redis: qurilma qulflanadi** — `device:lock:{serialNumber} = userId` (30 daqiqa TTL)
8. **RabbitMQ → DeviceApi → MQTT:** `Start` buyrug'i qurilmaga yuboriladi

```
MQTT Topic: station/{serialNumber}/command/start
Payload: { "product_id": 1, "amount": 100.0 }
```

**Response (200):**
```json
{
  "isSuccess": true,
  "result": {
    "limitQuantity": 100.0,
    "productName": "Yuqori bosimli suv",
    "unit": "Second",
    "pricePerUnit": 50.00,
    "deviceSerialNumber": "WASH-001-2026",
    "productId": 1,
    "message": "Miqdor belgilandi. Qurilmaga start buyrug'i yuborildi."
  }
}
```

#### Qadam 4: Real-time progress (Avtomatik)

Qurilma ishlash jarayonida telemetry ma'lumot yuboradi:

```
MQTT Topic: station/{serialNumber}/telemetry
Payload: { "session_token": "...", "device_token": "...", "quantity": 5.0 }
```

Har bir telemetry kelganda `DeliveredQuantity += quantity` yangilanadi va SignalR orqali:

**SignalR Event: `ProgressUpdate`**
```json
{
  "quantity": 5.0,
  "total_quantity": 25.0,
  "product_id": 1,
  "unit": "Second",
  "price_per_unit": 50.00,
  "current_cost": 1250.00
}
```

#### Qadam 5a: Foydalanuvchi to'xtatishi

```
POST /api/Session/Close
Permission: Session.Close
```

**Request:**
```json
{
  "sessionId": 1
}
```

**Logika:**
1. Sessiya topiladi, caller ga tegishli bo'lishi shart
2. Status → `ClosedByUser`, EndReason → "closed_by_user"
3. **Balans yechiladi:** `cost = DeliveredQuantity * PricePerUnit`
4. SignalR: `SessionClosed` event

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "message": "Sessiya muvaffaqiyatli yopildi.",
    "totalDelivered": 25.0
  }
}
```

#### Qadam 5b: Qurilma o'zi tugatishi (Avtomatik)

```
MQTT Topic: station/{serialNumber}/session/completed
Payload: { "session_token": "...", "final_quantity": 100.0, "end_reason": "limit_reached" }
```

**Server logikasi:**
1. Status → `Completed`
2. `DeliveredQuantity = finalQuantity`
3. Balans yechiladi
4. SignalR: `SessionCompleted`

#### SignalR real-time control (SessionHub)

Foydalanuvchi sessiya davomida qo'shimcha buyruqlar yuborishi mumkin:

```javascript
// SignalR connection
connection.invoke("JoinSession", sessionToken);
connection.invoke("PauseSession", deviceSerialNumber);  // → MQTT pause
connection.invoke("ResumeSession", deviceSerialNumber); // → MQTT resume
connection.invoke("StopSession", deviceSerialNumber);   // → MQTT stop
connection.invoke("LeaveSession", sessionToken);
```

#### Sessiya timeout (Avtomatik)

`CloseTimedOutSessionsAsync()` — background job orqali 30 daqiqa harakatsiz sessiyalar avtomatik yopiladi:
- Status → `TimedOut`
- EndReason → "timed_out"
- Balans yechiladi

#### Session Status lari

```
Pending → DeviceConnected → InProgress → Completed
                                       → ClosedByUser
                                       → TimedOut
```

| Status | Tavsif |
|--------|--------|
| `Pending` | Sessiya yaratildi, qurilma hali ulanmadi |
| `DeviceConnected` | Qurilma QR skanerladi, product tanlandi |
| `InProgress` | Miqdor belgilandi, qurilma ishlayapti |
| `Completed` | Qurilma o'zi tugatdi (limit yetdi yoki boshqa sabab) |
| `ClosedByUser` | Foydalanuvchi o'zi to'xtatdi |
| `TimedOut` | 30 daqiqa harakatsizlik, avtomatik yopildi |

---

## 4. BillingApi — Balans boshqaruvi

**Port:** `5003` | **Full Base URL:** `http://localhost:5003/api/Balance`

### Balansni ko'rish

```
GET /api/Balance/GetMyBalance
Permission: Yo'q (SkipPermissionCheck)
Authorize: JWT
```

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "userId": 1,
    "balance": 50000.00,
    "currency": "UZS"
  }
}
```

> NaturalUser → o'z balansi, LegalUser → Organization balansi.

### Balansni to'ldirish

```
POST /api/Balance/TopUp
Permission: Balance.TopUp
```

**Request:**
```json
{
  "userId": 1,
  "amount": 100000.00
}
```

**Logika:**
1. `Amount > 0` tekshiriladi
2. User topiladi
3. NaturalUser → `Balance += Amount`
4. LegalUser → `Organization.Balance += Amount`

**Response:**
```json
{
  "isSuccess": true,
  "result": {
    "newBalance": 150000.00,
    "resultMessage": "Balans 100,000.00 so'mga to'ldirildi."
  }
}
```

---

## 5. DeviceApi — Qurilma integratsiyasi

**Port:** `5004` | **Full Base URL:** `http://localhost:5004/api/DeviceAuth`
**Authorize:** Yo'q (qurilma o'zi autentifikatsiya qiladi)

### Qurilma autentifikatsiyasi

```
POST /api/DeviceAuth/Authenticate
```

**Request:**
```json
{
  "serialNumber": "WASH-001-2026"
}
```

**Logika:**
1. SerialNumber bo'yicha qurilma topiladi (`IsActive=true` bo'lishi shart)
2. `SecretKey` device token sifatida qaytariladi
3. `mqttTopicPrefix` — qurilma MQTT da shu topic ga subscribe bo'ladi

**Response:**
```json
{
  "deviceToken": "abc123secretkey",
  "serialNumber": "WASH-001-2026",
  "mqttTopicPrefix": "station/WASH-001-2026"
}
```

### MQTT Topic strukturasi

Qurilma quyidagi topic larga subscribe bo'ladi:

| Topic | Yo'nalish | Tavsif |
|-------|-----------|--------|
| `station/{serial}/command/start` | Server → Device | Ishni boshlash (product_id, amount) |
| `station/{serial}/command/pause` | Server → Device | Pauza |
| `station/{serial}/command/resume` | Server → Device | Davom ettirish |
| `station/{serial}/command/stop` | Server → Device | To'xtatish |
| `station/{serial}/session/connected` | Device → Server | QR skanerlandi |
| `station/{serial}/telemetry` | Device → Server | Real-time progress |
| `station/{serial}/session/completed` | Device → Server | Ish tugadi |
| `station/{serial}/status` | Device → Server | Qurilma holati |

---

## 6. PaymentApi — To'lov tizimi

**Port:** `5005` | **Full Base URL:** `http://localhost:5005/api/Payment`

> **Status: STUB** — hozircha placeholder endpoint lar. Payme integratsiyasi rejalashtirilgan.

```
POST /api/Payment/create   → { "qrCode": "..." }
POST /api/Payment/verify   → { "paid": true }
```

---

## Permission lari to'liq ro'yxati

### Admin boshqaruv permission lari

| Permission | Tavsif |
|------------|--------|
| `Role.CreateRole` | Yangi role yaratish |
| `Role.GetAll` | Barcha role larni ko'rish |
| `Role.GetById` | Role ni ID bo'yicha ko'rish |
| `Role.Update` | Role ni tahrirlash (permission lar bilan) |
| `Role.Delete` | Role ni o'chirish |
| `Role.GetPermissions` | Role permission larini ko'rish |
| `Role.GetAllowedPermissions` | Barcha ruxsat etilgan permission larni ko'rish |
| `OrganizationAdmin.Create` | Tashkilot yaratish |
| `OrganizationAdmin.GetAll` | Barcha tashkilotlarni ko'rish |
| `OrganizationAdmin.GetById` | Tashkilotni ko'rish |
| `OrganizationAdmin.Update` | Tashkilotni tahrirlash |
| `OrganizationAdmin.Delete` | Tashkilotni o'chirish |
| `MerchantAdmin.Register` | Merchant ro'yxatdan o'tkazish (super admin) |
| `MerchantAdmin.GetAll` | Barcha merchantlarni ko'rish |
| `MerchantAdmin.GetById` | Merchantni ko'rish |
| `MerchantAdmin.Update` | Merchantni tahrirlash |
| `MerchantAdmin.Delete` | Merchantni o'chirish |
| `StationAdmin.Create` | Stansiya yaratish |
| `StationAdmin.GetAll` | Barcha stansiyalarni ko'rish |
| `StationAdmin.GetById` | Stansiyani ko'rish |
| `StationAdmin.GetByMerchant` | Merchant bo'yicha stansiyalarni ko'rish |
| `StationAdmin.Update` | Stansiyani tahrirlash |
| `StationAdmin.Delete` | Stansiyani o'chirish |
| `DeviceAdmin.Register` | Qurilma ro'yxatdan o'tkazish |
| `DeviceAdmin.GetAll` | Barcha qurilmalarni ko'rish |
| `DeviceAdmin.GetById` | Qurilmani ko'rish |
| `DeviceAdmin.GetByStation` | Stansiya bo'yicha qurilmalarni ko'rish |
| `DeviceAdmin.Update` | Qurilmani tahrirlash |
| `DeviceAdmin.Delete` | Qurilmani o'chirish |
| `ProductAdmin.Create` | Mahsulot yaratish |
| `ProductAdmin.GetAll` | Barcha mahsulotlarni ko'rish |
| `ProductAdmin.GetByDevice` | Qurilma bo'yicha mahsulotlarni ko'rish |
| `ProductAdmin.GetById` | Mahsulotni ko'rish |
| `ProductAdmin.GetAllowedTypes` | Ruxsat etilgan mahsulot turlarini ko'rish |
| `ProductAdmin.Update` | Mahsulotni tahrirlash |
| `ProductAdmin.Delete` | Mahsulotni o'chirish |
| `UserAdmin.Create` | Foydalanuvchi yaratish (Legal/Merchant) |
| `UserAdmin.GetAll` | Barcha foydalanuvchilarni ko'rish |
| `UserAdmin.GetById` | Foydalanuvchini ko'rish |
| `UserAdmin.SetPassword` | Parol o'rnatish (birinchi marta) |
| `UserAdmin.ResetPassword` | Parol tiklash |
| `UserAdmin.Block` | Foydalanuvchini bloklash |
| `UserAdmin.Unblock` | Blokdan chiqarish |
| `UserAdmin.Delete` | Foydalanuvchini o'chirish |

### User app permission lari

| Permission | Tavsif |
|------------|--------|
| `Balance.TopUp` | Balansni to'ldirish |
| `Session.Create` | Sessiya yaratish |
| `Session.SetQuantity` | Miqdor belgilash va start |
| `Session.Close` | Sessiyani yopish |

### Permission ierarxiyasi

`MerchantAdmin.Register` — bu super admin permission. Uni egallagan user:
- Istalgan merchant ga station yaratishi mumkin
- Istalgan station ga device ro'yxatdan o'tkazishi mumkin
- Istalgan device ga product yaratishi mumkin
- Istalgan merchant ga user qo'shishi mumkin

Oddiy `StationAdmin.Create` yoki `DeviceAdmin.Register` — faqat o'z merchant/organization doirasida ishlaydi.

---

## Xavfsizlik mexanizmlari

### 1. Global Soft Delete Filter
```csharp
HasQueryFilter(e => !e.IsDeleted)
```
Barcha querylar avtomatik `WHERE is_deleted = false` sharti bilan ishlaydi. O'chirilgan entity lar hech qayerda ko'rinmaydi.

### 2. Parent Entity validatsiyasi (Create operatsiyalarida)

| Service | Tekshiruv |
|---------|-----------|
| StationService.Create | Merchant mavjud + `IsActive=true` |
| DeviceService.Register | Station mavjud + `IsActive=true` |
| ProductService.Create | Device mavjud + `IsActive=true`, Station `IsActive=true` |
| UserAdminService.Create | Organization/Station mavjud + `IsActive=true` |
| SessionService.Create | User mavjud + `IsBlocked=false` |

### 3. Access Control (ownership tekshiruvi)

Har bir Create/Update operatsiyada caller ning tegishli Merchant/Organization ga egaligi tekshiriladi:
- `MerchantUserEntity` → `callerStation.MerchantId == targetStation.MerchantId`
- `LegalUserEntity` → `caller.OrganizationId == target.OrganizationId`

### 4. Token xavfsizligi

| Xususiyat | Qiymat |
|-----------|--------|
| Access Token muddati | 15 daqiqa |
| Refresh Token muddati | 7 kun |
| Token rotation | Ha (har refresh da eski token bekor qilinadi) |
| Refresh token storage | Redis (TTL bilan) |
| Algoritm | HMAC-SHA256 |

### 5. Qurilma autentifikatsiyasi

Qurilma har bir MQTT xabarida `device_token` yuboradi. MqttBridge `ValidateDeviceAsync(serialNumber, secretKey)` orqali tekshiradi.

---

## Message Flow arxitekturasi

### User → Device (buyruqlar)

```
Mobile App
  → SignalR (SessionHub.PauseSession / ResumeSession / StopSession)
    → RabbitMQ (device.commands queue)
      → DeviceApi (consumer)
        → MqttBridge.PublishPauseCommandAsync()
          → MQTT: station/{serial}/command/pause
            → IoT Device
```

### Device → User (eventlar)

```
IoT Device
  → MQTT: station/{serial}/telemetry
    → MqttBridge.OnMessageAsync()
      → ValidateDeviceAsync (auth check)
        → RabbitMQ (device.events queue)
          → DeviceEventConsumer (UserApi)
            → SessionService.ReportProgressAsync()
              → SignalR: ProgressUpdate
                → Mobile App
```

### Redis ishlatilishi

| Key pattern | Maqsad | TTL |
|-------------|--------|-----|
| `refresh:{token}` | Refresh token → userId | 7 kun |
| `device:lock:{serialNumber}` | Qurilma ekskluziv qulfi → userId | 30 daqiqa |

---

## Error Response formati

Barcha API lar yagona format qaytaradi:

**Muvaffaqiyatli:**
```json
{
  "isSuccess": true,
  "result": { ... }
}
```

**Xato:**
```json
{
  "isSuccess": false,
  "errorObj": {
    "code": 404,
    "errorMessage": "Stansiya topilmadi."
  }
}
```

**HTTP status kodlari:**

| Kod | Ishlatilish |
|-----|-------------|
| 200 | Muvaffaqiyatli operatsiya |
| 400 | Validatsiya xatosi (noto'g'ri ma'lumot, balans yetarli emas) |
| 401 | JWT token yo'q yoki yaroqsiz |
| 403 | Permission yo'q yoki boshqa foydalanuvchining resursi |
| 404 | Entity topilmadi yoki o'chirilgan |
| 409 | Dublikat (telefon, serial number va h.k.) |
