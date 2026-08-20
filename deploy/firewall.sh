#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — tashqi portlarni boshqarish va tashxis. VPS'ning O'ZIDA ishlaydi.
#
#   ./deploy/firewall.sh status     tashxis: firewall, docker, ichki/tashqi farq
#   ./deploy/firewall.sh open       Gateway portini (5008) tashqariga ochadi
#   ./deploy/firewall.sh lockdown   backend portlarini (5001-5007) tashqaridan yopadi
#
# Nega kerak: arxitektura bo'yicha tashqariga faqat Gateway chiqadi, backend'lar
# esa localhost'da qoladi. Hozir aksi: 5001-5007 ochiq, 5008 yopiq.
#
# `lockdown` ni Gateway tashqaridan ishlayotgani TASDIQLANGANDAN keyin ishlating —
# aks holda simulyatorlar va mobil ilova hech qayerga ulana olmay qoladi.
# ─────────────────────────────────────────────────────────────────────────────
set -uo pipefail

GATEWAY_PORT="${GATEWAY_PORT:-5008}"
BACKEND_PORTS="${BACKEND_PORTS:-5001 5002 5003 5004 5005 5006 5007}"

SUDO=""
[ "$(id -u)" -ne 0 ] && SUDO="sudo"

# Tashqi IP: birinchi navbatda default route orqali, bo'lmasa qo'lda bering (PUBLIC_IP=...)
PUBLIC_IP="${PUBLIC_IP:-$(ip route get 1.1.1.1 2>/dev/null | awk '{print $7; exit}')}"
[ -z "$PUBLIC_IP" ] && PUBLIC_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"

log()  { echo "▶ $*"; }
warn() { echo "  ⚠ $*"; }

# HTTP kodini qaytaradi (ulanmasa "---")
code_of() {
  curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$1" 2>/dev/null || echo "---"
}

# CORS preflight'da Access-Control-Allow-Origin bormi? ("bor" / "yo'q")
# Eski va yangi kodni ajratadi: yangi kodda ro'yxat bo'sh bo'lsa header ALBATTA qaytadi.
cors_of() {
  local n
  n=$(curl -s -i -X OPTIONS "$1/api/PlatformAuth/Login" \
        -H 'Origin: http://firewall-test' -H 'Access-Control-Request-Method: POST' \
        --max-time 5 2>/dev/null | grep -ci 'access-control-allow-origin')
  [ "${n:-0}" -gt 0 ] && echo "bor" || echo "yo'q"
}

cmd_status() {
  log "Tashqi IP: ${PUBLIC_IP:-aniqlanmadi}"
  echo

  log "Firewall"
  if command -v ufw >/dev/null 2>&1; then
    $SUDO ufw status | sed 's/^/  /'
  else
    warn "ufw o'rnatilmagan — iptables INPUT zanjiri:"
    $SUDO iptables -S INPUT 2>/dev/null | head -20 | sed 's/^/  /'
  fi
  echo

  log "Docker (tashqi portlarni ushlab turgan eski konteynerlar bormi?)"
  if command -v docker >/dev/null 2>&1; then
    $SUDO docker ps --format '  {{.Names}}  {{.Image}}  {{.Ports}}' 2>/dev/null | head -20
    local nat
    nat=$($SUDO iptables -t nat -S DOCKER 2>/dev/null | grep -E "dport (${GATEWAY_PORT}|$(echo "$BACKEND_PORTS" | tr ' ' '|'))")
    if [ -n "$nat" ]; then
      warn "Docker NAT qoidalari bizning portlarga tegadi — TASHQI trafik konteynerga buriladi,"
      warn "loopback esa systemd servisiga tushadi. Ikkisi turli jarayon bo'ladi:"
      echo "$nat" | sed 's/^/    /'
    fi
  else
    echo "  docker o'rnatilmagan — bu yo'nalish tekshirilmaydi"
  fi
  echo

  log "Portlar: loopback (systemd servisi) va tashqi IP yonma-yon"
  printf "  %-6s %-12s %-12s %s\n" "port" "127.0.0.1" "$PUBLIC_IP" "izoh"
  for p in $GATEWAY_PORT $BACKEND_PORTS; do
    local in out note=""
    in=$(code_of "http://127.0.0.1:$p/health/live")
    out=$(code_of "http://$PUBLIC_IP:$p/health/live")
    [ "$p" = "$GATEWAY_PORT" ] && note="Gateway — TASHQARIDAN OCHIQ bo'lishi kerak"
    [ "$in" = "200" ] && [ "$out" != "200" ] && [ "$p" != "$GATEWAY_PORT" ] && note="ichkarida ishlayapti, tashqarida yopiq (to'g'ri)"
    [ "$in" != "200" ] && note="servis javob bermayapti!"
    printf "  %-6s %-12s %-12s %s\n" "$p" "$in" "$out" "$note"
  done
  echo

  log "Kod farqi (AuthApi, 5002): CORS preflight header'i"
  local ci co
  ci=$(cors_of "http://127.0.0.1:5002")
  co=$(cors_of "http://$PUBLIC_IP:5002")
  echo "  127.0.0.1:5002 → $ci"
  echo "  $PUBLIC_IP:5002 → $co"
  if [ "$ci" != "$co" ]; then
    warn "FARQ BOR: loopback va tashqi port TURLI jarayonga tushyapti."
    warn "Deploy loopback'ni tekshirgani uchun 'success' deb yozadi, tashqi dunyo esa eski kodni ko'radi."
    warn "Yechim: tashqi portni ushlab turgan eski konteynerni o'chiring (docker rm -f <nom>)."
  fi
}

