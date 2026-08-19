#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — /etc/botenergy/botenergy.env ni sirlar bilan yaratish.
#
# Bir marta ishlatiladi. Mavjud faylni USTIGA YOZMAYDI — bor bo'lsa to'xtaydi.
#
#   sudo BOTENERGY_DOMAIN=sizning-domen.uz bash generate-env.sh
#
# Fayl root:docker 0640 bo'lib yaratiladi — GitHub runner ham o'qiy olishi kerak
# (`docker run --env-file` faylni runner nomidan o'qiydi).
#
# Nega skript: DB paroli 3 joyda, internal secret 2 joyda takrorlanadi.
# Qo'lda yozganda mos kelmay qolishi — eng ko'p uchraydigan xato.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

TARGET="/etc/botenergy/botenergy.env"
DOMAIN="${BOTENERGY_DOMAIN:-}"

if [ "$(id -u)" -ne 0 ]; then
  echo "❌ root sifatida ishga tushiring (sudo)."; exit 1
fi

if [ -z "$DOMAIN" ]; then
  echo "❌ BOTENERGY_DOMAIN berilmagan."
  echo "   Masalan: sudo BOTENERGY_DOMAIN=botenergy.uz bash generate-env.sh"
  exit 1
fi

if [ -e "$TARGET" ]; then
  echo "❌ $TARGET allaqachon mavjud — ustiga yozilmadi."
  echo "   Qayta yaratish kerak bo'lsa avval zaxira qiling:"
  echo "     sudo cp $TARGET $TARGET.bak-\$(date +%F)"
  echo "     sudo rm $TARGET"
  exit 1
fi

# Faqat harf+raqam: parol connection string ichida ham, URL ichida ham
# (DATA_SOURCE_NAME=postgresql://user:PAROL@host) muammosiz o'tsin.
# '/', '+', '@', '=' bo'lsa URL parsing buziladi.
rand() {
  local n="$1" s
  s="$(openssl rand -base64 $(( n * 2 )) | tr -d '\n' | tr -dc 'A-Za-z0-9')"
  printf '%s' "${s:0:n}"
}

DB_PASS="$(rand 32)"
JWT_SECRET="$(rand 96)"
ADMIN_PASS="$(rand 20)"
MQTT_PASS="$(rand 32)"
INTERNAL_SECRET="$(rand 48)"
EMQX_COOKIE="$(rand 32)"
EMQX_DASH_PASS="$(rand 24)"
GRAFANA_PASS="$(rand 24)"

# Katalog ham guruhga ochiq bo'lishi kerak: 0700 bo'lsa fayl huquqi to'g'ri
# bo'lgan holda ham ichiga kirib bo'lmaydi (traverse huquqi yo'q).
if getent group docker >/dev/null 2>&1; then
  install -d -m 0750 -o root -g docker /etc/botenergy
else
  install -d -m 0700 -o root -g root /etc/botenergy
fi
umask 077

cat > "$TARGET" <<EOF
# BotEnergy production env — generate-env.sh tomonidan $(date +%F) da yaratilgan.
# Bu fayl HECH QACHON git'ga tushmasligi kerak. Huquq: 0600 root.

ASPNETCORE_ENVIRONMENT=Production

# ── Ma'lumotlar bazasi ──────────────────────────────────────────────────────
# Parol uch joyda BIR XIL bo'lishi shart (quyida shunday).
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=botenergy_db;Username=botenergy_user;Password=${DB_PASS}
POSTGRES_DB=botenergy_db
POSTGRES_USER=botenergy_user
POSTGRES_PASSWORD=${DB_PASS}
DATA_SOURCE_NAME=postgresql://botenergy_user:${DB_PASS}@postgres:5432/botenergy_db?sslmode=disable

# ── JWT ─────────────────────────────────────────────────────────────────────
# Bu qiymat o'zgarsa barcha access token bekor bo'ladi (refresh ishlaydi).
Jwt__Secret=${JWT_SECRET}
Jwt__Issuer=https://${DOMAIN}
Jwt__ValidateIssuer=true

