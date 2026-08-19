#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — VPS'da Docker tayyorligini tekshirish.
#
# Faqat O'QIYDI, hech narsani o'zgartirmaydi. server-bootstrap.sh dan oldin
# yoki keyin ishlatish mumkin.
#
#   bash check-docker.sh
#
# Chiqish kodi: 0 = tayyor, 1 = muammo bor.
# ─────────────────────────────────────────────────────────────────────────────
set -uo pipefail          # -e YO'Q: bitta tekshiruv yiqilsa ham qolganlari ishlasin

OK=0; FAIL=0; WARN=0
ok()   { echo "  ✅ $1"; OK=$((OK+1)); }
bad()  { echo "  ❌ $1"; FAIL=$((FAIL+1)); }
warn() { echo "  ⚠️  $1"; WARN=$((WARN+1)); }

echo "═══ BotEnergy — Docker tayyorligi ═══"
echo "Server: $(hostname)   User: $(whoami)"
echo ""

# ── 1. Binary ───────────────────────────────────────────────────────────────
echo "1) Docker o'rnatilganmi?"
if ! command -v docker >/dev/null 2>&1; then
  bad "docker topilmadi"
  echo ""
  echo "     O'rnatish: sudo bash deploy/scripts/server-bootstrap.sh"
  echo "     ⚠️  'snap install docker' QILMANG — snap versiyasi confinement"
  echo "        sababli /etc/botenergy/botenergy.env ni o'qiy olmaydi."
  echo ""
  echo "Natija: ❌ Docker yo'q — boshqa tekshiruvlar o'tkazilmadi."
  exit 1
fi
DOCKER_BIN="$(command -v docker)"
ok "docker topildi: $DOCKER_BIN"

# ── 2. Snap emasligini tekshirish ───────────────────────────────────────────
echo ""
echo "2) O'rnatish manbai"
if [[ "$DOCKER_BIN" == /snap/* ]] || readlink -f "$DOCKER_BIN" 2>/dev/null | grep -q '^/snap/'; then
  bad "Docker SNAP orqali o'rnatilgan"
  echo "     Snap docker /etc/botenergy/ ni o'qiy olmaydi (env_file ishlamaydi)."
  echo "     Yechim: sudo snap remove docker && sudo bash deploy/scripts/server-bootstrap.sh"
elif dpkg -l docker-ce >/dev/null 2>&1; then
  ok "docker-ce (rasmiy Docker apt repo) — to'g'ri"
elif dpkg -l docker.io >/dev/null 2>&1; then
  warn "docker.io (Ubuntu paketi) — ishlaydi, lekin versiyasi eski bo'lishi mumkin"
else
  warn "manba aniqlanmadi (qo'lda o'rnatilgan?)"
fi

# ── 3. Versiya ──────────────────────────────────────────────────────────────
echo ""
echo "3) Versiya"
VER="$(docker --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"
if [ -z "$VER" ]; then
  warn "versiyani aniqlab bo'lmadi"
else
  MAJOR="${VER%%.*}"
  if [ "$MAJOR" -ge 24 ]; then
    ok "Docker $VER"
  else
    warn "Docker $VER — 24+ tavsiya etiladi"
  fi
fi

# ── 4. Daemon ishlayaptimi + huquq ──────────────────────────────────────────
echo ""
echo "4) Daemon va huquqlar"
DOCKER_ERR="$(docker info 2>&1 >/dev/null)"
if [ -z "$DOCKER_ERR" ]; then
  ok "docker info ishladi — daemon tirik, huquq bor"
elif echo "$DOCKER_ERR" | grep -qi 'permission denied'; then
  bad "huquq yo'q: '$(whoami)' docker guruhida emas"
  echo "     Yechim: sudo usermod -aG docker $(whoami)  (keyin qayta login)"
elif echo "$DOCKER_ERR" | grep -qiE 'cannot connect|is the docker daemon running'; then
  bad "daemon ishlamayapti"
  echo "     Yechim: sudo systemctl enable --now docker"
else
  bad "docker info xato berdi: $(echo "$DOCKER_ERR" | head -1)"
fi

if systemctl is-enabled docker >/dev/null 2>&1; then
  ok "docker.service avtoyuklanishda yoqilgan"
else
  warn "docker.service avtoyuklanishda yoqilmagan — server restartda ko'tarilmaydi"
  echo "     Yechim: sudo systemctl enable docker"
fi

# ── 5. Compose v2 plugin ────────────────────────────────────────────────────
echo ""
echo "5) Compose plugin"
if docker compose version >/dev/null 2>&1; then
  ok "$(docker compose version | head -1)"
elif command -v docker-compose >/dev/null 2>&1; then
  bad "faqat eski 'docker-compose' (v1) bor — loyiha 'docker compose' (v2) ishlatadi"
  echo "     Yechim: sudo apt-get install -y docker-compose-plugin"
else
  bad "compose plugin yo'q"
  echo "     Yechim: sudo apt-get install -y docker-compose-plugin"
fi

# ── 6. Loyiha uchun qo'shimcha ──────────────────────────────────────────────
echo ""
echo "6) Loyiha talablari"
[ -d /opt/botenergy ]  && ok "/opt/botenergy mavjud"  || warn "/opt/botenergy yo'q (server-bootstrap.sh hali ishlatilmagan)"
if [ -f /etc/botenergy/botenergy.env ]; then
  PERM="$(stat -c '%a %U:%G' /etc/botenergy/botenergy.env)"
  if [ "${PERM% *}" = "640" ]; then
    ok "botenergy.env mavjud ($PERM)"
  else
    warn "botenergy.env huquqi $PERM — 640 root:docker bo'lishi kerak"
    echo "      (runner 'docker run --env-file' uchun o'qiy olishi shart)"
  fi
  if [ -r /etc/botenergy/botenergy.env ]; then
    ok "joriy user ($(whoami)) env faylni o'qiy oladi"
  else
    warn "joriy user env faylni O'QIY OLMAYDI — deploy shu yerda yiqiladi"
  fi
else
  warn "/etc/botenergy/botenergy.env yo'q (generate-env.sh hali ishlatilmagan)"
fi

DISK="$(df -BG --output=avail / 2>/dev/null | tail -1 | tr -dc '0-9')"
if [ -n "$DISK" ]; then
  # Build VPS'da bajariladi: SDK image (~800MB) + BuildKit keshi + haftalik
  # image teglari. 20G tez to'ladi, shuning uchun chegara 30G.
  [ "$DISK" -ge 30 ] && ok "bo'sh disk: ${DISK}G" || warn "bo'sh disk atigi ${DISK}G — build kesh bilan 30G+ kerak"
fi

RAM="$(free -g | awk '/^Mem:/{print $2}')"
[ -n "$RAM" ] && { [ "$RAM" -ge 8 ] && ok "RAM: ${RAM}G" || warn "RAM ${RAM}G — compose'da shared_buffers=2GB, 8G+ tavsiya etiladi"; }

# ── Xulosa ──────────────────────────────────────────────────────────────────
echo ""
echo "═══════════════════════════════════════"
echo "  ✅ $OK    ⚠️  $WARN    ❌ $FAIL"
if [ "$FAIL" -gt 0 ]; then
  echo "  Natija: TAYYOR EMAS — yuqoridagi ❌ larni tuzating."
  exit 1
fi
echo "  Natija: Docker tayyor."
exit 0
