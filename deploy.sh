#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — deploy (systemd). VPS'ning o'zida ishlaydi (self-hosted runner).
#
#   ./deploy.sh
#
# Serverdagi mavjud tuzilishga tayanadi — yangi hech narsa o'rnatmaydi:
#   /home/ubuntu/botenergy/<Servis>/   — servis fayllari
#   botenergy-<Servis>.service         — systemd unit'lari
#
# Oqim:
#   publish -> .staging/  (ishlab turgan versiyaga tegilmaydi)
#   migratsiya + seed     (yangi binardan, ALMASHTIRISHDAN OLDIN)
#   kataloglarni almashtirish (eskisi .prev/ ga o'tadi)
#   servislarni birma-bir restart + /health/live kutish
#   xato bo'lsa -> .prev/ dan tiklanadi va servislar qayta ishga tushadi
#
# Eski skriptdan farqi — server tomonda hech narsa talab qilmaydi:
#   • `rm -rf` dan keyingi "fayl yo'q" oynasi yo'q (mv bilan almashtiriladi).
#   • Migratsiya alohida qadam: yiqilsa servislar UMUMAN restart qilinmaydi.
#   • Har restartdan keyin health tekshiriladi; ko'tarilmasa deploy fail + rollback.
#
# Sozlash (env orqali):
#   BOTENERGY_ROOT   — servislar ildizi (default /home/ubuntu/botenergy)
#   BOTENERGY_ENV    — sirlar fayli (default /etc/botenergy/botenergy.env)
#   HEALTH_TIMEOUT   — bitta servisni kutish, sekund (default 60)
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

ROOT="${BOTENERGY_ROOT:-/home/ubuntu/botenergy}"
ENV_FILE="${BOTENERGY_ENV:-/etc/botenergy/botenergy.env}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-60}"

STAGING="$ROOT/.staging"
BACKUP="$ROOT/.prev"

# Serverdagi unit'lar: har biri uchun botenergy-<Servis>.service bo'lishi shart.
# Yangi servis qo'shganda: unit yarating + port_of() ga port qo'shing + shu ro'yxatga
# nom qo'shing. Gateway ro'yxat OXIRIDA — backend'lar ko'tarilgach almashtiriladi.
SERVICES="AuthApi UserApi AdminApi BillingApi PaymentApi DeviceApi SessionApi Gateway"

port_of() {
  case "$1" in
    Gateway)    echo 5008 ;;
    AdminApi)   echo 5001 ;;
    AuthApi)    echo 5002 ;;
    BillingApi) echo 5003 ;;
    DeviceApi)  echo 5004 ;;
    PaymentApi) echo 5005 ;;
    UserApi)    echo 5006 ;;
    SessionApi) echo 5007 ;;
    *) echo "" ;;
  esac
}

SWAPPED=""      # almashtirilgan servislar — rollback shular bo'yicha ishlaydi
DONE=0

log()  { echo "▶ $*"; }
fail() { echo "❌ $*" >&2; exit 1; }

# ── Xatoda avtomatik rollback ───────────────────────────────────────────────
# EXIT trap (ERR emas): `fail` ichidagi `exit` ham shu yerga tushadi.
cleanup() {
  local code=$?
  set +e
  trap - EXIT
  [ "$DONE" = "1" ] && exit 0

  if [ -n "$SWAPPED" ]; then
    echo ""
    echo "⟲ Xato (kod $code) — oldingi versiya tiklanmoqda"
    for svc in $SWAPPED; do
      if [ -d "$BACKUP/$svc" ]; then
        rm -rf "${ROOT:?}/$svc"
        mv "$BACKUP/$svc" "$ROOT/$svc"
        sudo systemctl restart "botenergy-$svc" || true
        echo "  ⟲ $svc"
      fi
    done
    echo "⟲ Rollback yakunlandi."
    echo "   DIQQAT: migratsiya QAYTARILMAYDI — sxema o'zgargan bo'lsa qo'lda ko'ring."
  fi
  exit "$code"
}
trap cleanup EXIT