# ── Default admin (faqat birinchi seed'da) ──────────────────────────────────
Seed__AdminPassword=${ADMIN_PASS}

# ── MQTT (EMQX) ─────────────────────────────────────────────────────────────
Mqtt__Password=${MQTT_PASS}
Mqtt__BrokerHost=emqx
Mqtt__BrokerPort=8883
Mqtt__UseTls=true
Mqtt__SharedSubscriptionGroup=botenergy
EMQX_NODE_COOKIE=${EMQX_COOKIE}
EMQX_DASHBOARD_PASSWORD=${EMQX_DASH_PASS}

# ── Internal servis-servis ──────────────────────────────────────────────────
# Ikkalasi BIR XIL bo'lishi shart: EMQX hook <-> DeviceApi.
INTERNAL_API_SHARED_SECRET=${INTERNAL_SECRET}
InternalApi__SharedSecret=${INTERNAL_SECRET}

# ── Redis ───────────────────────────────────────────────────────────────────
Redis__ConnectionString=redis:6379

# ── CORS / domen ────────────────────────────────────────────────────────────
Cors__AllowedOrigins__0=https://${DOMAIN}

# ── Payme — TODO: hisob ochilgach to'ldiring ────────────────────────────────
Payme__MerchantId=TODO
Payme__Key=TODO

# ── Observability ───────────────────────────────────────────────────────────
Observability__ConsoleJsonLogs=true
Observability__EnableTracing=true
Observability__EnableMetrics=true
Observability__OtlpEndpoint=http://tempo:4317
GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASS}

# ── Telegram alert — TODO: xohlasangiz to'ldiring ───────────────────────────
ALERT_TELEGRAM_BOT_TOKEN=TODO
ALERT_TELEGRAM_CHAT_ID=TODO

# ── Migratsiya / Swagger ────────────────────────────────────────────────────
Migrate__AutoApply=false
Swagger__Enabled=false
EOF

# Huquq: root:docker 0640.
#
# Nega 0600 root EMAS: GitHub runner `docker run --env-file` chaqiradi va bu
# faylni docker CLI RUNNER nomidan o'qiydi, root nomidan emas. 0600 bo'lsa
# har deploy "permission denied" bilan yiqiladi.
#
# Nega bu xavfsizlikni pasaytirmaydi: `docker` guruhi a'zosi allaqachon
# `docker run -v /:/host` orqali butun diskni o'qiy oladi, ya'ni amalda root.
# Guruhga o'qish berish yangi imkoniyat qo'shmaydi.
if getent group docker >/dev/null 2>&1; then
  chown root:docker "$TARGET"
  chmod 0640 "$TARGET"
  PERMNOTE="0640 root:docker (katalog 0750 root:docker)"
else
  chown root:root "$TARGET"
  chmod 0600 "$TARGET"
  PERMNOTE="0600 root — DIQQAT: docker guruhi yo'q, runner o'qiy olmaydi"
fi

echo "✅ $TARGET yaratildi ($(grep -c '=' "$TARGET") ta qiymat, $PERMNOTE)"
echo ""
echo "───────────────────────────────────────────────────────"
echo "  PAROL MENEJERIGA SAQLANG — boshqa joyda ko'rsatilmaydi:"
echo ""
echo "    Admin login paroli : ${ADMIN_PASS}"
echo "    EMQX dashboard     : ${EMQX_DASH_PASS}"
echo "    Grafana admin      : ${GRAFANA_PASS}"
echo "───────────────────────────────────────────────────────"
echo ""
echo "Qolgan sirlar (DB, JWT, MQTT, internal) faqat faylda —"
echo "ularni qo'lda kiritish kerak emas."
echo ""
echo "TODO qolganlari: Payme__MerchantId, Payme__Key, ALERT_TELEGRAM_*"
echo "Ular hozir deploy'ni bloklamaydi."
