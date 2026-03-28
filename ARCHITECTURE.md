# BotEnergy — Loyiha Arxitekturasi

## Umumiy tasnif

**BotEnergy** — elektr zaryadlash stantsiyalari, qurilmalar va foydalanuvchilarni boshqarish uchun mo'ljallangan backend tizim. Qurilmalardan foydalanish sessiyalarini kuzatish, to'lovlar va foydalanuvchi rollari tizimini o'z ichiga oladi.

---

## Texnologiyalar

| Texnologiya | Versiya / Tavsif |
|---|---|
| .NET | 8.0 |
| ASP.NET Core | Web API |
| Entity Framework Core | 9.x (Npgsql) |
| PostgreSQL | Asosiy ma'lumotlar ombori |
| JWT | `System.IdentityModel.Tokens.Jwt` |
| Swagger | Swashbuckle 6.6.2 |

---

## Arxitektura — Clean Architecture

```
BotEnergy/
├── Core/
│   ├── Domain/          ← Entitylar, Interfacelar, DTOlar, Repozitoriy interfeyslari
│   └── Application/     ← Servislar, Helperlar (biznes logikasi)
├── Infrastructure/
│   ├── Persistence/     ← EF Core DbContext, Repozitoriy implementatsiyalari
│   └── External/        ← Tashqi servislar
├── CommonConfiguration/ ← DI konfiguratsiya, Middleware, Filterlar, Atributlar
└── WebApi/
    ├── AuthApi/         ← Autentifikatsiya (port 5002)
    ├── UserApi/         ← Foydalanuvchi API (port 5006)
    └── AdminApi/        ← Admin API (port 5001)
```

### Bog'liqlik zanjiri

```
Domain  ←  Application  ←  Infrastructure/Persistence
                        ←  Infrastructure/External
                        ←  CommonConfiguration
                        ←  WebApi/*
```

---

## API larning tavsifi

### AuthApi — `http://*:5002`

Foydalanuvchi autentifikatsiyasini boshqaradi. **Permission tekshiruvi yo'q.**

| Endpoint | Metod | Tavsif |
|---|---|---|
| `/api/auth/register` | POST | Yangi foydalanuvchi ro'yxatdan o'tkazish (OTP yuboradi) |
| `/api/auth/verify` | POST | OTP kodni tasdiqlash |
| `/api/auth/set-password` | POST | Parol o'rnatish → Access + Refresh token qaytaradi |
| `/api/auth/login` | POST | Kirish → Access + Refresh token |
| `/api/auth/refresh-token` | POST | Token yangilash *(hali implement qilinmagan)* |
| `/api/auth/reset-password-request` | POST | Parolni tiklash uchun OTP yuborish |
| `/api/auth/reset-password-verify` | POST | Tiklash OTP ni tasdiqlash |
| `/api/auth/reset-password-set` | POST | Yangi parol o'rnatish |

**Ro'yxatdan o'tish 3 bosqich:**
1. `Register` → OTP yuboriladi
2. `Verify` → OTP tasdiqlanadi
3. `SetPassword` → Parol o'rnatiladi, token beriladi

---

### UserApi — `http://*:5006`

Autentifikatsiyalangan foydalanuvchilar uchun. Barcha endpoint `[Authorize]` + `PermissionFilter`.

| Endpoint | Metod | Permission | Tavsif |
|---|---|---|---|
| `/api/user/me` | GET | *(SkipPermissionCheck)* | O'z profilini ko'rish |
| `/api/user/update-me` | PUT | *(SkipPermissionCheck)* | Profilni yangilash (Mail, PhoneId) |

---

### AdminApi — `http://*:5001`

Admin va operator funksiyalari uchun. Barcha endpoint `[Authorize]` + `PermissionFilter`.

| Endpoint | Metod | Permission | Tavsif |
|---|---|---|---|
| `/api/role/create-role` | POST | `Role.CreateRole` | Yangi rol yaratish |
| `/api/role/get-all` | GET | `Role.GetAll` | Barcha rollarni ko'rish |
| `/api/role/add-permission` | POST | `Role.AddPermission` | Rolga permission qo'shish |
| `/api/role/remove-permission` | DELETE | `Role.RemovePermission` | Roldan permission o'chirish |
| `/api/role/assign-to-user` | POST | `Role.AssignToUser` | Userga rol berish |
| `/api/role/get-permissions/{roleId}` | GET | `Role.GetPermissions` | Rol permissionlarini ko'rish |
| `/api/clientadmin/register` | POST | `ClientAdmin.Register` | Mijoz ro'yxatga olish *(stub)* |
| `/api/deviceadmin/register` | POST | `DeviceAdmin.Register` | Qurilma ro'yxatga olish *(stub)* |
| `/api/deviceadmin/change-status` | POST | `DeviceAdmin.ChangeStatus` | Qurilma holatini o'zgartirish *(stub)* |
| `/api/yuridikadmin/create` | POST | `YuridikAdmin.Create` | Yuridik shaxs yaratish *(stub)* |

