Timestamp: 2026-04-12T23-00-02Z
Command: docker compose -f docker-compose.yml -f docker-compose.dev.yml config
EXIT_CODE: 0
Output Summary: Docker Compose rendered both OpenClaw services successfully with loopback-only published ports, the validation token bind mount, read-only root filesystem for `openclaw-core`, and the expected `host.docker.internal` host mapping for `openclaw-dev`.
Raw Output:
- Resolved `openclaw-core` port binding to `127.0.0.1:8080:8080`.
- Resolved `openclaw-core` token bind source to `C:\Users\DanMoisan\repos\open-claw-bridge\TestResults\qa-csharp\phase7-hostadapter.token`.
- Preserved `read_only: true`, `user: 10001:10001`, and `/data` volume for `openclaw-core`.
- Preserved `host.docker.internal=host-gateway` and the validation token bind mount for `openclaw-dev`.
