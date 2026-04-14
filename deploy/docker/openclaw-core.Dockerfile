# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY . .
RUN dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj
RUN dotnet publish ./src/OpenClaw.Core/OpenClaw.Core.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ARG APP_UID=1654
ARG APP_GID=1654

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN if ! getent group app >/dev/null; then groupadd --gid ${APP_GID} app; fi \
    && if ! id -u app >/dev/null 2>&1; then useradd --uid ${APP_UID} --gid ${APP_GID} --home-dir /app --create-home app; fi

WORKDIR /app

RUN mkdir -p /data /run/openclaw && chown app:app /data /run/openclaw

COPY --chown=app:app deploy/docker/entrypoint.sh /app/entrypoint.sh
COPY --chown=app:app deploy/docker/healthcheck.sh /app/healthcheck.sh
RUN chmod +x /app/entrypoint.sh /app/healthcheck.sh

COPY --from=build --chown=app:app /app/publish /app/

USER app
EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
