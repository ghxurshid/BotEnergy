#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — HTTPS (TLS) sozlash: nginx + Let's Encrypt.
#
# Nima qiladi:
#   1. nginx va certbot o'rnatadi (yo'q bo'lsa);
#   2. domen uchun bepul sertifikat oladi (Let's Encrypt);
#   3. 443-portda nginx'ni Gateway (127.0.0.1:5008) oldiga qo'yadi;
#   4. WebSocket (SignalR) va uzun so'rovlarni to'g'ri uzatadi;
#   5. sertifikatni avtomatik yangilashni tekshiradi.
#
# Ishlatish (serverda, root huquqi bilan):
#   sudo DOMAIN=api.botep.uz EMAIL=botenergyuz@gmail.com ./tls-setup.sh
#
# SHART: DOMAIN uchun DNS A-yozuvi shu serverning IP'siga ko'rsatgan bo'lishi
# kerak, aks holda Let's Encrypt tekshiruvi (HTTP-01) o'tmaydi.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

DOMAIN="${DOMAIN:-}"
EMAIL="${EMAIL:-}"
GATEWAY_PORT="${GATEWAY_PORT:-5008}"

if [[ -z "$DOMAIN" || -z "$EMAIL" ]]; then
  echo "❌ DOMAIN va EMAIL berilishi shart."
  echo "   Misol: sudo DOMAIN=api.botep.uz EMAIL=botenergyuz@gmail.com $0"
  exit 1
fi

if [[ $EUID -ne 0 ]]; then
  echo "❌ root huquqi kerak: sudo bilan ishga tushiring."
  exit 1
fi

echo "▶ 1/6 DNS tekshiruvi: $DOMAIN"
SERVER_IP="$(curl -fsS --max-time 10 https://api.ipify.org || echo '')"
DOMAIN_IP="$(getent hosts "$DOMAIN" | awk '{print $1}' | head -1 || echo '')"

if [[ -z "$DOMAIN_IP" ]]; then
  echo "❌ $DOMAIN uchun DNS yozuvi topilmadi. A-yozuvni $SERVER_IP ga yo'naltiring."
  exit 1
fi

if [[ -n "$SERVER_IP" && "$DOMAIN_IP" != "$SERVER_IP" ]]; then
  echo "⚠️  DNS $DOMAIN → $DOMAIN_IP, server IP esa $SERVER_IP."
  echo "   Tarqalish tugamagan bo'lishi mumkin. Davom etamizmi? (Ctrl+C — to'xtatish)"
  sleep 10
fi
echo "✅ DNS joyida: $DOMAIN → $DOMAIN_IP"

echo "▶ 2/6 nginx va certbot"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq nginx certbot python3-certbot-nginx
echo "✅ o'rnatildi"

echo "▶ 3/6 Portlar (80/443) ochilmoqda"
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q "Status: active"; then
  ufw allow 80/tcp  >/dev/null
  ufw allow 443/tcp >/dev/null
  echo "✅ ufw: 80 va 443 ochildi"
else
  echo "ℹ️  ufw faol emas — portlar allaqachon ochiq deb hisoblanadi"
fi

echo "▶ 4/6 nginx konfiguratsiyasi"
cat > /etc/nginx/sites-available/botenergy <<NGINX
# BotEnergy — barcha API trafigi shu yerdan o'tadi (Gateway'ga uzatiladi).
server {
    listen 80;
    listen [::]:80;
    server_name $DOMAIN;

    # Let's Encrypt tekshiruvi uchun joy; qolgani HTTPS'ga yo'naltiriladi.
    location /.well-known/acme-challenge/ { root /var/www/html; }
    location / { return 301 https://\$host\$request_uri; }
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    http2 on;
    server_name $DOMAIN;

    # Sertifikat yo'llari certbot tomonidan qo'shiladi/yangilanadi.

    # Faqat zamonaviy protokollar — TLS 1.0/1.1 o'chirilgan.
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 1d;

    # Xavfsizlik sarlavhalari
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header Referrer-Policy "no-referrer" always;

    # Karta ma'lumotlari o'tadi — so'rov tanasi logga yozilmaydi.
    client_max_body_size 20m;

    location / {
        proxy_pass http://127.0.0.1:$GATEWAY_PORT;
        proxy_http_version 1.1;

        # Haqiqiy mijoz IP'si — rate limiting shunga tayanadi.
        proxy_set_header Host              \$host;
        proxy_set_header X-Real-IP         \$remote_addr;
        proxy_set_header X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;

        # SignalR (WebSocket) uchun
        proxy_set_header Upgrade    \$http_upgrade;
        proxy_set_header Connection "upgrade";

        proxy_connect_timeout 30s;
        proxy_send_timeout    120s;
        proxy_read_timeout    120s;
        proxy_buffering       off;
    }
}
NGINX

ln -sf /etc/nginx/sites-available/botenergy /etc/nginx/sites-enabled/botenergy
rm -f /etc/nginx/sites-enabled/default
mkdir -p /var/www/html

echo "▶ 5/6 Sertifikat olinmoqda (Let's Encrypt)"
certbot --nginx -d "$DOMAIN" --non-interactive --agree-tos -m "$EMAIL" --redirect

nginx -t
systemctl reload nginx
echo "✅ nginx qayta yuklandi"

echo "▶ 6/6 Tekshiruv"
sleep 2
CODE="$(curl -s -o /dev/null -w '%{http_code}' "https://$DOMAIN/health/live" || echo 000)"
echo "   https://$DOMAIN/health/live → $CODE"

systemctl list-timers | grep -q certbot \
  && echo "✅ Sertifikat avtomatik yangilanadi (certbot timer)" \
  || echo "⚠️  certbot timer topilmadi — 'systemctl enable --now certbot.timer'"

cat <<DONE

─────────────────────────────────────────────────────────────
✅ HTTPS tayyor: https://$DOMAIN

Keyingi qadamlar:
  1. Ilovalarda ApiConfig'ni o'zgartiring:
       scheme = 'https';  host = '$DOMAIN';  viaGateway = true;
  2. Ilovalarni qayta yig'ing va tarqating.
  3. Hamma ilova HTTPS'ga o'tgach ichki portlarni yoping:
       sudo ./firewall.sh lockdown
  4. Telegram webhook (ixtiyoriy) endi ishlatilishi mumkin.
─────────────────────────────────────────────────────────────
DONE
