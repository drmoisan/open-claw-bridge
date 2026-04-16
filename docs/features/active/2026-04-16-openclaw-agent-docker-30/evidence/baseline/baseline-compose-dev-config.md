# Baseline — docker compose config (dev combined)

Timestamp: 2026-04-16T00-17
Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config
EXIT_CODE: 0
Output Summary: Clean config output with two services (`openclaw-core`, `openclaw-dev`), three named volumes (`openclaw_data`, `nuget_cache`, `openclaw_dev_data`), and default network. `openclaw-dev` has `extra_hosts: host.docker.internal=host-gateway` and workspace bind mount. Both services use loopback-only ports and read-only token bind mounts.
