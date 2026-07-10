# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project context

BotEnergy — IoT charging/dispenser station backend. .NET 8, PostgreSQL, Redis, MQTT, SignalR. Seven independently-runnable Web APIs in one solution sharing a Clean Architecture core. (RabbitMQ removed — device messaging is MQTT-only.)

For deep API reference, architecture overview, and business flows see `README.md` (the canonical doc). `PROMTS.md` holds the business-process spec (extracted from code). (`ARCHITECTURE.md` was removed — its content was merged into `README.md`.)

## Commands

All commands run from solution root (`BotEnergy.sln` lives there).

```powershell
# Build single API (fastest feedback loop — solution build pulls in all 6)
dotnet build WebApi/UserApi/UserApi.csproj

# Build whole solution
dotnet build BotEnergy.sln

# Run a specific API locally
dotnet run --project WebApi/UserApi
dotnet run --project WebApi/AuthApi
# ...etc

# EF Core migrations — Persistence is the project, ANY WebApi works as startup
dotnet ef migrations add <Name> --project Infrastructure/Persistence --startup-project WebApi/AuthApi
dotnet ef database update --project Infrastructure/Persistence --startup-project WebApi/AuthApi
```

There are no tests in the repo. Don't claim test coverage when reporting completion.

Production deploy is a self-hosted GitHub Actions runner that executes `deploy.sh` on push to `main`/`master` — builds each API in Release, copies to `/home/ubuntu/botenergy/<Service>`, restarts `botenergy-<Service>` systemd unit.

## API layout

| API | Port | Purpose |
|---|---|---|
| `AuthApi` | 5002 | Public. Two surfaces: `/api/Auth/*` (Customer — register/OTP/login/refresh) and `/api/PlatformAuth/{Login,RefreshToken}` (Platform — no self-register). No `[Authorize]`, no `PermissionFilter`. |
| `UserApi` | 5006 | Mobile app endpoints — profile, balance read, reports. JWT + permissions. |
| `AdminApi` | 5001 | Admin/operator endpoints. JWT + permissions. |
| `DeviceApi` | 5004 | Device identity & CRUD (DeviceAuth, DeviceController). DeviceProcess/DevicePayment are legacy HTTP stubs. MQTT bridge moved to SessionApi. |
| `SessionApi` | 5007 | Owns sessions, processes, payments, MQTT transport+pipeline (qurilma ↔ server), SignalR hub `/hubs/session`, and all device-event handlers. |
| `BillingApi` | 5003 | Admin balance top-up (`/api/Balance/TopUp`). User balance read lives in UserApi. |
| `PaymentApi` | 5005 | Stub (Payme integration planned). |

All APIs share `Persistence/AppDbContext` (single PostgreSQL DB) and `CommonConfiguration` (DI extensions, filters, middleware, Redis wiring, health checks).

## Architecture invariants worth remembering

### Configuration loading is custom — `appsettings.json` is mostly ignored

Configuration lives in `Infrastructure/CommonConfiguration/ConfigurationFile/Configuration{.Development,.Production}.json` and is loaded by `builder.Configuration.AddCommonConfiguration()` (see `ConfigurationServices/CommonConfiguration.cs`). Load order: `Configuration.json` (Hosting/ports) → `Configuration.{env}.json` → env vars. Both env files carry the SAME field set; in Production sensitive values are `Env_<EnvVarName>` placeholders — the server supplies same-named env vars (`Jwt__Secret`, `Mqtt__Password`, ...) at service start, which override the json. `ResolveSecret` treats `Env_*` values as unset, so a missing env var fails fast instead of leaking the placeholder as a secret. The `appsettings.json` files in each WebApi exist but are not the primary source.

`RunApi(name, defaultPort)` extension reads `Hosting:Ports:{apiName}` and `Hosting:UseHttps` from config to pick port and scheme.

### Database migrations apply automatically on startup

`app.ApplyMigrationsAsync()` runs `Database.MigrateAsync()` then `DataSeeder.SeedAsync()` every boot. Don't write code that assumes migrations are run separately. After adding entities/properties, generate a migration but don't run `database update` — the next API start will apply it.

### DI registration is split — pay attention to which `Register*` extension you extend

