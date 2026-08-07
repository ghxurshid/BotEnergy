# BotEnergy — Qo'lda bajarilishi kerak bo'lgan vazifalar

> Bu ro'yxatdagi ishlarni kod orqali bajarib bo'lmaydi: ular server kirish huquqi,
> DNS boshqaruvi, tashqi provayder hisoblari, fizik qurilmalar yoki sizning
> qaroringizni talab qiladi.
>
> Kod tomonidagi barcha ishlar bajarilgan — `docs/IMPLEMENTED.md` ga qarang.
> Har bir vazifada: **nega kerak**, **aniq buyruqlar**, **qabul mezoni** bor.
>
> Holat: `TODO` → `IN PROGRESS` → `DONE`
> Oxirgi yangilanish: 2026-08-07

---

## Umumiy holat

| Guruh | Vazifalar | DONE |
|---|---|---|
| A. Favqulodda xavfsizlik | 5 | 0 |
| B. Domen, DNS, TLS | 4 | 0 |
| C. Server tayyorlash | 4 | 0 |
| D. CI/CD va registry | 3 | 0 |
| E. EMQX va qurilmalar | 5 | 0 |
| F. Klientlar (mobil, simulyator) | 3 | 0 |
| G. Backup va tiklash | 3 | 0 |
| H. Monitoring | 3 | 0 |
| I. Qaror talab qiladigan masalalar | 5 | 0 |
| **Jami** | **35** | **0** |

> ⚠️ **Bloklovchi bog'liqlik:** `A2` bajarilmaguncha production'ga deploy qilmang.
> `Configuration.Production.json` endi `Env_*` placeholder tutadi — env fayl
> to'ldirilmasa servislar **ataylab** ishga tushmaydi (fail-fast). Bu xato emas:
> sozlanmagan secret bilan ishlashdan ko'ra to'xtab qolgani xavfsizroq.

---

## A. Favqulodda xavfsizlik

Bular birinchi navbatda. Har biri hozir ochiq turgan real zaiflik.

### A1. Barcha production sirlarini rotatsiya qilish — `TODO`

**Nega:** `Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.Production.json`
git'da kuzatilgan holda haqiqiy DB paroli, JWT secret, MQTT paroli va internal secret'ni
tutgan (`git ls-files` bilan tasdiqlangan). `Configuration.Development.json` da ham
**xuddi shu DB credential'i** turibdi. Repo'ga kirish huquqi bo'lgan har kim — sobiq
xodim, fork, CI log — bu qiymatlarni ko'rgan bo'lishi mumkin. Fayldan o'chirish
yetarli emas: ular **allaqachon oshkor** deb hisoblanishi kerak.

**Bajarish:**

```bash
# 1) PostgreSQL paroli
#    openssl rand -base64 24
docker exec -it botenergy-postgres psql -U postgres -c \
  "ALTER USER botenergy_user WITH PASSWORD '<YANGI_PAROL>';"

# 2) JWT secret (kamida 64 belgi)
openssl rand -base64 64 | tr -d '\n'
#    DIQQAT: bu qiymat o'zgarganda barcha amaldagi access tokenlar bekor bo'ladi.
#    Refresh tokenlar (opaque GUID, Redis'da) ishlaydi — klientlar avtomatik qayta login qiladi.
#    Eng kam ta'sir uchun tungi past trafik oynasida qiling.

# 3) MQTT backend paroli, internal secret, EMQX cookie/dashboard, Grafana admin
openssl rand -base64 32   # har biri uchun alohida

# 4) Seed admin paroli — kuchli parol, parol menejerida saqlang
```

**Qabul mezoni:** eski parollarning hech biri hech qayerda ishlamaydi; yangi qiymatlar
faqat `/etc/botenergy/botenergy.env` (0600) va parol menejerida.

---

### A2. `/etc/botenergy/botenergy.env` ni yaratish — `TODO`

