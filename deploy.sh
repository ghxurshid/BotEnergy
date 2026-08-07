#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# ESKIRGAN — bu skript endi ishlatilmaydi.
#
# Ilgari nima qilardi: prod mashinada `dotnet publish`, so'ng 7 ta servis
# katalogini `rm -rf` qilib qayta nusxalash va systemd unit'larni restart qilish.
#
# Nega olib tashlandi:
#   • Build prod mashinada bajarilardi — deploy paytida CPU/RAM cho'qqisi va
#     prodda .NET SDK talab qilinardi.
#   • Har push'da 7 ta servis ham qayta quriladi va restart qilinardi.
#   • `rm -rf` dan keyin nusxalashgacha bo'lgan oynada fayllar yo'q edi;
#     ROLLBACK IMKONI YO'Q edi.
#   • Health tekshiruvi yo'q — servis ko'tarilmasa ham "✅ muvaffaqiyatli" deyilardi.
#
# Yangi oqim: .github/workflows/deploy.yml
#   build (CI runner) → GHCR → migration (bir marta) → rolling update
#   → health gate → smoke test → kerak bo'lsa avtomatik rollback
#
# Qo'lda deploy qilish kerak bo'lsa (serverda):
#   cd /opt/botenergy
#   echo "TAG=<git-sha>" > .env
#   docker compose pull && docker compose up -d --wait
#
# To'liq tavsif: docs/PRODUCTION_ARCHITECTURE.md §13
# ─────────────────────────────────────────────────────────────────────────────

echo "❌ deploy.sh eskirgan va o'chirilgan."
echo ""
echo "   Deploy endi GitHub Actions orqali: .github/workflows/deploy.yml"
echo "   Qo'lda: cd /opt/botenergy && docker compose pull && docker compose up -d --wait"
echo ""
echo "   Batafsil: docs/PRODUCTION_ARCHITECTURE.md §13, docs/MANUAL_TASKS.md"
exit 1