cmd_open() {
  command -v ufw >/dev/null 2>&1 || {
    echo "❌ ufw yo'q. iptables bilan qo'lda oching:" >&2
    echo "   $SUDO iptables -I INPUT -p tcp --dport $GATEWAY_PORT -j ACCEPT" >&2
    exit 1
  }
  log "Gateway porti ochilmoqda: $GATEWAY_PORT/tcp"
  $SUDO ufw allow "$GATEWAY_PORT/tcp"
  $SUDO ufw status | grep -E "^${GATEWAY_PORT}|Status" | sed 's/^/  /'
  echo
  log "Tashqaridan tekshiruv"
  local out
  out=$(code_of "http://$PUBLIC_IP:$GATEWAY_PORT/health/live")
  if [ "$out" = "200" ]; then
    echo "  ✓ http://$PUBLIC_IP:$GATEWAY_PORT/health/live → 200"
    echo "  ✓ Swagger: http://$PUBLIC_IP:$GATEWAY_PORT/swagger"
  else
    warn "javob: $out — port ochildi, lekin tashqaridan kelmayapti."
    warn "Provayder (OVH) tarmoq firewall'ini ham tekshiring."
  fi
}

cmd_lockdown() {
  command -v ufw >/dev/null 2>&1 || { echo "❌ ufw yo'q — qo'lda yoping." >&2; exit 1; }

  local out
  out=$(code_of "http://$PUBLIC_IP:$GATEWAY_PORT/health/live")
  if [ "$out" != "200" ]; then
    echo "❌ Gateway tashqaridan javob bermayapti (kod: $out)." >&2
    echo "   Avval './deploy/firewall.sh open' bilan 5008 ni ochib, ishlashiga ishonch hosil qiling." >&2
    echo "   Aks holda backend'lar yopilgach hech qanday klient ulana olmaydi." >&2
    exit 1
  fi

  log "Backend portlari tashqaridan yopilmoqda: $BACKEND_PORTS"
  for p in $BACKEND_PORTS; do
    $SUDO ufw delete allow "$p/tcp" >/dev/null 2>&1
    $SUDO ufw deny "$p/tcp" >/dev/null 2>&1
    echo "  ✓ $p"
  done
  echo
  log "Natija"
  for p in $BACKEND_PORTS; do
    printf "  %-6s tashqi: %s\n" "$p" "$(code_of "http://$PUBLIC_IP:$p/health/live")"
  done
  warn "Docker NAT qoidalari ufw'ni chetlab o'tadi — port baribir ochiq qolsa,"
  warn "sabab konteynerda. 'status' buyrug'i buni ko'rsatadi."
}

case "${1:-status}" in
  status)   cmd_status ;;
  open)     cmd_open ;;
  lockdown) cmd_lockdown ;;
  *) echo "Ishlatilishi: $0 {status|open|lockdown}" >&2; exit 1 ;;
esac
