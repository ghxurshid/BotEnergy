#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# Backup'ni tiklash MASHQI — choraklik bajariladi.
#
# Nega: sinovdan o'tmagan backup — backup emas. Eng ko'p uchraydigan holat —
# backup 6 oy davomida muvaffaqiyatli olinib, incident kunida u ochilmasligi.
#
# Bu skript backup'ni VAQTINCHALIK konteynerga tiklaydi va tekshiradi.
# Ishlaydigan bazaga TEGMAYDI.
#
#   ./restore-drill.sh /var/backups/botenergy/botenergy-db-20260807-030000.dump
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

DUMP_FILE="${1:-}"
DRILL_CONTAINER="botenergy-restore-drill"
DRILL_PASSWORD="drill-$(head -c 8 /dev/urandom | base64 | tr -d '/+=')"

if [ -z "$DUMP_FILE" ] || [ ! -f "$DUMP_FILE" ]; then
  echo "Foydalanish: $0 <dump-fayl>"
  echo "Mavjud backup'lar:"
  ls -lh /var/backups/botenergy/*.dump 2>/dev/null || echo "  (topilmadi)"
  exit 1
fi

cleanup() {
  echo "▶ Tozalash..."
  docker rm -f "$DRILL_CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "▶ Vaqtinchalik PostgreSQL konteyneri ko'tarilmoqda..."
docker run -d --name "$DRILL_CONTAINER" \
  -e POSTGRES_PASSWORD="$DRILL_PASSWORD" \
  -e POSTGRES_USER=drill \
  -e POSTGRES_DB=drill_db \
  postgis/postgis:16-3.4 >/dev/null

echo "▶ Baza tayyor bo'lishini kutilmoqda..."
for i in $(seq 1 30); do
  if docker exec "$DRILL_CONTAINER" pg_isready -U drill -d drill_db >/dev/null 2>&1; then
    break
  fi
  sleep 2
  [ "$i" = "30" ] && { echo "❌ Baza 60 soniyada ko'tarilmadi"; exit 1; }
done

echo "▶ Backup tiklanmoqda..."
docker exec -i "$DRILL_CONTAINER" pg_restore \
  -U drill -d drill_db --no-owner --no-privileges \
  < "$DUMP_FILE" 2>&1 | grep -v "^pg_restore: warning" || true

echo "▶ Tekshiruv: asosiy jadvallarda qatorlar bormi?"
FAILED=0
for table in "auth.customer_users" "auth.platform_users" "app.devices" "app.stations" "app.sessions"; do
  COUNT=$(docker exec "$DRILL_CONTAINER" psql -U drill -d drill_db -tAc \
    "SELECT count(*) FROM $table" 2>/dev/null || echo "XATO")
  if [ "$COUNT" = "XATO" ]; then
    echo "  ❌ $table — o'qib bo'lmadi"
    FAILED=1
  else
    echo "  ✓ $table: $COUNT qator"
  fi
done

echo "▶ Tekshiruv: PostGIS extension va koordinatalar?"
docker exec "$DRILL_CONTAINER" psql -U drill -d drill_db -tAc \
  "SELECT count(*) FROM app.stations WHERE coordinates IS NOT NULL" \
  && echo "  ✓ PostGIS ustunlari o'qildi" \
  || { echo "  ❌ PostGIS muammosi"; FAILED=1; }

if [ "$FAILED" = "1" ]; then
  echo ""
  echo "❌ MASHQ MUVAFFAQIYATSIZ — backup ishonchsiz. Darhol tekshiring."
  exit 1
fi

echo ""
echo "✅ Mashq muvaffaqiyatli. Backup tiklanadigan holatda."
echo "   Sana: $(date '+%Y-%m-%d %H:%M')  Fayl: $DUMP_FILE"
