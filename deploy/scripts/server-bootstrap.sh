#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — yangi Ubuntu serverni production uchun tayyorlash.
#
# Bir marta ishlatiladi. Idempotent: qayta ishlatilsa zarar qilmaydi.
#
#   sudo BOTENERGY_DOMAIN=company.uz ADMIN_SSH_IP=1.2.3.4 ./server-bootstrap.sh
#
# Bu skript SIRLARNI o'zi yaratmaydi va DNS'ni sozlamaydi —
# ular docs/MANUAL_TASKS.md dagi qadamlar.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

DOMAIN="${BOTENERGY_DOMAIN:-company.uz}"
ADMIN_SSH_IP="${ADMIN_SSH_IP:-}"

if [ "$(id -u)" -ne 0 ]; then
  echo "❌ root sifatida ishga tushiring (sudo)."
  exit 1
fi

echo "═══ 1/6  Tizim yangilanishlari ═══"
apt-get update -qq
apt-get install -y -qq ca-certificates curl gnupg rsync ufw fail2ban unattended-upgrades

echo "═══ 2/6  Docker ═══"
# Snap docker strict confinement ostida ishlaydi: /etc/botenergy/ va /opt/botenergy/
# ni o'qiy olmaydi — env_file va bind mount'lar ishlamaydi. Rasmiy apt paketi shart.
# `command -v docker` yetarli emas: snap versiyasi ham PATH'da turadi.
if snap list docker >/dev/null 2>&1; then
  echo "❌ Docker snap orqali o'rnatilgan — bu setup bilan ishlamaydi."
  echo "   Konteyner/volume borligini tekshiring, so'ng qo'lda olib tashlang:"
  echo "     docker ps -a && docker volume ls"
  echo "     sudo snap remove --purge docker && hash -r"
  echo "   So'ng shu skriptni qayta ishga tushiring."
  exit 1
fi

if ! dpkg -s docker-ce >/dev/null 2>&1; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi

# userland-proxy=false — port publishing to'g'ridan-to'g'ri iptables orqali,
# ortiqcha proxy jarayoni bo'lmaydi.
cat > /etc/docker/daemon.json <<'EOF'
{
  "iptables": true,
  "ip-forward": true,
  "userland-proxy": false,
  "log-driver": "json-file",
  "log-opts": { "max-size": "50m", "max-file": "3" }
}
EOF
systemctl restart docker

echo "═══ 3/6  Kataloglar ═══"
install -d -m 0755 /opt/botenergy
install -d -m 0755 /opt/botenergy/emqx/certs
install -d -m 0755 /opt/botenergy/scripts
install -d -m 0700 /etc/botenergy
install -d -m 0755 /var/www/certbot
install -d -m 0755 /var/www/botenergy-admin
install -d -m 0700 /var/backups/botenergy
install -d -m 0755 /var/log/nginx

echo "═══ 4/6  Firewall (ufw) ═══"
ufw --force reset >/dev/null
ufw default deny incoming
ufw default allow outgoing

if [ -n "$ADMIN_SSH_IP" ]; then
  ufw allow from "$ADMIN_SSH_IP" to any port 22 proto tcp comment 'SSH admin'
  echo "  ✓ SSH faqat $ADMIN_SSH_IP dan"
else
  ufw allow 22/tcp comment 'SSH (OCHIQ — ADMIN_SSH_IP bering!)'
  echo "  ⚠ SSH hamma uchun ochiq. ADMIN_SSH_IP bilan qayta ishga tushiring."
fi

ufw allow 80/tcp   comment 'ACME + HTTPS redirect'
ufw allow 443/tcp  comment 'HTTPS / WSS / MQTT-over-WSS'
ufw allow 8883/tcp comment 'MQTTS devices'
ufw --force enable

# Docker `-p` ufw'ni aylanib o'tadi. Bizning compose'da faqat 80/443/8883
# publish qilingan, lekin himoyani ikkinchi qatlam sifatida ham qo'yamiz.
cat > /etc/ufw/after.rules.botenergy <<'EOF'
# Docker konteynerlari uchun qo'shimcha filtr (DOCKER-USER zanjiri).
# Faqat ruxsat etilgan portlarga tashqi kirish.
EOF

echo "═══ 5/6  SSH qattiqlashtirish ═══"
sed -i 's/^#*PasswordAuthentication.*/PasswordAuthentication no/'   /etc/ssh/sshd_config
sed -i 's/^#*PermitRootLogin.*/PermitRootLogin no/'                 /etc/ssh/sshd_config
sed -i 's/^#*ChallengeResponseAuthentication.*/ChallengeResponseAuthentication no/' /etc/ssh/sshd_config
systemctl reload ssh || systemctl reload sshd

echo "═══ 6/6  Avtomatik xavfsizlik yangilanishlari ═══"
dpkg-reconfigure -f noninteractive unattended-upgrades
systemctl enable --now fail2ban

echo ""
echo "✅ Server tayyor."
echo ""
echo "Keyingi qadamlar (docs/MANUAL_TASKS.md):"
echo "  1. /etc/botenergy/botenergy.env ni to'ldiring (deploy/botenergy.env.example dan)"
echo "  2. DNS A yozuvlarini shu serverga yo'naltiring"
echo "  3. certbot bilan sertifikat oling ($DOMAIN)"
echo "  4. cert-deploy-hook.sh ni /etc/letsencrypt/renewal-hooks/deploy/ ga qo'ying"
echo "  5. GitHub Actions self-hosted runner o'rnating"
