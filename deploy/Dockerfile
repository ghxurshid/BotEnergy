# ─────────────────────────────────────────────────────────────────────────────
# BotEnergy — barcha servislar uchun YAGONA parametrlangan Dockerfile.
#
# Nega bitta fayl: 9 ta servis bir xil qatlam tuzilishiga ega. Alohida Dockerfile'lar
# muqarrar ravishda bir-biridan uzoqlashadi (biriga base image yangilanadi, boshqasiga yo'q).
# Bu yerda o'zgaruvchi faqat loyiha nomi.
#
# Build (solution ROOT dan):
#   docker build -f deploy/Dockerfile --build-arg SERVICE=SessionApi -t botenergy-sessionapi .
#
# SERVICE qiymatlari: Gateway | Migrator | AuthApi | UserApi | AdminApi |
#                     SessionApi | BillingApi | PaymentApi | DeviceApi
# ─────────────────────────────────────────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG SERVICE
WORKDIR /src

# 1-qadam: faqat csproj fayllari → restore qatlami kod o'zgarganda qayta ishlamaydi.
COPY Core/Domain/Domain.csproj                                     Core/Domain/
COPY Core/Application/Application.csproj                           Core/Application/
COPY Infrastructure/Persistence/Persistence.csproj                 Infrastructure/Persistence/
COPY Infrastructure/CommonConfiguration/CommonConfiguration.csproj Infrastructure/CommonConfiguration/
COPY WebApi/${SERVICE}/${SERVICE}.csproj                           WebApi/${SERVICE}/

RUN dotnet restore "WebApi/${SERVICE}/${SERVICE}.csproj"

# 2-qadam: manba kodi.
COPY Core/                                Core/
COPY Infrastructure/CommonConfiguration/  Infrastructure/CommonConfiguration/
COPY Infrastructure/Persistence/          Infrastructure/Persistence/
COPY WebApi/${SERVICE}/                   WebApi/${SERVICE}/

RUN dotnet publish "WebApi/${SERVICE}/${SERVICE}.csproj" \
        -c Release -o /app --no-restore /p:UseAppHost=false

# ─────────────────────────────────────────────────────────────────────────────
# Runtime. aspnet image ishlatiladi (Migrator ham — CommonConfiguration
# Microsoft.AspNetCore.App framework reference'iga bog'liq).
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
COPY --from=build /app ./

# root'dan ishlamaydi — konteyner ichida RCE bo'lsa ham imtiyoz cheklangan qoladi.
RUN chown -R botenergy:botenergy /app
USER botenergy

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# ENTRYPOINT'da ENV kengaytirilishi uchun shell shakli kerak.
ENTRYPOINT exec dotnet /app/$SERVICE_DLL
