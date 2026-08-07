# Bajarilgan ishlar — arxitektura implementatsiyasi

> `docs/PRODUCTION_ARCHITECTURE.md` dagi dizaynning kod bilan bajariladigan qismi.
> Qo'lda bajarilishi kerak bo'lganlar: `docs/MANUAL_TASKS.md`.
>
> Sana: 2026-08-07 · Holat: `dotnet build BotEnergy.sln` → 0 xato.

---

## 1. Xavfsizlik konfiguratsiyasi

| Fayl | O'zgarish |
|---|---|
| `ConfigurationFile/Configuration.Production.json` | Barcha sirlar `Env_*` placeholder'ga; `Otp:AllowTestCode` → **false**; `Swagger:Enabled` → **false**; `Migrate:AutoApply` → **false**; MQTT host `emqx`, Redis `redis:6379` |
| `ConfigurationFile/Configuration.json` | `Hosting:Ports:Gateway=8080`; JWT/RateLimit/Swagger/Migrate/Observability default'lari; to'liq `ReverseProxy` bo'limi |
| `ConfigurationFile/Configuration.Development.json` | Yangi kalitlar qo'shildi; `SharedSubscriptionGroup` bo'sh (Mosquitto `$share` ni qo'llab-quvvatlamaydi) |

⚠️ Production'da endi env fayl to'ldirilmasa servislar **ataylab** ishga tushmaydi.

---

## 2. JWT qattiqlashtirish

- `Jwt:ValidateIssuer` + `Jwt:Issuer` — `AddJwtAuthentication` tekshiradi, `TokenService` yozadi (bitta konfiguratsiya manbai).
- `ClockSkew` 5 daqiqadan **30 soniyaga** tushirildi — muddati tugagan token 5 daqiqa qabul qilinmaydi.
- `ValidateIssuer` yoqilganda `Jwt:Issuer` majburiy — bo'lmasa startup'da fail-fast.

**Fayllar:** `ConfigurationAddExtensions.cs`, `Core/Domain/Auth/JwtSettings.cs`, `Core/Application/Services/TokenService.cs`

> Deploy paytida amaldagi access tokenlar bekor bo'ladi (15 daqiqa umr). Refresh
> tokenlar opaque GUID — ular ishlaydi, klientlar avtomatik qayta login qiladi.

---

## 3. Health check'lar ajratildi

```
/health/live   → hech qanday check. "Jarayon javob beryaptimi" (Docker healthcheck)
/health/ready  → DB + Redis ("ready" tegli). YARP active health check shu yerga qaraydi
/health        → orqaga moslik, /ready bilan bir xil
```

Ilgari bitta `/health` DB+Redis'ni tekshirardi. Uni liveness sifatida ishlatganda
Postgres 30 soniya sekinlashsa orchestrator 7 servisni birdan restart qilib
vaziyatni yomonlashtirardi.

**Fayllar:** `ConfigurationAddExtensions.AddInfrastructure`, `ConfigurationUseExtensions.MapBotEnergyHealthChecks`

---

## 4. YARP Gateway — `WebApi/Gateway/`

Yangi loyiha. Yagona public HTTP kirish nuqtasi.

| Komponent | Vazifa |
|---|---|
| `Program.cs` | YARP wiring, JWT, CORS, forwarded headers, health, metrics |
| `Middlewares/AuditLoggingMiddleware.cs` | POST/PUT/PATCH/DELETE audit: kim, nima, status, IP, davomiylik. Body **yozilmaydi** (parol/karta/PDP) |
| `Extensions/GatewayRateLimitingExtensions.cs` | 3 qatlam: global IP (300/min), `auth-strict` (10/min), `per-user` token bucket |
| `Extensions/GatewaySwaggerExtensions.cs` | 7 ta downstream swagger bitta UI'da |

**Transform'lar:** har so'rovga `X-Request-Id`; autentifikatsiyalangan bo'lsa
`X-User-Id`, `X-User-Group`, `X-User-SubType`, `X-Merchant-Id`, `X-Organization-Id`.
Bular **ishonch manbai emas** — servis baribir JWT'ni qayta tekshiradi.

**URL sxemasi:** `/api/{servis}/{controller}/{action}` → gateway `{servis}` ni olib tashlaydi.
Swagger yo'llari `Order=-1` va policy'siz — hujjat tokensiz o'qiladi.

**Runtime tasdiqlangan:**
```
/health/live                    → 200
/nothing                        → 404
/api/user/User/Profile (token yo'q) → 401
/api/user/swagger/v1/swagger.json   → 502 (proxy qilindi, 401 EMAS ✓)
/swagger/index.html             → 200
/metrics                        → OpenTelemetry chiqishi
```

---

## 5. Observability

`Infrastructure/CommonConfiguration/Observability/`:

- `ObservabilityExtensions` — OpenTelemetry tracing (OTLP) + metrics (Prometheus `/metrics`).
  `/health` va `/metrics` trace'dan filtrlanadi.
- `BotEnergyMetrics` — `botenergy_mqtt_received_total`, `_rejected_total{reason}`,
  `_handled_total`, `_published_total`, `_pipeline_duration_ms`.
- `LoggingExtensions` — Production'da **compact JSON stdout** (fayl sink yo'q, konteynerda ma'nosiz);
  `TraceContextEnricher` har logga `TraceId`/`SpanId` qo'shadi → Loki'dan Tempo'ga o'tish.

MQTT pipeline'ining har bir rad etish nuqtasi metrika yozadi:
`deserialize`, `device_not_found`, `hmac`, `timestamp_old`, `timestamp_future`, `replay`, `exception`.
`MqttHost` har xabar uchun `ActivitySource` span ochadi (`device.serial`, `mqtt.topic` tag'lari bilan).

> **Dizayndan chetlanish:** hujjatda alohida `Infrastructure/Observability` loyihasi
> ko'rsatilgan edi. Amalda `CommonConfiguration` ichiga qo'yildi — uni barcha 9 loyiha
> allaqachon referens qiladi, alohida loyiha 9 ta `ProjectReference` qo'shishni talab qilardi.

---

## 6. MQTT masshtab tayyorgarligi

**Blocker bartaraf etildi:** `MqttConnection` sobit `ClientId` + `CleanSession(false)`
ishlatardi. MQTT spetsifikatsiyasi bo'yicha bir xil ClientId bilan ikkinchi ulanish
birinchisini uzadi — ikkinchi SessionApi replikasi birinchisini cheksiz uzib turardi.

- `MqttOptions.EffectiveClientId` = `{prefiks}-{MachineName}-{ProcessId}`
- `MqttOptions.SubscriptionTopic()` — `SharedSubscriptionGroup` berilgan bo'lsa `$share/{group}/` prefiksi
- `state` topic'i **hech qachon** shared emas (retained snapshot har instansiyaga kerak)

> **Hujjatdagi da'vo noto'g'ri edi:** replay counter'ning atomikligi allaqachon
> ta'minlangan — `RedisMqttMessageIdStore` Lua script bilan compare-and-set qiladi.
> O'zgartirish kerak bo'lmadi.

---

## 7. Per-device MQTT autentifikatsiya

**Muammo:** barcha qurilmalar bitta `botenergy-device` credential'i bilan ulanardi.

**Yechim — migratsiyasiz:** `Domain/Helpers/DeviceMqttCredentials`
qurilmaning broker parolini `SecretKey` dan HMAC bilan hosil qiladi
(`HMAC-SHA256(SecretKey, "botenergy-mqtt-auth-v1")`).

Nega derivatsiya, nega yangi ustun emas:
- Broker authn hook'i parolni ochiq ko'radi. Agar parol `SecretKey` ning o'zi bo'lsa,
  envelope HMAC qatlami ma'nosini yo'qotardi.
- Derivatsiya bir tomonlama — brokerga kirgan hujumchi `SecretKey` ni tiklay olmaydi.
- Bazaga ustun qo'shilmagani uchun **EF migratsiya kerak emas**.

| Komponent | Joy |
|---|---|
| Credential derivatsiyasi | `Core/Domain/Helpers/DeviceMqttCredentials.cs` |
| EMQX authn/authz hook | `WebApi/DeviceApi/Controllers/InternalMqttController.cs` |
| Internal endpoint himoyasi | `CommonConfiguration/Filters/InternalSecretFilter.cs` |
| Provisioning endpoint | `AdminApi/DeviceController.MqttCredentials` |
| Permission | `Permissions.DeviceAdminMqttCredentials` (Manage roliga avtomatik seed bo'ladi) |

**ACL:** publish faqat `device/{serial}/*`, subscribe faqat `server/{serial}/*`,
`clientId` serial'ga teng bo'lishi shart. `InternalSecretFilter` secret sozlanmagan
bo'lsa **hamma so'rovni rad etadi** (503) — ochiq qolishdan ko'ra yopiq.

---

## 8. SignalR Redis backplane

`SessionApi/Program.cs` — `Redis:ConnectionString` berilgan bo'lsa
`AddStackExchangeRedis` ulanadi (`AbortOnConnectFail=false`).

Backplane'siz ikkinchi replika **jimgina** noto'g'ri ishlardi: telemetriya
1-instansiyaga keladi, mobil klient 2-instansiyaga ulangan — xabar yetib bormaydi
va hech qanday xato chiqmaydi.

---

## 9. OTP stateless bo'ldi

`IOtpService` in-memory singleton edi — AuthApi 2-replikaga chiqqanda login tasodifiy ishlamay qolardi.

- `Redis/RedisOtpService` — TTL Redis'da, urinishlar `HINCRBY` bilan **atomik**
  (parallel ikki urinish limitni chetlab o'ta olmaydi)
- `Redis/ResilientOtpService` — Redis primary + in-memory fallback (boshqa `Resilient*` store'lar bilan bir xil naqsh)

---

## 10. Migrator — `WebApi/Migrator/`

Yangi konsol ilova. Migratsiya va seed'ni **bir marta, servislardan oldin** qo'llaydi.

```
(argumentsiz)   migratsiya + seed
--migrate-only  faqat migratsiya
--seed-only     faqat seed
--list          holatni ko'rsatadi, hech narsa o'zgartirmaydi
```

Exit kodi 1 — CI qadamni fail qiladi va servislar **umuman yangilanmaydi**.
`ApplyMigrationsAsync` endi `Migrate:AutoApply` ni tekshiradi (Production'da false).

---

## 11. Docker

| Fayl | Izoh |
|---|---|
| `deploy/Dockerfile` | **Yagona parametrlangan** (`--build-arg SERVICE=...`). Multi-stage, non-root (uid 1001), csproj-first layer caching |
| `.dockerignore` | bin/obj/git/docs — build konteksti yengil |

> **Dizayndan chetlanish:** hujjatda har servisga alohida Dockerfile ko'rsatilgan edi.
> 9 ta deyarli bir xil fayl muqarrar ravishda bir-biridan uzoqlashadi (biriga base image
> yangilanadi, boshqasiga yo'q). Bitta parametrlangan fayl tanlandi.

---

## 12. `deploy/` infratuzilma konfiguratsiyasi

```
deploy/
├── Dockerfile                          parametrlangan build
├── docker-compose.yml                  9 servis + postgres/redis/emqx/rabbitmq
├── docker-compose.observability.yml    LGTM + exporterlar
├── botenergy.env.example               barcha env var'lar (sirlarsiz)
├── nginx/botenergy.conf                TLS, rate limit, /api /hubs /mqtt, stub_status
├── nginx/snippets/proxy-common.conf    X-Forwarded-*, timeout, next_upstream
├── emqx/emqx.conf                      TLS 8883 + WS 8083, HTTP authn/authz, no_match=deny
├── prometheus/prometheus.yml           8 servis + emqx/pg/redis/nginx/node/blackbox
├── prometheus/rules/botenergy.yml      18 alert (infra + API + MQTT)
├── prometheus/alertmanager.yml         Telegram, critical/warning marshrutlash
├── loki/{loki.yml, promtail.yml}       JSON log parse, 30 kun retention
├── tempo/tempo.yml                     OTLP, 7 kun, service graph
├── grafana/provisioning/datasources/   Loki→Tempo derived field
├── rabbitmq/{definitions.json, rabbitmq.conf}   topic/DLX/retry, quorum queue
└── scripts/
    ├── server-bootstrap.sh             Docker, ufw, SSH, fail2ban
    ├── cert-deploy-hook.sh             LE → EMQX + Nginx
    ├── backup.sh                       pg_dump + Redis + GPG + retention
    └── restore-drill.sh                backup'ni izolyatsiyada tiklab tekshiradi
```

**Tarmoq segmentatsiyasi:** `be-edge` (nginx, emqx) / `be-app` (internal) / `be-data` (internal).
Hech bir API'ga `ports:` berilmagan — `docker -p` ufw qoidalarini aylanib o'tadi.

**Redis `noeviction`:** `allkeys-lru` bo'lsa xotira to'lganda Redis MQTT replay
counter'ini jimgina o'chirib replay himoyasini buzardi.

---

## 13. CI/CD qayta yozildi

Eski: prod mashinada `dotnet publish`, `rm -rf`, 7 servisni restart, health tekshiruvi
yo'q, rollback yo'q.

Yangi (`.github/workflows/deploy.yml`):

```
build (ubuntu-latest, 9 ta parallel) → GHCR
  ↓
migrate (self-hosted, BITTA marta, --list keyin apply)
  ↓
deploy (rolling, --wait health gate)
  ↓
smoke test (health 200, protected route 401, barcha konteyner healthy)
  ↓ fail bo'lsa
rollback (.env.prev → docker compose up)
```

`deploy.sh` eskirgan deb belgilandi va ishga tushmaydi (exit 1 + tushuntirish).

---

## Nima qilinmadi va nega

| Element | Sabab |
|---|---|
| EF migratsiya fayllari | Loyiha qoidasi: migratsiyalarni foydalanuvchi `dotnet ef migrations add` bilan o'zi yaratadi. **Bu ishda migratsiya kerak ham bo'lmadi** — entity'larga yangi ustun qo'shilmagan |
| Telemetriya batch writer + partitioning | Alohida telemetriya jadvali/entity'si mavjud emas; uni yaratish sxema o'zgarishi va migratsiya talab qiladi |
| `DeviceMessaging` refaktoringi (transport ajratish) | Katta mexanik ko'chirish; TCP server hali yo'q. Foydasi kelajakda, riski hozir |
| RabbitMQ kod integratsiyasi (MassTransit, outbox) | Real ehtiyoj yo'q (§7.2 triggerlari). Topologiya tayyor, `--profile messaging` ortida |
| RS256 ga o'tish | Alohida o'zgarish; birinchi deploy barqarorlashgandan keyin (`MANUAL_TASKS.md` I5) |
| Grafana dashboard JSON'lari | Metrikalar chiqarilmoqda; dashboardlar import/qo'lda yasaladi (`MANUAL_TASKS.md` H3) |
