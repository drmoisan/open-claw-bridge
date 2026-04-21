# syntax=docker/dockerfile:1.7

# Build the ASP.NET Core application in a dedicated SDK stage so the final
# runtime image stays smaller and contains only published output.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the full repository because the project references other src/ projects
# during restore and publish.
COPY . .
RUN dotnet restore ./src/OpenClaw.Core/OpenClaw.Core.csproj
RUN dotnet publish ./src/OpenClaw.Core/OpenClaw.Core.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    /p:UseAppHost=false

# Start from the smaller ASP.NET runtime image for the container that will
# actually run OpenClaw.Core.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Allow the container UID and GID to be overridden to match the host when the
# bind-mounted data directory needs predictable ownership.
ARG APP_UID=1654
ARG APP_GID=1654

# Install the minimal runtime dependencies used by the entrypoint and health
# check scripts.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create a dedicated non-root account so the application and its writable data
# directories do not run as root inside the container.
RUN if ! getent group app >/dev/null; then groupadd --gid ${APP_GID} app; fi \
    && if ! id -u app >/dev/null 2>&1; then useradd --uid ${APP_UID} --gid ${APP_GID} --home-dir /app --create-home app; fi

WORKDIR /app

# Prepare the persistent data path and runtime socket/state directory before
# switching to the unprivileged application account.
RUN mkdir -p /data /run/openclaw && chown app:app /data /run/openclaw

# Copy the container bootstrap scripts separately so they keep executable
# permissions and remain easy to inspect beside the published app output.
COPY --chown=app:app deploy/docker/entrypoint.sh /app/entrypoint.sh
COPY --chown=app:app deploy/docker/healthcheck.sh /app/healthcheck.sh
RUN chmod +x /app/entrypoint.sh /app/healthcheck.sh

# Bring in the published application from the build stage and hand ownership to
# the non-root runtime user.
COPY --from=build --chown=app:app /app/publish /app/

# Run the service as the dedicated application user and expose the internal web
# port consumed by the compose configuration.
USER app
EXPOSE 8080

# Delegate startup to the repository entrypoint so environment setup and any
# pre-launch checks stay in one place.
ENTRYPOINT ["/app/entrypoint.sh"]