# ── 0. Old shartlar ─────────────────────────────────────────────────────────
log "Old shartlar"
command -v dotnet >/dev/null 2>&1 || fail "dotnet topilmadi."
[ -d "$ROOT" ] || fail "$ROOT yo'q. BOTENERGY_ROOT ni to'g'ri bering."
[ -w "$ROOT" ] || fail "$ROOT ga yozib bo'lmadi ($(whoami))."
# Sirlar: servislar EnvironmentFile orqali oladi, migratsiya esa shu yerdan o'qiydi.
[ -r "$ENV_FILE" ] || fail "$ENV_FILE o'qilmadi. Namuna: deploy/botenergy.env.example"
grep -q 'CHANGE_ME' "$ENV_FILE" && fail "$ENV_FILE ichida to'ldirilmagan CHANGE_ME bor."
for svc in $SERVICES; do
  systemctl cat "botenergy-$svc.service" >/dev/null 2>&1 \
    || fail "botenergy-$svc.service topilmadi. SERVICES ro'yxatini serverdagi unit nomlariga moslang."
done
# `enable` qilinmagan unit deploy'da restart bo'ladi, lekin serverni qayta
# yuklaganda ko'tarilmaydi — deploy'ni to'xtatmaymiz, faqat ogohlantiramiz.
not_enabled=""
for svc in $SERVICES; do
  systemctl is-enabled --quiet "botenergy-$svc.service" 2>/dev/null \
    || not_enabled="$not_enabled $svc"
done
[ -n "$not_enabled" ] && echo "  ⚠ auto-start yoqilmagan:$not_enabled → sudo systemctl enable$(printf ' botenergy-%s' $not_enabled)"
# Unit'lar sirlarni ko'ryaptimi? `systemctl show` YUKLANGAN holatni beradi — drop-in
# yaratilib `daemon-reload` qilinmagan holat ham shu yerda ushlanadi. Bu tekshiruvsiz
# servis ishga tushadi va Configuration.Production.json'dagi "Env_*" placeholder bilan
# yiqiladi (migratsiya esa allaqachon qo'llanilgan bo'ladi — rollback uni qaytarmaydi).
# Chetlab o'tish kerak bo'lsa: SKIP_ENV_CHECK=1 ./deploy.sh
if [ "${SKIP_ENV_CHECK:-0}" = "1" ]; then env_check=""; else env_check="$SERVICES"; fi
missing_env=""
for svc in $env_check; do
  systemctl show "botenergy-$svc.service" -p EnvironmentFiles 2>/dev/null \
    | grep -qF "$ENV_FILE" || missing_env="$missing_env $svc"
done
if [ -n "$missing_env" ]; then
  echo "  $ENV_FILE quyidagi unit'larga ulanmagan:$missing_env" >&2
  echo "  Tuzatish (bir marta, unit fayllarga tegmasdan):" >&2
  echo "    for s in$missing_env; do" >&2
  echo "      sudo mkdir -p /etc/systemd/system/botenergy-\$s.service.d" >&2
  echo "      printf '[Service]\nEnvironmentFile=$ENV_FILE\n' \\" >&2
  echo "        | sudo tee /etc/systemd/system/botenergy-\$s.service.d/env.conf >/dev/null" >&2
  echo "    done" >&2
  echo "    sudo systemctl daemon-reload" >&2
  fail "Servislar sirlarni ololmaydi — deploy to'xtatildi (hech narsa o'zgarmadi)."
fi
echo "  ✓ dotnet $(dotnet --version), $ROOT yoziladigan, ${SERVICES// /, } unit'lari joyida (sirlar ulangan)"

# ── 1. Publish — .staging ga ────────────────────────────────────────────────
# Ishlab turgan versiya tegilmaydi: bu bosqichda xato bo'lsa hech narsa o'zgarmagan.
log "NuGet restore"
dotnet restore BotEnergy.sln --nologo

