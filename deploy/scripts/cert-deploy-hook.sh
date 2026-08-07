#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# Let's Encrypt sertifikati yangilangandan keyin EMQX va Nginx ga tarqatish.
#
# O'rnatish:
#   sudo cp deploy/scripts/cert-deploy-hook.sh /etc/letsencrypt/renewal-hooks/deploy/botenergy.sh
#   sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/botenergy.sh
#
# Nega kerak: certbot sertifikatni /etc/letsencrypt/live/ ga yozadi. Nginx uni
# to'g'ridan-to'g'ri o'qiydi (reload yetarli), lekin EMQX konteyneriga alohida
# katalogdan mount qilingan — nusxa ko'chirib, listener'ni qayta ishga tushirish kerak.
# Busiz sertifikat 90 kundan keyin tugaydi va BARCHA qurilmalar ulanolmay qoladi.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

DOMAIN="${BOTENERGY_DOMAIN:-company.uz}"
LIVE_DIR="/etc/letsencrypt/live/${DOMAIN}"
EMQX_CERT_DIR="/opt/botenergy/emqx/certs"

if [ ! -d "$LIVE_DIR" ]; then
  echo "❌ Sertifikat katalogi topilmadi: $LIVE_DIR"
  exit 1
fi

echo "▶ EMQX uchun sertifikat nusxalanmoqda..."
install -d -m 0755 "$EMQX_CERT_DIR"
install -m 0644 "$LIVE_DIR/fullchain.pem" "$EMQX_CERT_DIR/fullchain.pem"
install -m 0644 "$LIVE_DIR/privkey.pem"   "$EMQX_CERT_DIR/privkey.pem"

# EMQX konteyner ichida emqx foydalanuvchisi (uid 1000) sifatida ishlaydi.
chown 1000:1000 "$EMQX_CERT_DIR/fullchain.pem" "$EMQX_CERT_DIR/privkey.pem"
chmod 0600 "$EMQX_CERT_DIR/privkey.pem"

echo "▶ EMQX SSL listener qayta ishga tushirilmoqda..."
if docker ps --format '{{.Names}}' | grep -q '^botenergy-emqx$'; then
  docker exec botenergy-emqx emqx ctl listeners restart ssl:default || {
    echo "⚠ Listener restart ishlamadi — konteynerni qayta ishga tushiring: docker restart botenergy-emqx"
  }
else
  echo "⚠ botenergy-emqx konteyneri ishlamayapti — o'tkazib yuborildi."
fi

echo "▶ Nginx reload..."
if docker ps --format '{{.Names}}' | grep -q '^botenergy-nginx$'; then
  docker exec botenergy-nginx nginx -t && docker exec botenergy-nginx nginx -s reload
else
  echo "⚠ botenergy-nginx konteyneri ishlamayapti — o'tkazib yuborildi."
fi

echo "✅ Sertifikat tarqatildi: $DOMAIN"
