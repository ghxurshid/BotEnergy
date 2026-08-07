#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy kunlik backup — PostgreSQL logical dump + Redis snapshot.
#
# Cron (har kuni 03:00):
#   0 3 * * * /opt/botenergy/scripts/backup.sh >> /var/log/botenergy-backup.log 2>&1
#
# Bu skript RPO ~24 soatni beradi. RPO ~5 daqiqa uchun WAL archiving (PITR)
# alohida sozlanadi — MANUAL_TASKS.md ga qarang.
#
# DIQQAT: sinovdan o'tmagan backup — backup emas. restore-drill.sh ni choraklik ishlating.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

BACKUP_DIR="${BOTENERGY_BACKUP_DIR:-/var/backups/botenergy}"
RETENTION_DAYS="${BOTENERGY_BACKUP_RETENTION:-30}"
ENV_FILE="/etc/botenergy/botenergy.env"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# GPG bilan shifrlash uchun (ixtiyoriy, lekin tashqi saqlashda MAJBURIY).
GPG_RECIPIENT="${BOTENERGY_BACKUP_GPG_RECIPIENT:-}"

if [ ! -f "$ENV_FILE" ]; then
  echo "❌ $ENV_FILE topilmadi"
  exit 1
fi

# shellcheck disable=SC1090
set -a; source "$ENV_FILE"; set +a

install -d -m 0700 "$BACKUP_DIR"

# ── PostgreSQL ──────────────────────────────────────────────────────────────
PG_FILE="$BACKUP_DIR/botenergy-db-$TIMESTAMP.dump"
echo "▶ PostgreSQL dump..."
docker exec botenergy-postgres pg_dump \
    -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    --format=custom --compress=9 \
    > "$PG_FILE"

# Bo'sh yoki juda kichik dump — jimgina buzilgan backup'ning eng ko'p uchraydigan ko'rinishi.
PG_SIZE=$(stat -c%s "$PG_FILE")
if [ "$PG_SIZE" -lt 10000 ]; then
  echo "❌ Dump juda kichik ($PG_SIZE bayt) — backup ishonchsiz!"
  exit 1
fi
echo "  ✓ $PG_FILE ($(numfmt --to=iec "$PG_SIZE"))"

# ── Redis ───────────────────────────────────────────────────────────────────
echo "▶ Redis snapshot..."
docker exec botenergy-redis redis-cli SAVE > /dev/null
docker cp botenergy-redis:/data/dump.rdb "$BACKUP_DIR/botenergy-redis-$TIMESTAMP.rdb"
echo "  ✓ Redis snapshot"

# ── Shifrlash ───────────────────────────────────────────────────────────────
if [ -n "$GPG_RECIPIENT" ]; then
  echo "▶ Shifrlash ($GPG_RECIPIENT)..."
  for f in "$PG_FILE" "$BACKUP_DIR/botenergy-redis-$TIMESTAMP.rdb"; do
    gpg --batch --yes --encrypt --recipient "$GPG_RECIPIENT" "$f"
    rm -f "$f"
  done
  echo "  ✓ Shifrlandi"
else
  echo "⚠ BOTENERGY_BACKUP_GPG_RECIPIENT sozlanmagan — backup SHIFRLANMAGAN."
  echo "  Tashqi saqlashga (S3) yuborishdan oldin albatta shifrlang."
fi

# ── Retention ───────────────────────────────────────────────────────────────
find "$BACKUP_DIR" -name 'botenergy-*' -mtime "+$RETENTION_DAYS" -delete
echo "✅ Backup yakunlandi. Saqlanadi: $RETENTION_DAYS kun."

# ── Tashqi saqlash ──────────────────────────────────────────────────────────
# Lokal disk yiqilsa backup ham yo'qoladi. Tashqi nusxa MAJBURIY:
# if command -v rclone >/dev/null; then
#   rclone copy "$BACKUP_DIR" remote:botenergy-backups --max-age 25h
# fi