**Nega:** kod endi sirlarni **faqat** env var'dan oladi. `ResolveSecret` `Env_*`
qiymatni "berilmagan" deb biladi, `GetJwtSecret` esa Production'da exception otadi.

**Bajarish:**

```bash
sudo install -d -m 0700 -o root -g root /etc/botenergy
sudo install -m 0600 -o root -g root deploy/botenergy.env.example /etc/botenergy/botenergy.env
sudo nano /etc/botenergy/botenergy.env     # barcha CHANGE_ME larni to'ldiring
```

⚠️ `INTERNAL_API_SHARED_SECRET` va `InternalApi__SharedSecret` **bir xil qiymat**
bo'lishi shart — birinchisini EMQX konfiguratsiyasi, ikkinchisini DeviceApi o'qiydi.
Mos kelmasa MQTT authn hook ishlamaydi va **hech bir qurilma ulana olmaydi**.

**Qabul mezoni:**
```bash
sudo grep -c CHANGE_ME /etc/botenergy/botenergy.env   # 0 chiqishi kerak
stat -c '%a %U' /etc/botenergy/botenergy.env          # 600 root
```

---

### A3. Git tarixidan sirlarni tozalash — `TODO`

**Nega:** fayl hozir placeholder tutadi, lekin eski commitlarda haqiqiy qiymatlar qoladi.

**Bajarish:**

```bash
# git-filter-repo o'rnatish: pip install git-filter-repo
# DIQQAT: tarix qayta yoziladi — barcha klonlar qayta klon qilinishi kerak.
git filter-repo --path Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.Production.json --invert-paths
git filter-repo --path Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.Development.json --invert-paths
git push --force --all
git push --force --tags
```

Keyin fayllarni yangi holatida qayta qo'shing (ular endi sirsiz).

**Alternativa** (agar tarixni qayta yozish qabul qilinmasa): A1 rotatsiyasini bajaring va
tarixdagi qiymatlarni "bekor qilingan" deb hisoblang. Bu ham himoyalaydi, lekin repo
sirlar tarixini saqlab qoladi.

**Qabul mezoni:** `git log -p --all -- '*Configuration.Production.json' | grep -c 'WsfR5sDcVfABT2F1'` → 0

---

### A4. Dev va Prod uchun alohida DB credential — `TODO`

**Nega:** `Configuration.Development.json` prod bazasining aynan o'sha host/parolini
ishlatadi. Ya'ni har bir dasturchining mashinasida prod bazasiga to'liq yozish huquqi bor,
va lokal test ma'lumotlari prodga tushishi mumkin.

**Bajarish:** alohida dev bazasi ko'taring (lokal Docker Postgres+PostGIS eng oson) va
`Configuration.Development.json` dagi connection string'ni unga yo'naltiring.

```bash
docker run -d --name botenergy-dev-db \
  -e POSTGRES_DB=botenergy_db -e POSTGRES_USER=botenergy_user \
  -e POSTGRES_PASSWORD=devpassword -p 5432:5432 \
  postgis/postgis:16-3.4
```

**Qabul mezoni:** dev config'da `Host=localhost`; prod bazasiga faqat server env orqali kirish.

---

### A5. Git'ga secret tushishining oldini olish — `TODO`

**Nega:** A1–A3 bir martalik tuzatish. Takrorlanmasligi uchun avtomatik to'siq kerak.

**Bajarish:**

```bash
# gitleaks (tavsiya) — pre-commit hook sifatida
# https://github.com/gitleaks/gitleaks
gitleaks protect --staged --redact

# yoki .git/hooks/pre-commit ga qo'shing
```

Qo'shimcha: GitHub repo sozlamalarida **Secret scanning** va **Push protection** ni yoqing
(Settings → Code security).

**Qabul mezoni:** ataylab secretli commit urinishi bloklanadi.

---

## B. Domen, DNS, TLS

### B1. Haqiqiy domenni konfiguratsiyalarga qo'yish — `TODO`

**Nega:** kodda va konfiguratsiyalarda `company.uz` placeholder ishlatilgan.

