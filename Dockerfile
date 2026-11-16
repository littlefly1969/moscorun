# syntax=docker/dockerfile:1
# Immagine unica: API ASP.NET Core + cruscotto React statico. Multi-architettura (amd64/arm64)
# con compilazione nativa sulla piattaforma di build (niente emulazione QEMU per SDK e Node).

FROM --platform=$BUILDPLATFORM node:26-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY contracts/ /src/contracts/
COPY web/ ./
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
ARG TARGETARCH
ARG VERSION=0.0.0-dev
ARG REVISION=unknown
ENV CI=true DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src/backend
COPY backend/ ./
COPY contracts/ /src/contracts/
RUN dotnet publish src/MoscoRun.Api/MoscoRun.Api.csproj \
      --configuration Release \
      --arch "$TARGETARCH" \
      --no-self-contained \
      --output /app \
      -p:Version="${VERSION%%-*}" \
      -p:InformationalVersion="$VERSION+$REVISION" \
      -p:OpenApiGenerateDocuments=false
COPY --from=web /src/web/dist /app/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
ARG VERSION=0.0.0-dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="MoscoRun" \
      org.opencontainers.image.description="Assistente personale per allenamenti e forma fisica" \
      org.opencontainers.image.source="https://github.com/littlefly1969/moscorun" \
      org.opencontainers.image.version="$VERSION" \
      org.opencontainers.image.revision="$REVISION" \
      org.opencontainers.image.licenses="UNLICENSED"
WORKDIR /app
COPY --from=api /app ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x4B
# Utente non root predefinito delle immagini chiseled (uid 1654); filesystem dell'app in sola lettura.
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --start-interval=2s --retries=3 \
  CMD ["dotnet", "/app/MoscoRun.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "/app/MoscoRun.Api.dll"]