log "Publish -> $STAGING"
rm -rf "$STAGING"
for svc in $SERVICES Migrator; do
  echo "  · $svc"
  dotnet publish "WebApi/$svc/$svc.csproj" \
    -c Release -o "$STAGING/$svc" --no-restore --nologo /p:UseAppHost=false
done

# ── 2. Migratsiya + seed — YANGI binardan, almashtirishdan oldin ────────────
# Yiqilsa servislar tegilmagan holda qoladi.
#
# DIQQAT: env faylni `. "$ENV_FILE"` bilan SOURCE QILIB BO'LMAYDI. Qiymatlar
# ichida `;` bor (connection string), shell uni buyruq ajratgichi deb biladi va
# o'zgaruvchi "Host=localhost" da kesilib qoladi. systemd EnvironmentFile'ni
# literal o'qiydi — bu yerda ham xuddi shunday qilamiz.
load_env() {
  local line key val
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$''}"                       # Windows'da tahrirlangan bo'lsa
    case "$line" in ''|'#'*) continue ;; esac
    case "$line" in *=*) ;; *) continue ;; esac
    key="${line%%=*}"
    val="${line#*=}"
    case "$key" in [A-Za-z_]*) ;; *) continue ;; esac
    # systemd qo'shtirnoqlarni olib tashlaydi — biz ham
    case "$val" in
      \"*\") val="${val#\"}"; val="${val%\"}" ;;
      '*') val="${val#'}"; val="${val%'}" ;;
    esac
    export "$key=$val"
  done < "$ENV_FILE"
}

log "Migratsiya + seed"
( load_env; dotnet "$STAGING/Migrator/Migrator.dll" )   || fail "Migratsiya yiqildi — servislar almashtirilmadi."
echo "  ✓ migratsiya o'tdi"

# ── 3. Almashtirish + restart + health gate ─────────────────────────────────
mkdir -p "$BACKUP"
for svc in $SERVICES; do
  port="$(port_of "$svc")"
  log "$svc (port $port)"

  # mv — bir xil fayl tizimida bir zumda. `rm -rf` dagi "fayl yo'q" oynasi yo'q.
  rm -rf "${BACKUP:?}/$svc"
  # Birinchi deploy'da $ROOT/$svc hali yo'q — `[ -d ]` false bo'lsa mv o'tkazib
  # yuboriladi (backup ham bo'lmaydi, rollback'da tiklaydigan narsa yo'q).
  [ -d "$ROOT/$svc" ] && mv "$ROOT/$svc" "$BACKUP/$svc"
  mv "$STAGING/$svc" "$ROOT/$svc"
  SWAPPED="$SWAPPED $svc"

  sudo systemctl restart "botenergy-$svc"

  deadline=$(( SECONDS + HEALTH_TIMEOUT ))
  until curl -fsS --max-time 3 "http://127.0.0.1:$port/health/live" >/dev/null 2>&1; do
    if ! systemctl is-active --quiet "botenergy-$svc"; then
      echo "── $svc loglari ──────────────────────────────────"
      journalctl -u "botenergy-$svc" -n 40 --no-pager 2>&1 | sed 's/^/  /' || true
      echo "──────────────────────────────────────────────────"
      fail "$svc ishga tushmadi (jarayon o'lgan)."
    fi
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "── $svc loglari ──────────────────────────────────"
      journalctl -u "botenergy-$svc" -n 40 --no-pager 2>&1 | sed 's/^/  /' || true
      echo "──────────────────────────────────────────────────"
      fail "$svc ${HEALTH_TIMEOUT}s ichida health bermadi."
    fi
    sleep 2
  done
  echo "  ✓ healthy"
done

# Shu nuqtadan keyin deploy muvaffaqiyatli.
DONE=1

# Migrator'ni ham yangilab qo'yamiz — qo'lda ishlatish uchun asqotadi.
rm -rf "${ROOT:?}/Migrator" && mv "$STAGING/Migrator" "$ROOT/Migrator" 2>/dev/null || true
rm -rf "$STAGING"

echo ""
echo "✅ Deploy yakunlandi. Oldingi versiya: $BACKUP/ (keyingi deploy'da ustiga yoziladi)"
