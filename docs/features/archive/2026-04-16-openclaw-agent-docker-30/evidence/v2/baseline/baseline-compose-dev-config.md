# Baseline — docker compose config (dev)

Timestamp: 2026-04-18T00-00

Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config

EXIT_CODE: 0

## Output Summary

All three services are present (`openclaw-agent`, `openclaw-core`, `openclaw-dev`). The `openclaw-agent` service includes `extra_hosts: host.docker.internal=host-gateway` from the dev overlay. Both `openclaw-agent` and `openclaw-core` are security-hardened. The `openclaw-dev` service uses a different user (`10001:10001`) and does not have `read_only` or `cap_drop`.

```yaml
name: openclaw
services:
  openclaw-agent:
    cap_drop:
      - ALL
    container_name: openclaw-agent
    environment:
      OpenClaw__HostAdapter__BaseUrl: http://host.docker.internal:4319/v1
      OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token
    extra_hosts:
      - host.docker.internal=host-gateway
    healthcheck:
      test:
        - CMD
        - curl
        - -fsS
        - http://127.0.0.1:18789/healthz
      timeout: 5s
      interval: 30s
      retries: 5
      start_period: 30s
    image: ghcr.io/openclaw/openclaw:latest
    init: true
    networks:
      default: null
    ports:
      - mode: ingress
        host_ip: 127.0.0.1
        target: 18789
        published: "18789"
        protocol: tcp
    read_only: true
    restart: unless-stopped
    security_opt:
      - no-new-privileges:true
    tmpfs:
      - /tmp:size=64m,noexec,nosuid,nodev
      - /.openclaw:size=64m,noexec,nosuid,nodev
    user: 1654:1654
    volumes:
      - type: bind
        source: C:\ProgramData\OpenClaw\HostAdapter\adapter.token
        target: /run/openclaw/hostadapter.token
        read_only: true
      - type: bind
        source: C:\Users\DanMoisan\repos\open-claw-bridge\deploy\docker\openclaw-assistant
        target: /workspace
  openclaw-core:
    build:
      context: C:\Users\DanMoisan\repos\open-claw-bridge
      dockerfile: deploy/docker/openclaw-core.Dockerfile
      args:
        BUILD_CONFIGURATION: Release
      target: runtime
    cap_drop:
      - ALL
    container_name: openclaw-core
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://0.0.0.0:8080
      OpenClaw__Defaults__Limit: "100"
      OpenClaw__Defaults__MaxLimit: "250"
      OpenClaw__HostAdapter__BaseUrl: http://host.docker.internal:4319/v1
      OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token
      OpenClaw__Polling__CalendarFutureDays: "30"
      OpenClaw__Polling__CalendarIntervalSeconds: "300"
      OpenClaw__Polling__CalendarPastDays: "14"
      OpenClaw__Polling__MeetingRequestsIntervalSeconds: "60"
      OpenClaw__Polling__MessageLookbackHours: "48"
      OpenClaw__Polling__MessagesIntervalSeconds: "60"
      OpenClaw__Storage__DbPath: /data/openclaw.db
    healthcheck:
      test:
        - CMD
        - /app/healthcheck.sh
      timeout: 5s
      interval: 30s
      retries: 5
      start_period: 20s
    image: openclaw/core:pre-mvp
    init: true
    networks:
      default: null
    ports:
      - mode: ingress
        host_ip: 127.0.0.1
        target: 8080
        published: "8080"
        protocol: tcp
    read_only: true
    restart: unless-stopped
    security_opt:
      - no-new-privileges:true
    tmpfs:
      - /tmp:size=256m,noexec,nosuid,nodev
    user: 1654:1654
    volumes:
      - type: volume
        source: openclaw_data
        target: /data
        volume: {}
      - type: bind
        source: C:\ProgramData\OpenClaw\HostAdapter\adapter.token
        target: /run/openclaw/hostadapter.token
        read_only: true
  openclaw-dev:
    build:
      context: C:\Users\DanMoisan\repos\open-claw-bridge
      dockerfile: .devcontainer/Dockerfile
    command:
      - sleep
      - infinity
    container_name: openclaw-dev
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      OpenClaw__HostAdapter__BaseUrl: http://host.docker.internal:4319/v1
      OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token
      OpenClaw__Storage__DbPath: /data/openclaw.db
    extra_hosts:
      - host.docker.internal=host-gateway
    init: true
    networks:
      default: null
    ports:
      - mode: ingress
        host_ip: 127.0.0.1
        target: 8080
        published: "8080"
        protocol: tcp
    user: 10001:10001
    volumes:
      - type: bind
        source: C:\Users\DanMoisan\repos\open-claw-bridge
        target: /workspace
        bind: {}
      - type: volume
        source: nuget_cache
        target: /home/vscode/.nuget/packages
        volume: {}
      - type: volume
        source: openclaw_dev_data
        target: /data
        volume: {}
      - type: bind
        source: C:\ProgramData\OpenClaw\HostAdapter\adapter.token
        target: /run/openclaw/hostadapter.token
        read_only: true
    working_dir: /workspace
networks:
  default:
    name: openclaw_default
volumes:
  nuget_cache:
    name: openclaw_nuget_cache
  openclaw_data:
    name: openclaw_openclaw_data
  openclaw_dev_data:
    name: openclaw_openclaw_dev_data
```
