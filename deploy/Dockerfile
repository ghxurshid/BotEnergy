# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — barcha servislar uchun YAGONA parametrlangan Dockerfile.
#
# Build VPS'ning o'zida bajariladi (registry yo'q), shuning uchun `build` bosqichi
# ataylab SERVICE'ga BOG'LIQ EMAS: u bir marta 9 ta servisni ham publish qiladi.
# 9 ta image qurilganda BuildKit shu bosqichni keshdan oladi — kompilyatsiya
# bir marta ketadi, keyingi 8 tasi faqat fayl nusxalash.
#
# (Ilgari `ARG SERVICE` build bosqichida ishlatilardi — natijada har servis uchun
#  Domain/Application/Persistence qaytadan kompilyatsiya bo'lardi, ya'ni 9 barobar.)
#
# Build (solution ROOT dan):
#   docker build -f deploy/Dockerfile --build-arg SERVICE=SessionApi -t botenergy-sessionapi:dev .
#
# SERVICE qiymatlari: Gateway | Migrator | AuthApi | UserApi | AdminApi |
#                     SessionApi | BillingApi | PaymentApi | DeviceApi
# ─────────────────────────────────────────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Barcha servislar bitta ro'yxatda — pastdagi publish sikli ham shuni ishlatadi.
ENV SERVICES="Gateway Migrator AuthApi UserApi AdminApi SessionApi BillingApi PaymentApi DeviceApi"

# 1-qadam: faqat csproj fayllari → NuGet restore qatlami kod o'zgarganda qayta ishlamaydi.
COPY Core/Domain/Domain.csproj                                     Core/Domain/
COPY Core/Application/Application.csproj                           Core/Application/
COPY Infrastructure/Persistence/Persistence.csproj                 Infrastructure/Persistence/
COPY Infrastructure/CommonConfiguration/CommonConfiguration.csproj Infrastructure/CommonConfiguration/
COPY WebApi/Gateway/Gateway.csproj         WebApi/Gateway/
COPY WebApi/Migrator/Migrator.csproj       WebApi/Migrator/
COPY WebApi/AuthApi/AuthApi.csproj         WebApi/AuthApi/
COPY WebApi/UserApi/UserApi.csproj         WebApi/UserApi/
COPY WebApi/AdminApi/AdminApi.csproj       WebApi/AdminApi/
COPY WebApi/SessionApi/SessionApi.csproj   WebApi/SessionApi/
COPY WebApi/BillingApi/BillingApi.csproj   WebApi/BillingApi/
COPY WebApi/PaymentApi/PaymentApi.csproj   WebApi/PaymentApi/
COPY WebApi/DeviceApi/DeviceApi.csproj     WebApi/DeviceApi/

RUN set -eux; \
    for svc in $SERVICES; do \
      dotnet restore "WebApi/$svc/$svc.csproj"; \
    done

# 2-qadam: manba kodi.
COPY Core/           Core/
COPY Infrastructure/ Infrastructure/
COPY WebApi/         WebApi/

# 3-qadam: hammasini bir yurishda publish qilish.
# Umumiy loyihalar (Domain/Application/Persistence/CommonConfiguration) birinchi
# servisda kompilyatsiya bo'ladi, qolganlari MSBuild inkremental keshidan oladi.
RUN set -eux; \
    for svc in $SERVICES; do \
      dotnet publish "WebApi/$svc/$svc.csproj" \
        -c Release -o "/out/$svc" --no-restore /p:UseAppHost=false; \
    done

# ─────────────────────────────────────────────────────────────────────────────
# Runtime. aspnet image ishlatiladi (Migrator ham — CommonConfiguration
# Microsoft.AspNetCore.App framework reference'iga bog'liq).
#
# Bu bosqichgacha bo'lgan hamma narsa 9 ta image uchun bir xil va keshlanadi.
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG SERVICE
ENV SERVICE_DLL=${SERVICE}.dll

# curl — compose healthcheck uchun. Boshqa hech narsa qo'shilmaydi (hujum yuzasi).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r botenergy && useradd -r -g botenergy -u 1001 botenergy

WORKDIR /app
# Faqat shu servisning chiqishi olinadi — qolgan 8 tasi image'ga tushmaydi.
COPY --from=build --chown=botenergy:botenergy /out/${SERVICE} ./

# root'dan ishlamaydi — konteyner ichida RCE bo'lsa ham imtiyoz cheklangan qoladi.
USER botenergy

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# ENTRYPOINT'da ENV kengaytirilishi uchun shell shakli kerak.
ENTRYPOINT exec dotnet /app/$SERVICE_DLL