**O'zgartirilishi kerak bo'lgan joylar:**

| Fayl | Nima |
|---|---|
| `deploy/nginx/botenergy.conf` | `server_name`, `ssl_certificate` yo'llari (3 joy) |
| `deploy/botenergy.env.example` → `/etc/botenergy/botenergy.env` | `Jwt__Issuer`, `Cors__AllowedOrigins__0` |
| `Infrastructure/CommonConfiguration/ConfigurationFile/Configuration.Production.json` | `Jwt.Issuer`, `Cors.AllowedOrigins` |
| `deploy/prometheus/prometheus.yml` | blackbox target |
| `.github/workflows/deploy.yml` | smoke test URL'lari (2 joy) |
| `deploy/scripts/*.sh` | `BOTENERGY_DOMAIN` default |

```bash
# Bir buyruq bilan (avval git commit qiling!)
grep -rl 'company\.uz' --include='*.conf' --include='*.json' --include='*.yml' --include='*.sh' . \
  | xargs sed -i 's/company\.uz/SIZNING-DOMEN.uz/g'
```

**Qabul mezoni:** `grep -r 'company\.uz' .` faqat `docs/` ichida chiqadi.

---

### B2. DNS yozuvlari — `TODO`

| Yozuv | Turi | Qiymat |
|---|---|---|
| `sizning-domen.uz` | A | VPS public IP |
| `www` | CNAME | `sizning-domen.uz` |
| `mqtt` | A | VPS public IP |
| `sizning-domen.uz` | CAA | `0 issue "letsencrypt.org"` |

⚠️ **`mqtt.` alohida yozuvi muhim.** ESP32 firmware'iga apex domen emas, `mqtt.sizning-domen.uz`
yozing. Keyinchalik brokerni alohida serverga ko'chirsangiz DNS'ni o'zgartirasiz —
aks holda butun qurilma parkiga OTA yoki dala tashrifi kerak bo'ladi.

**Qabul mezoni:** `dig +short sizning-domen.uz mqtt.sizning-domen.uz` VPS IP'sini qaytaradi.

---

### B3. Let's Encrypt sertifikati — `TODO`

**Bajarish:**

```bash
sudo apt install -y certbot
sudo certbot certonly --webroot -w /var/www/certbot \
  -d sizning-domen.uz -d www.sizning-domen.uz -d mqtt.sizning-domen.uz \
  --agree-tos -m admin@sizning-domen.uz --no-eff-email
```

Birinchi marta Nginx hali ishlamayotgan bo'lsa `--standalone` ishlating (80-port bo'sh bo'lishi kerak).

**Qabul mezoni:** `/etc/letsencrypt/live/sizning-domen.uz/fullchain.pem` mavjud;
`openssl x509 -in ... -noout -text | grep DNS:` uchala nomni ko'rsatadi.

---

### B4. Sertifikat yangilanish hook'i — `TODO`

**Nega:** Nginx sertifikatni to'g'ridan-to'g'ri o'qiydi, lekin **EMQX ga alohida
katalogdan mount qilingan**. Hook bo'lmasa 90 kundan keyin sertifikat yangilanadi,
EMQX esa eskisini ishlatishda davom etadi va **barcha qurilmalar ulanolmay qoladi**.

```bash
sudo cp deploy/scripts/cert-deploy-hook.sh /etc/letsencrypt/renewal-hooks/deploy/botenergy.sh
sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/botenergy.sh
sudo certbot renew --dry-run     # hook ishlashini tekshiradi
```

**Qabul mezoni:** `--dry-run` xatosiz o'tadi va `/opt/botenergy/emqx/certs/` da yangi fayllar paydo bo'ladi.

---

## C. Server tayyorlash

### C1. Bootstrap skriptini ishga tushirish — `TODO`

```bash
sudo BOTENERGY_DOMAIN=sizning-domen.uz ADMIN_SSH_IP=<ofis-IP> \
  bash deploy/scripts/server-bootstrap.sh
```

