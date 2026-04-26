# QA Gate — docker compose config (dev)

Timestamp: 2026-04-18T00-00

Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config

EXIT_CODE: 0

## Output Summary

All three services are present (`openclaw-agent`, `openclaw-core`, `openclaw-dev`). The `openclaw-agent` service includes:
- `ANTHROPIC_API_KEY: ""` confirming env_file is loaded
- `extra_hosts: - host.docker.internal=host-gateway` from the dev overlay
- All security posture properties preserved (read_only, cap_drop, security_opt, user 1654:1654, loopback port)

The `openclaw-core` service is unchanged from baseline. The `openclaw-dev` service is the existing devcontainer service.

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
    (identical to production config — see qa-compose-config.md)
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
    user: 10001:10001
    volumes: [workspace, nuget_cache, openclaw_dev_data, token bind-mount]
    working_dir: /workspace
```