In `CommonConfiguration/ConfigurationExtensions/ConfigurationAddExtensions.cs`:

- `RegisterServices()` — shared by all APIs. Includes the split user/role layer: `IPlatformUserRepository`+`ICustomerUserRepository`, `IPlatformRoleRepository`+`ICustomerRoleRepository`, `IPermissionRepository`; services `IUserService`, `IUserAdminService` (platform), `ICustomerAdminService` (corporate), `IRoleService` (platform), `ICustomerRoleService`, plus Device/Product/Merchant/Organization/Billing.
- `RegisterAuthServices(config)` — AuthApi: `IAuthService` (customer) + `IPlatformAuthService` + `ITokenService` + `IOtpService` (singleton) + `JwtSettings`/`OtpSettings` singletonlari.
- `RegisterSessionServices()` — SessionApi-only (`ISessionService`, `IProcessService`, `IBootstrapService`, `IPushNotificationService`, `IdleSessionCleanerService` HostedService).
- `RegisterDeviceServices()` — DeviceApi-only (legacy; DeviceApi no longer carries live traffic).
- `AddRedisServices()` — Redis multiplexer + `IRefreshTokenStore` (resilient: Redis primary, in-memory fallback) + `IIdempotencyStore` + `IdempotencyFilter`.
- `AddIpRateLimiting()` — IP-based fixed window limiter (AuthApi'da yoqilgan).
- `RegisterServices()` ichida `ITransactionRunner` ham bor — bir nechta repo chaqiruvini bitta DB tranzaksiyada bajarish uchun (nested chaqiruv tashqi tranzaksiyaga qo'shiladi).

`ISessionNotifier` is *not* registered in any shared extension — SessionApi `Program.cs` registers `SignalRSessionNotifier` itself, because it depends on the SignalR hub which only SessionApi hosts.

If you put a service into `RegisterServices` that depends on `ISessionService`, every API except SessionApi will fail at `ValidateOnBuild` (DI graph is validated on build in Development).

### Permission system uses string convention `{Controller}.{Action}`

`PermissionFilter` (Order=1000, runs AFTER ValidationFilters at Order=0):

1. If `[SkipPermissionCheck]` present → pass.
2. If `[RequirePermission("...")]` present → use that string.
3. Else → derive from route: `"{controller}.{action}"`.
4. Look for the string among JWT `"Permission"` claims. Missing → 403.

All permission strings live as constants in `Core/Domain/Constants/Permissions.cs` and `Permissions.All` (the seed list). When you add a permission-protected endpoint:
1. Add the const.
2. Add it to `Permissions.All`.
3. Apply `[RequirePermission(Permissions.X)]`.

`DataSeeder` reads `Permissions.All` and inserts missing rows on startup. `Permissions` is split into `PlatformAll` (admin/merchant management) and `CustomerAll` (session/process/profile/report/payment-self + corporate `CustomerAdmin.*`); `All` is their union. `PermissionScopes.IsAllowedFor(RoleKind, name)` gates which permissions attach to which role kind.

### User & Role model: two groups, separate tables (NOT TPH)

The user/role domain is split into two independent groups, each with its own table and its own role table (no shared `users`/`roles` table, no discriminator):

- **Platform** (`auth.platform_users`) — `PlatformUserEntity`, subtype enum `PlatformUserType {Manage, Merchant}` (int column `type`). `Manage` = full access (no scope limit). `Merchant` = scoped to its `MerchantId` (whole business: all its stations/devices/products). Roles: `auth.platform_roles` (`PlatformRoleEntity`, `MerchantId?` null=Manage/global role). Created only by Manage. Login via PlatformAuth.
- **Customer** (`auth.customer_users`) — `CustomerUserEntity`, subtype `CustomerUserType {Natural, Corporate}`. `Natural` = self-registers (mobile), own `Balance`. `Corporate` = created by Manage/corp-admin, shares `Organization.Balance` via `OrganizationId`. Roles: `auth.customer_roles` (`CustomerRoleEntity`, `OrganizationId?` null=global Natural role). Join tables: `platform_role_permissions`, `customer_role_permissions` (shared `permissions` catalog).

`UserBase`/`RoleBase` are unmapped abstract bases for shared columns only (each concrete entity `ToTable`s separately). There is no `UserRoleEntity` (m:n) — a single `RoleId` FK per user.

`AccessScope` (`Core/Domain/Auth/AccessScope.cs`, built from JWT claims `UserGroup`/`UserSubType`/`MerchantId`/`OrganizationId`): `IsManage` = full access, `IsMerchant`/`IsCorporate` = scoped, `CanAccessMerchant/Organization`. Scope-aware services (Station/Device/Product/Merchant/Organization/User/Role/Corporate) use it — `IsManage` means "no scope limit" (NOT old `IsPlatform`). Sessions/payments FK to `customer_users` only.

Refresh tokens are namespaced by group prefix in the token value: `c:` (customer) / `p:` (platform), so each auth service only accepts its own.

### Two-layer service split inside a session

Sessions and processes are separate services with distinct responsibilities:

- `SessionService` — lifecycle (Create/DeviceConnect/Close/Heartbeat/GetCurrent/History/timeout cleaners). Does NOT touch device commands directly.
- `ProcessService` — Start/Stop/Pause/Resume + telemetry + device-finished reports. Does NOT manage session lifecycle.

`SessionStatus`: `Created → Connected → InProcess → Closed`. `InProcess` is set when the first telemetry arrives in `ProcessService.ReportTelemetryAsync`, not when `Process.Start` is called.

`SessionService` keeps `LastActivityAt` as a sliding idle timeout (30 min). Background `IdleSessionCleanerService` invokes `CloseTimedOutSessionsAsync` and `CloseOfflineDeviceSessionsAsync`. Anything that mutates a session should also touch `LastActivityAt` (use `ISessionRepository.TouchAsync(sessionId)` for the heartbeat hot-path — it's a single SQL UPDATE without entity tracking).

### Message flow (mobile ↔ device) — single broker (MQTT), no RabbitMQ

RabbitMQ has been removed — commands publish straight to MQTT (`MqttDeviceCommandPublisher`), inbound MQTT messages run through an in-process middleware pipeline (`WebApi/SessionApi/Mqtt/`).

```
Mobile → REST (SessionApi)
       → SessionService/ProcessService → DB + IDeviceCommandPublisher
                                       → MqttDeviceCommandPublisher (direct, same process)
                                       → MQTT topic device/{serial}/...
                                       → IoT device

IoT device → MQTT topics (request/response/event/telemetry/state)
           → MqttHost (BackgroundService) → MqttPipeline middlewares:
             Deserialize → DeviceAuth → Hmac → Timestamp → Replay → Dispatcher
           → handler (SessionConnectHandler / ProcessTelemetryHandler /
             ProcessFinishedHandler / PaymentQrHandler / ...)
           → SessionService / ProcessService / DeviceSessionService
           → DB update + ISessionNotifier (SignalR)
           → Mobile
```

Envelope security: every device message is `{id, type, timestamp, payload, hmac}` — HMAC-SHA256 keyed off the device `SecretKey`, verified constant-time; replay protection is per-device monotonic `id` stored in Redis without TTL (`mqttid:in:{serial}` / `mqttid:out:{serial}`, in-memory shadow fallback if Redis is down). Counters are NEVER reset automatically — not on `session.connect`, not on restart; the device keeps its counters in EEPROM (simulator: localStorage). The only reset path is the expert-mode endpoint `POST /api/Device/ResetMqttCounters/{id}` (AdminApi, permission `DeviceAdmin.ResetMqttCounters`, Manage-only) used after a device EEPROM re-flash.

All pieces — REST controllers, MQTT transport+pipeline, handlers, ProcessService, SignalR hub — live in the SessionApi process.

SignalR hub path: `/hubs/session`. Two group schemes:
- `sessionToken` — tablet+phone watching same session.
- `user:{userId}` — auto-joined in `SessionHub.OnConnectedAsync` from JWT, used by `NotifyUserAsync` for cross-device pushes that don't depend on having a session token.

### Soft delete + auto-timestamps

Every entity inherits `Entity` (`Id`, `CreatedDate`, `UpdatedDate`, `IsDeleted`). `AppDbContext` has a global query filter `WHERE IsDeleted = false` and overrides `SaveChangesAsync` to update `UpdatedDate`. There is no hard-delete path. PostgreSQL columns use `timestamp without time zone` with `LOCALTIMESTAMP` defaults — code uses `DateTime.Now` everywhere (NOT `DateTime.UtcNow`). Don't change this without a project-wide decision; the user has explicitly chosen local time.

### Idempotency for retry-safe POSTs

`[Idempotent]` attribute + `IdempotencyFilter` (registered globally in SessionApi `Program.cs` via `options.Filters.AddService<IdempotencyFilter>()`):

- Reads `Idempotency-Key` request header. If `Required = true` and missing → 400.
- Cache key is `{userId}:{action}:{header}`. Stored in Redis under `idem:` prefix.
- Reservation lock (30s) prevents concurrent duplicates → 409. Successful 2xx responses cached 24h. Non-2xx releases the reservation so client can retry after fixing input.
- Replay sets `Idempotent-Replay: true` response header.

Currently applied to `Session.Create` and `Process.Start`. Apply to other state-mutating POSTs as the need arises.

### Refresh tokens use a resilient Redis-with-fallback pattern

`ResilientRefreshTokenStore` wraps `RedisRefreshTokenStore` + `InMemoryRefreshTokenStore` — if Redis is down, writes go to memory and reads check both. `Redis.AbortOnConnectFail = false`, so app boots even with no Redis. Token rotation: every refresh revokes the old token and issues a new pair (15min access, 7d refresh).

### `Bootstrap` endpoint is the canonical "app start" call

`GET /api/Session/Bootstrap` (SessionApi, composed by `BootstrapService`) runs `IUserService.GetCurrentUserAsync` and `ISessionService.GetCurrentAsync` in parallel and returns `{ user, activeSession?, serverTime }`. Mobile app should call this on every cold start (whether resuming an active session or freshly logged in).

## Things to avoid

- **Don't put new shared services into `RegisterServices` if they need `ISessionService`/`IProcessService`** — those are SessionApi-only and `ValidateOnBuild` will fail other APIs.
- **Don't write `appsettings.json` for new config** — add to `Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.{env}.json`.
- **Don't switch `DateTime.Now` → `DateTime.UtcNow`** in isolation — the whole stack is local-time and PostgreSQL columns are `timestamp without time zone`. User has explicitly declined this change.
- **Don't call `DELETE FROM`** — soft delete only (`IsDeleted = true`).
- **Production secrets never go into config files as real values.** `Configuration.Production.json` holds `Env_<EnvVarName>` placeholders; real values come from env vars at service start (`ConnectionStrings__DefaultConnection`, `Jwt__Secret`, `Mqtt__Password`, `InternalApi__SharedSecret`, `Seed__AdminPassword`, ...). `GetJwtSecret` throws in Production if `Jwt:Secret` is missing/placeholder; Development falls back to a dev-only constant. Signing (`TokenService` via `JwtSettings`) and validation (`AddJwtAuthentication`) read the same value.
- **JWT audience is per user group** (`JwtAudiences.Customer` / `.Platform`): AdminApi/BillingApi accept platform-only, UserApi/SessionApi customer-only. New APIs must pass `acceptedAudiences` to `AddJwtAuthentication`.
- **OTP test code `"123456"`** works only when `Otp:AllowTestCode` is true (set in `Configuration.Development.json`, NOT in prod). OTP has TTL (3 min) + attempt limit (5); service is in-memory and resets on restart.
- **Passwords are PBKDF2** (`Domain/Helpers/PasswordHelper`, 600k iterations); legacy SHA256 hashes are re-hashed lazily on successful login. Don't compare hashes with `==`.
- **Money mutations must be atomic**: use `ICustomerUserRepository/IOrganizationRepository.DeductBalanceAsync/TopUpBalanceAsync` (FOR UPDATE lock) and claim the process via `TryClaimBalanceDeductionAsync` inside `ITransactionRunner.RunAsync` — never read-modify-write `Balance` on the entity.
- **Default admin** is seeded only when `Seed:AdminPassword` is set (Development falls back to `Admin@123`; Production skips creation).
- **`/health`** is mapped on every API (DB + Redis checks). AuthApi has IP rate limiting (30 req/min → 429 + `Retry-After`).