Bu: Docker o'rnatadi, kataloglar yaratadi, ufw sozlaydi (22 cheklangan / 80 / 443 / 8883),
SSH'ni qattiqlashtiradi, fail2ban va unattended-upgrades yoqadi.

**Qabul mezoni:** `sudo ufw status verbose` — faqat 4 ta ruxsat; `docker --version` ishlaydi.

---

### C2. Deploy fayllarini serverga joylash — `TODO`

```bash
sudo rsync -a deploy/ /opt/botenergy/ \
  --exclude botenergy.env.example --exclude Dockerfile
cd /opt/botenergy
echo "TAG=latest"                        >  .env
echo "IMAGE_PREFIX=<github-user>/botenergy" >> .env
```

**Qabul mezoni:** `docker compose -f /opt/botenergy/docker-compose.yml config` xatosiz.

---

### C3. Eski systemd unit'larni o'chirish — `TODO`

**Nega:** eski `botenergy-<Service>` unit'lari hali ham 5001–5007 portlarni egallab
turgan bo'lishi mumkin. Ular ishlab turganda Docker konteynerlari port konfliktiga
tushadi va eng yomoni — **eski, himoyalanmagan versiya internetdan ochiq qoladi**.

```bash
for s in AdminApi AuthApi BillingApi DeviceApi PaymentApi UserApi SessionApi; do
  sudo systemctl stop    "botenergy-$s" 2>/dev/null || true
  sudo systemctl disable "botenergy-$s" 2>/dev/null || true
done
sudo ss -tlnp | grep -E ':(500[1-7])'    # bo'sh bo'lishi kerak
```

**Qabul mezoni:** 5001–5007 portlarida hech narsa tinglamaydi.

---

### C4. Eski ochiq portlarni yopishni tasdiqlash — `TODO`

**Nega:** hozir servislar `http://51.38.127.221:500X` da to'g'ridan-to'g'ri ochiq.
Yangi arxitekturada ular faqat Docker internal tarmog'ida bo'lishi kerak.

```bash
# Tashqi mashinadan tekshiring (server ustida EMAS):
nmap -Pn -p 22,80,443,1883,5001-5007,5432,6379,8883,15672,18083 <server-ip>
```

**Qabul mezoni:** faqat 22 (sizning IP'ingizdan), 80, 443, 8883 ochiq. Qolganlari `filtered`/`closed`.

---

## D. CI/CD va registry

### D1. GHCR ruxsatlari — `TODO`

**Bajarish:** GitHub repo → Settings → Actions → General → Workflow permissions →
**Read and write permissions** yoqilgan bo'lsin (workflow `packages: write` so'raydi).

Birinchi build'dan keyin paketlarni ko'rinadigan qiling yoki serverga pull huquqi bering:

```bash
# Serverda (private paketlar uchun)
echo "<GITHUB_PAT_with_read:packages>" | docker login ghcr.io -u <github-user> --password-stdin
```

**Qabul mezoni:** `docker pull ghcr.io/<user>/botenergy-gateway:latest` serverda ishlaydi.

---

### D2. Self-hosted runner — `TODO`

**Nega:** `migrate` va `deploy` job'lari `runs-on: self-hosted` — ular serverga kirish huquqiga muhtoj.
`build` esa `ubuntu-latest` da (prod mashinasi endi build qilmaydi).

```bash
# GitHub → Settings → Actions → Runners → New self-hosted runner
# Runner foydalanuvchisiga docker huquqi kerak:
sudo usermod -aG docker <runner-user>
# rsync uchun parolsiz sudo (faqat kerakli buyruqlarga):
echo '<runner-user> ALL=(ALL) NOPASSWD: /usr/bin/rsync, /bin/cp' | sudo tee /etc/sudoers.d/botenergy-runner
```

**Qabul mezoni:** runner "Idle" holatida ko'rinadi; test workflow o'tadi.

---