---

## Ma'lumotlar modeli

### Entity ierarxiyasi

```
Entity (BaseEntity)
├── Id: long
├── CreatedDate: DateTime
├── UpdatedDate: DateTime
└── IsDeleted: bool (soft-delete)
```

### Asosiy entitylar

#### UserEntity
```
UserEntity
├── PhoneId: string          ← Qurilma identifikatori
├── Mail: string
├── PhoneNumber: string      ← Login uchun ishlatiladi
├── Balance: decimal
├── IsBlocked: bool
├── IsVerified: bool         ← 3-bosqich tugagandan keyin true
├── IsOtpVerified: bool      ← OTP tasdiqlangandan keyin true
├── UserType: UserType       ← NaturalPerson | LegalEntity
├── LastLoginDate: DateTime
├── LastActiveDate: DateTime
├── PasswordHash: string?
├── PasswordSalt: string?
├── RoleId: long?            ← Foydalanuvchi roli
└── Role: RoleEntity?
```

#### RoleEntity
```
RoleEntity
├── Name: string
├── Description: string?
├── IsActive: bool
├── OrganizationId: long
└── Organization: OrganizationEntity?
```

#### RolePermissionEntity
```
RolePermissionEntity
├── RoleId: long
├── Role: RoleEntity?
└── Permission: string       ← "Controller.Action" formatida, masalan "Role.CreateRole"
```

#### OrganizationEntity
```
OrganizationEntity
├── Name: string
├── Inn: string
├── Address: string
├── PhoneNumber: string
└── IsActive: bool
```

#### StationEntity
```
StationEntity
├── Name: string
├── Location: string
├── OrganizationId: long
├── Organization: OrganizationEntity?
└── IsActive: bool
```

#### DeviceEntity
```
DeviceEntity
├── SerialNumber: string
├── Model: string
├── FirmwareVersion: string
├── StationId: long
├── Station: StationEntity?
├── IsOnline: bool
└── IsActive: bool
```

#### UsageSessionEntity
```
UsageSessionEntity
├── UserId: long
├── User: UserEntity?
├── DeviceId: long
├── Device: DeviceEntity?
├── ProductType: ProductType
├── Quantity: decimal
├── Price: decimal
├── StartedAt: DateTime
└── EndedAt: DateTime?
```

#### ProductEntity
```
ProductEntity
├── Name: string
├── Type: ProductType
├── Unit: UnitType
├── Price: decimal
└── IsActive: bool
```

### Enumlar

```csharp
// Qurilma turlari
DeviceType { FUEL_DISPENSER, WASH_BOX, CHARGER, METHANE_PUMP, PROPANE_PUMP }

// Mahsulot turlari
ProductType { Water=1, Foam=2, Wax=3, Petrol=10, Diesel=11, Methane=12, Propane=13, Electricity=20 }

// O'lchov birliklari
UnitType { Liter=1, CubicMeter=2, KilowattHour=3, Second=4, Piece=5 }

// Foydalanuvchi turi
UserType { NaturalPerson, LegalEntity }

// OTP maqsadi
OtpPurpose { Register, ResetPassword }
```

---

## Permission tizimi

### Ishlash prinsipi

```
Login/SetPassword
    → IRoleRepository.GetUserPermissionsAsync(user.RoleId)
    → TokenService.GenerateAccessToken(user, permissions)
    → JWT ichiga "Permission" claimlar qo'shiladi

Har bir so'rov:
    → [Authorize] → JWT validatsiya
    → ValidationFilter (Order=0) → input tekshiruvi
    → PermissionFilter (Order=1000) → JWT dan permissionlar o'qiladi
                                    → "{Controller}.{Action}" talab qilinadi
                                    → yo'q → 403 Forbidden
                                    → bor → action bajariladi
```

### Permission nomlash qoidasi

```
{ControllerName}.{ActionName}

Misollar:
  Role.CreateRole
  Role.GetAll
  Role.AddPermission
  Role.RemovePermission
  Role.AssignToUser
  Role.GetPermissions
  ClientAdmin.Register
  DeviceAdmin.Register
  DeviceAdmin.ChangeStatus
  User.Me          ← [SkipPermissionCheck] bilan, tekshirilmaydi
  User.UpdateMe    ← [SkipPermissionCheck] bilan, tekshirilmaydi
```

### SkipPermissionCheckAttribute

