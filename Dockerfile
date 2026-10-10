# syntax=docker/dockerfile:1
# Multi-stage image: SDK builds IoTPlatform.Web, ASP.NET 8 runtime hosts it.
# HTTP listens on 0.0.0.0:5288; Embedded MQTT already binds 0.0.0.0:1883.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

RUN dotnet publish src/IoTPlatform.Web/IoTPlatform.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is used by HEALTHCHECK and `docker compose` health checks
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:5288 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__IoT="Data Source=/app/Data/iot.db;Cache=Shared;Default Timeout=30" \
    ConnectionStrings__IoTProvider=SQLite \
    DOTNET_EnableDiagnostics=0 \
    NEWLIFE_DATAPATH=/app/Data \
    NEWLIFE_LOGPATH=/app/Log

COPY --from=build /app/publish .
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN sed -i 's/\r$//' /app/docker-entrypoint.sh \
    && chmod +x /app/docker-entrypoint.sh \
    && mkdir -p /app/Data /app/Log \
    && chown -R ${APP_UID:-1654}:${APP_UID:-1654} /app/Data /app/Log /app/docker-entrypoint.sh

EXPOSE 5288 1883

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=5 \
    CMD curl -fsS http://127.0.0.1:5288/health || exit 1

ENTRYPOINT ["/app/docker-entrypoint.sh"]
CMD ["dotnet", "IoTPlatform.Web.dll"]