### D3. Birinchi deploy — `TODO`

**Tartib muhim:** A2 → C1 → C2 → B3 → D1 → D2 dan **keyin**.

```bash
git push origin master     # workflow avtomatik ishga tushadi
```

Kuzating: `build` (9 image) → `migrate` → `deploy` (rolling) → `smoke test`.

**Qabul mezoni:**
```bash
curl -fsS https://sizning-domen.uz/health                       # 200
curl -s -o /dev/null -w '%{http_code}' https://sizning-domen.uz/api/user/User/Profile   # 401
docker compose -f /opt/botenergy/docker-compose.yml ps          # hammasi healthy
```

---

## E. EMQX va qurilmalar

### E1. EMQX ni ishga tushirish va authn hook'ni tekshirish — `TODO`

**Nega:** broker endi qurilmalar ro'yxatini o'zida saqlamaydi — har CONNECT'da
DeviceApi'ning `/internal/mqtt/authn` endpoint'iga murojaat qiladi.

```bash
# Hook'ni to'g'ridan-to'g'ri sinash (server ustida):
docker exec botenergy-deviceapi curl -sS -X POST \
  http://localhost:5004/internal/mqtt/authn \
  -H 'content-type: application/json' \
  -H "x-internal-secret: $(sudo grep '^InternalApi__SharedSecret=' /etc/botenergy/botenergy.env | cut -d= -f2-)" \
  -d '{"clientId":"TEST-001","username":"TEST-001","password":"notreal"}'
# Kutilgan: {"result":"deny"}
```

**Qabul mezoni:** noto'g'ri parol `deny`, secret'siz so'rov `401` qaytaradi.

---

### E2. Har bir qurilma uchun MQTT credential olish — `TODO`

**Nega:** hozir barcha qurilmalar bitta umumiy `botenergy-device` credential'idan
foydalanadi. Bitta ESP32 ni fizik ochib flash'ni o'qigan odam **istalgan qurilma
nomidan** publish qila oladi.

Yangi model: har bir qurilmaning paroli uning `SecretKey` idan bir tomonlama hosil
qilinadi (`DeviceMqttCredentials`). **Bazaga yangi ustun qo'shilmagan** — migratsiya
kerak emas.

```bash
# Admin panel yoki curl orqali (Manage token bilan):
curl -H "Authorization: Bearer <manage-token>" \
  https://sizning-domen.uz/api/admin/Device/MqttCredentials/<deviceId>
```

Javobda: `username`, `clientId` (= serial), `password` (broker uchun), `secretKey` (HMAC uchun).

**Qabul mezoni:** har bir amaldagi qurilma uchun credential ro'yxati tayyorlangan.

---

### E3. Firmware'ni yangilash (ESP32) — `TODO`

Firmware'da o'zgarishi kerak:

