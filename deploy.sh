#!/bin/bash
set -e

echo "🚀 BotEnergy avtomatik deploy boshlandi – Hammasi bir joyda!"

# Mikroservicelar (faqat *Api papkalari)
SERVICES=("AdminApi" "AuthApi" "BillingApi" "DeviceApi" "PaymentApi" "UserApi")

for SERVICE in "${SERVICES[@]}"; do
  echo "🔨 $SERVICE ni build qilmoqda va /home/ubuntu/botenergy/$SERVICE ga joylashtirmoqda..."

  # WebApi ichiga kirish
  cd "WebApi/$SERVICE" || { echo "❌ WebApi/$SERVICE topilmadi!"; exit 1; }

  # .NET loyiha publish (Release rejimida)
  dotnet publish -c Release -o /tmp/$SERVICE --no-restore

  # Eski versiyani tozalash va yangisini joylashtirish
  sudo rm -rf /home/ubuntu/botenergy/$SERVICE
  sudo mkdir -p /home/ubuntu/botenergy/$SERVICE
  sudo cp -r /tmp/$SERVICE/* /home/ubuntu/botenergy/$SERVICE/
  sudo chown -R ubuntu:ubuntu /home/ubuntu/botenergy/$SERVICE

  # Systemd xizmatini qayta ishga tushirish
  sudo systemctl restart "botenergy-$SERVICE" || echo "⚠️  Xizmat topilmadi (birinchi marta bo‘lsa systemd unit yarating)"

  echo "✅ $SERVICE muvaffaqiyatli deploy qilindi!"

  cd ../..   # Solution ildiziga qaytish
done

echo "🎉 Deploy to‘liq yakunlandi! BotEnergy stansiyasi yangi versiyada – mijozlar uchun hammasi bir joyda va tez!"