`[SkipPermissionCheck]` atributi qo'yilgan endpointlarda `PermissionFilter` ishlaydi, lekin o'tkazib yuboradi. Faqat autentifikatsiya (`[Authorize]`) talab qilinadi.

---

## Servislar

### IAuthService / AuthService

Barcha autentifikatsiya oqimlarini boshqaradi.

### ITokenService / TokenService

```csharp
// JWT yaratish — 15 daqiqa muddatli, PhoneNumber + Permission claimlar
string GenerateAccessToken(UserEntity user, IEnumerable<string> permissions)
string GenerateRefreshToken() // GUID
```

**JWT Secret (hardcoded):** `3f1e2d4c5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d`

### IOtpService / OtpService

In-memory saqlash (`Dictionary<string, string>`). `"123456"` har doim qabul qilinadi (test rejimi). OTP konsol ga chiqariladi.

### IUserService / UserService

Autentifikatsiyalangan foydalanuvchining o'z profili bilan ishlash.

### IRoleService / RoleService

Rollar va permissionlarni boshqarish: yaratish, permission qo'shish/o'chirish, userga rol berish.

### PasswordHelper

SHA256 + random GUID salt bilan parol hashing.

---

## Konfiguratsiya

### Ma'lumotlar bazasi ulanishi

`CommonConfiguration/ConfigurationFile/Configuration.Development.json` da saqlanadi.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Port=5432;Database=botenergy_db;..."
  }
}
```

`builder.Configuration.AddCommonConfiguration()` orqali yuklanadi.

### DI ro'yxatdan o'tkazish

`CommonConfiguration/ConfigurationExtensions/ConfigurationAddExtensions.cs`:

```csharp
RegisterServices(services):
  IAuthService    → AuthService
  ITokenService   → TokenService
  IOtpService     → OtpService
  IUserRepository → UserRepository
  IUserService    → UserService
  IRoleRepository → RoleRepository
  IRoleService    → RoleService
```

---

## Filterlar va Request pipeline

### AuthApi
```
Request → ExceptionMiddleware → ValidationFilter (TypeFilter) → Action
```

### UserApi / AdminApi
```
Request → ExceptionMiddleware → [Authorize] → ValidationFilter (Order=0) → PermissionFilter (Order=1000) → Action
```

### Mavjud ValidationFilterlar (AuthApi)

| Filter | Tekshiradi |
|---|---|
| RegisterValidationFilter | PhoneNumber, Mail |
| VerifyValidationFilter | PhoneNumber, OtpCode |
| SetPasswordValidationFilter | PhoneNumber, Password |
| LoginValidationFilter | PhoneNumber, Password |
| RefreshTokenValidationFilter | RefreshToken |
| ResetPasswordRequestValidationFilter | PhoneNumber |
| ResetPasswordVerifyValidationFilter | PhoneNumber, OtpCode |
| ResetPasswordSetValidationFilter | PhoneNumber, NewPassword |

### Mavjud ValidationFilterlar (AdminApi)

| Filter | Tekshiradi |
|---|---|
| CreateRoleValidationFilter | Name |
| AddPermissionValidationFilter | RoleId, Permission |
| RemovePermissionValidationFilter | RoleId, Permission |
| AssignRoleValidationFilter | PhoneNumber, RoleId |

---

## Ma'lumotlar omboridagi jadvallar (DbContext)

```
Users, Organizations, Roles, RolePermissions,
Stations, Products, Devices, UsageSessions, Clients
```

EF Core avtomatik `UpdatedDate` ni yangilaydi (`SaveChangesAsync` override).
Soft-delete: `IsDeleted = true` (haqiqiy o'chirish yo'q).

---

## Eslatmalar va rivojlantirish holati

| Holat | Tavsif |
|---|---|
| Stub (bajarilmagan) | `ClientAdmin`, `DeviceAdmin`, `YuridikAdmin`, `UserAdmin`, `UserBalance` controllerlari |
| Hali implement qilinmagan | Refresh token (`501 Not Implemented` qaytaradi) |
| Test rejimi | OTP `"123456"` har doim qabul qilinadi |
| In-memory | OTP xotirada saqlanadi, server restart da yo'qoladi |
| Hardcoded | JWT Secret kod ichida, konfigga ko'chirish kerak |
| Migration kerak | `UserEntity.RoleId` va `RolePermissionEntity.Permission` (string) uchun |

---

## Migratsiya

Yangi entitylar uchun quyidagi buyruq bajarilishi kerak:

```bash
dotnet ef migrations add AddRolePermissionSystem --project Infrastructure/Persistence --startup-project WebApi/AuthApi
dotnet ef database update --project Infrastructure/Persistence --startup-project WebApi/AuthApi
```