| Nima | Eski | Yangi |
|---|---|---|
| Broker host | IP yoki apex domen | `mqtt.sizning-domen.uz` |
| Port | 8883 | 8883 (o'zgarishsiz) |
| Username | `botenergy-device` (umumiy) | **qurilma serial raqami** |
| Password | umumiy parol | **E2 dan olingan qurilma paroli** |
| ClientId | ixtiyoriy | **serial raqami** (broker teng bo'lishini talab qiladi) |
| Root CA | — | **ISRG Root X1** (leaf sertifikatni PIN QILMANG) |

⚠️ **Leaf sertifikatni pin qilmang.** U har 60 kunda yangilanadi — pin qilinsa birinchi
renewal'da butun park oflayn bo'ladi. Root X1 2035-yilgacha amal qiladi.

**Bosqichma-bosqich o'tkazish:** avval 1–2 test qurilmasi, keyin partiya-partiya.
`botenergy_mqtt_rejected_total` metrikasini kuzating.

**Qabul mezoni:** test qurilmasi yangi credential bilan ulanadi; ACL o'zga topic'ga
publish qilishga urinishni rad etadi.

---

### E4. Umumiy credential'ni o'chirish — `TODO`

**Faqat E3 barcha qurilmalarda tugagandan keyin.**

`InternalMqttController.Authn` da `botenergy-backend` dan boshqa har qanday username
qurilma sifatida tekshiriladi — ya'ni eski umumiy `botenergy-device` login'i **allaqachon
ishlamaydi**. Amalda bu vazifa: eski credential'ga tayangan qurilma qolmaganini tasdiqlash.

```bash
# EMQX dashboard yoki:
docker exec botenergy-emqx emqx ctl clients list | head -50
```

**Qabul mezoni:** ulangan barcha klientlarning username'i serial raqami (yoki `botenergy-backend`).

---

### E5. Simulyator uchun alohida hisob — `TODO`

**Nega:** ACL qoidasi qat'iy — qurilma faqat `device/{o'z-seriali}/*` ga publish qila oladi.
HTML simulyator (`D:\WorkPath\VSCode\Simulators\BotEnergy\device\index.html`) esa
`device/+/telemetry` ga obuna bo'lishga harakat qiladi va **rad etiladi**.

**Qaror kerak:** simulyator prod brokerga ulanishi kerakmi?
- **Yo'q (tavsiya):** simulyator uchun alohida dev/staging broker. Prod ACL o'zgarmaydi.
- **Ha:** `InternalMqttController.Authz` ga `botenergy-simulator` uchun read-only qoida
  qo'shish kerak — va bu prod brokerda debug klienti bo'lishini anglatadi.

**Qabul mezoni:** qaror qabul qilingan va simulyator hujjatlashtirilgan holatda ishlaydi.

---

## F. Klientlar

### F1. Mobil ilova base URL — `TODO`

**Nega:** endpoint sxemasi o'zgardi. Eski: `http://51.38.127.221:5006/api/User/Profile`.
Yangi: `https://sizning-domen.uz/api/user/User/Profile`.

| Servis | Yangi prefiks |
|---|---|
| Auth | `https://domen/api/auth/` |
| User | `https://domen/api/user/` |
| Session | `https://domen/api/session/` |
| Billing | `https://domen/api/billing/` |
| Payment | `https://domen/api/payment/` |
| Device | `https://domen/api/device/` |
| Admin | `https://domen/api/admin/` |
| SignalR | `wss://domen/hubs/session` |

**Qabul mezoni:** ilova faqat bitta base URL bilan sozlanadi; barcha oqimlar ishlaydi.

---

### F2. Admin panel va simulyatorlar URL'lari — `TODO`

`D:\WorkPath\VSCode\Simulators\BotEnergy\{device,app,admin}\index.html` fayllarida
IP:Port manzillar yangi sxemaga o'tkazilsin. MQTT simulyatori uchun: `wss://domen/mqtt`.

**Qabul mezoni:** simulyatorlar yangi manzillar bilan ishlaydi.

---

### F3. CORS ro'yxatini yakunlash — `TODO`

`Cors__AllowedOrigins__0=https://sizning-domen.uz`. Agar admin panel alohida domenda
bo'lsa (`admin.domen.uz`), `Cors__AllowedOrigins__1` qo'shing.

⚠️ Production'da ro'yxat bo'sh qolsa **hech qanday cross-origin ruxsat berilmaydi**
(bu ataylab). Native mobil ilovaga CORS ta'sir qilmaydi — faqat brauzer klientlariga.

**Qabul mezoni:** admin panel brauzerdan CORS xatosiz ishlaydi.

---

## G. Backup va tiklash

### G1. Kunlik backup cron — `TODO`

```bash
sudo cp deploy/scripts/backup.sh /opt/botenergy/scripts/
sudo chmod +x /opt/botenergy/scripts/backup.sh
echo '0 3 * * * root /opt/botenergy/scripts/backup.sh >> /var/log/botenergy-backup.log 2>&1' \
  | sudo tee /etc/cron.d/botenergy-backup
```

**Qabul mezoni:** ertasi kuni `/var/backups/botenergy/` da dump paydo bo'ladi.

---

### G2. Tashqi saqlash + shifrlash — `TODO`

**Nega:** backup faqat o'sha diskda tursa, disk yiqilganda backup ham yo'qoladi.

```bash
# GPG kalit yarating va BOTENERGY_BACKUP_GPG_RECIPIENT ni sozlang
# rclone bilan tashqi saqlashga (backup.sh oxiridagi izohlangan blokni yoqing)
rclone config     # S3/Backblaze/Google Drive
```

**Qabul mezoni:** shifrlangan backup tashqi saqlashda; `rclone ls` ko'rsatadi.

---

### G3. WAL archiving (PITR) — `TODO`

**Nega:** kunlik dump RPO = 24 soat. To'lov tizimi uchun bu ko'p — bir kunlik
tranzaksiyalar yo'qolishi mumkin. WAL archiving RPO'ni ~5 daqiqaga tushiradi.

PostgreSQL konfiguratsiyasida `archive_mode=on`, `archive_command` va tashqi saqlash.
Eng oson yo'l — `pgbackrest` yoki `wal-g`.

**Qabul mezoni:** ixtiyoriy nuqtaga tiklash sinovdan o'tgan.

---

## H. Monitoring

### H1. Observability stack'ni ko'tarish — `TODO`

```bash
cd /opt/botenergy
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d
ssh -L 3000:localhost:3000 ubuntu@<server>   # keyin http://localhost:3000
```

**Qabul mezoni:** Grafana'da Prometheus/Loki/Tempo datasource'lari yashil;
`botenergy_mqtt_received_total` metrikasi ko'rinadi.

---

### H2. Telegram alert — `TODO`

```bash
# @BotFather da bot yarating → token
# Guruh yarating, botni qo'shing, chat_id ni oling:
curl "https://api.telegram.org/bot<TOKEN>/getUpdates"
```

`ALERT_TELEGRAM_BOT_TOKEN` va `ALERT_TELEGRAM_CHAT_ID` ni env faylga qo'ying, so'ng
`deploy/prometheus/alertmanager.yml` dagi `chat_id: 0` ni real qiymatga almashtiring
(Alertmanager konfiguratsiyada env kengaytirishni qo'llab-quvvatlamaydi).

**Qabul mezoni:** test alert Telegram'ga yetib keladi.

---

### H3. Grafana dashboardlari — `TODO`

`deploy/grafana/dashboards/` hozircha bo'sh. Datasource'lar avtomatik ulanadi,
lekin dashboardlarni yaratish kerak (yoki Grafana.com dan import qiling):

| Dashboard | Grafana.com ID |
|---|---|
| ASP.NET Core / OpenTelemetry | 19924 |
| Node Exporter Full | 1860 |
| PostgreSQL | 9628 |
| Redis | 11835 |
| EMQX | 17446 |

BotEnergy'ga xos panel (MQTT rad etishlari, faol sessiyalar) qo'lda yasaladi —
metrikalar allaqachon chiqarilmoqda (`botenergy_*`).

**Qabul mezoni:** 5 ta asosiy dashboard ishlaydi.

---

## I. Qaror talab qiladigan masalalar

Bular texnik ish emas — **sizning qaroringiz**. Har birida tavsiyam bor.

### I1. OpenTelemetry paketidagi advisory — `TODO`

`OpenTelemetry.Exporter.OpenTelemetryProtocol` ning **barcha** versiyalari (1.9 → 1.14)
NuGet'da moderate darajali advisory bilan belgilangan (GHSA-4625-4j76-fww9 va b.).
Upstream'da tuzatilgan versiya hali yo'q.

**Tavsiyam: qoldirish.** Bu eksport komponenti faqat **biz boshqaradigan** Tempo
instansiyasiga ulanadi (internal tarmoq), tashqi ma'lumot qabul qilmaydi. Amaliy risk juda past.

**Alternativa:** OTLP eksportini butunlay olib tashlash (tracing yo'qoladi, lekin
TraceId loglarda qoladi va Prometheus metrikalari ishlaydi).

**Qaror:** ⬜ Qoldirish  ⬜ Olib tashlash

---

### I2. Nginx + YARP yoki faqat YARP — `TODO`

Hozirgi dizayn: Nginx (TLS, certbot, statik, `/mqtt`) + YARP (routing, JWT, audit).

**Tavsiyam: hozirgicha qoldirish.** Nginx certbot integratsiyasi va YARP restart'idan
mustaqil 443 — kichik jamoa uchun katta qulaylik.

**Alternativa:** faqat YARP (`LettuceEncrypt` bilan). Kamroq komponent, lekin har deploy'da
443 bir necha soniya o'lik va WS ulanishlari uziladi.

**Qaror:** ⬜ Nginx + YARP  ⬜ Faqat YARP

---

### I3. RabbitMQ qachon — `TODO`

Kod hozir RabbitMQ ishlatmaydi (compose'da `--profile messaging` ortida turibdi).
`docs/PRODUCTION_ARCHITECTURE.md` §7.2 da kiritish uchun triggerlar sanab o'tilgan.

**Tavsiyam: hozir kiritmaslik.** Birinchi real ehtiyoj (push-bildirishnoma, merchant
webhook yoki og'ir hisobot agregatsiyasi) paydo bo'lganda kiriting. Topologiya tayyor
(`deploy/rabbitmq/definitions.json`).

**Qaror:** ⬜ Kutish  ⬜ Hozir kiritish

---

### I4. Cloudflare va MQTT DDoS — `TODO`

Cloudflare free/pro faqat HTTP(S) ni proxy qiladi. 8883 porti undan o'tmaydi va
origin IP oshkor bo'ladi.

**Tavsiyam:** HTTP uchun Cloudflare yoqing (`sizning-domen.uz` — orange cloud),
`mqtt.sizning-domen.uz` ni grey cloud qoldiring va EMQX `max_conn_rate` ga tayaning.
Bu bosqichda yetarli.

**Qaror:** ⬜ CF + grey MQTT  ⬜ CF Spectrum  ⬜ Faqat provayder himoyasi

---

### I5. JWT RS256 ga o'tish — `TODO`

Hozir HS256: gateway va 7 servis bir xil **imzolash** kalitini biladi. Bittasi buzilsa —
hujumchi istalgan Manage admin nomidan token yasay oladi.

RS256'da faqat AuthApi private key'ni biladi, qolganlari public key bilan tekshiradi.

**Tavsiyam:** birinchi deploy barqarorlashgandan keyin (2–3 hafta ichida) qiling.
Bu alohida kod o'zgarishi — `TokenService` va `AddJwtAuthentication` ni yangilash kerak.

**Qaror:** ⬜ Rejaga kiritish  ⬜ HS256'da qolish

---

## Tavsiya etilgan tartib

```
A1 → A2 → A4 → A5 → A3        (xavfsizlik, git tarixi oxirida)
  ↓
B1 → B2 → B3 → B4             (domen va TLS)
  ↓
C1 → C2 → C3 → C4             (server)
  ↓
D1 → D2 → D3                  (birinchi deploy)
  ↓
E1 → E2 → E3 → E4 → E5        (qurilmalar — bosqichma-bosqich)
  ↓
F1 → F2 → F3                  (klientlar)
  ↓
G1 → G2 → H1 → H2 → H3        (backup va monitoring)
  ↓
I1..I5 → G3                   (qarorlar va PITR)
```

**Minimal production-ready nuqta:** `A1–A5`, `B1–B4`, `C1–C4`, `D1–D3` tugagach
tizim HTTPS ostida, yopiq portlar bilan va rollback'li deploy bilan ishlaydi.
Qolgan guruhlar sifatni oshiradi, lekin ishga tushirishni bloklamaydi.
