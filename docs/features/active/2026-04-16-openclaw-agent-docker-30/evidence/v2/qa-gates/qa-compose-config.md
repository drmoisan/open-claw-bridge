# QA Gate — docker compose config (production)

Timestamp: 2026-04-18T00-00

Command: docker compose --env-file .env.example -f docker-compose.yml config

EXIT_CODE: 0

## Output Summary

Both services (`openclaw-agent` and `openclaw-core`) are present. The `openclaw-agent` service now includes `ANTHROPIC_API_KEY: ""` in its resolved environment, confirming that `env_file: ./secrets/.env.anthropic` is loaded and the empty placeholder value is picked up.

```yaml
name: openclaw
services:
  openclaw-agent:
    cap_drop:
      - ALL
    container_name: openclaw-agent
    environment:
      ANTHROPIC_API_KEY: ""
      OpenClaw__HostAdapter__BaseUrl: http://host.docker.internal:4319/v1
      OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token
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
networks:
  default:
    name: openclaw_default
volumes:
  openclaw_data:
    name: openclaw_openclaw_data
```

## Security Posture Verification

Verifying `openclaw-agent` service security posture from the parsed compose config output above.

| Property | Required Value | Observed Value | Status |
|---|---|---|---|
| `read_only` | `true` | `read_only: true` | PASS |
| `cap_drop` | `[ALL]` | `cap_drop: - ALL` | PASS |
| `security_opt` | `[no-new-privileges:true]` | `security_opt: - no-new-privileges:true` | PASS |
| `user` | non-root | `1654:1654` | PASS |
| Port binding | `127.0.0.1:<port>:18789` (loopback only) | `host_ip: 127.0.0.1`, `target: 18789`, `published: "18789"` | PASS |
| Token file mount | `/run/openclaw/hostadapter.token` with `read_only: true` | `source: C:\ProgramData\...\adapter.token`, `target: /run/openclaw/hostadapter.token`, `read_only: true` | PASS |
| `env_file` | `./secrets/.env.anthropic` | `ANTHROPIC_API_KEY: ""` present in environment (resolved from env_file) | PASS |

All seven security posture properties are confirmed. No `0.0.0.0` binding is present. The token mount is read-only. The user is non-root (`1654:1654`).
