# QA Gate — docker compose config (dev combined)

Timestamp: 2026-04-16T00-21
Command: docker compose --env-file .env.example -f docker-compose.yml -f docker-compose.dev.yml config
EXIT_CODE: 0
Output Summary: Clean config output with three services (`openclaw-core`, `openclaw-agent`, `openclaw-dev`). `openclaw-agent` has dev override applied: `extra_hosts: host.docker.internal=host-gateway`. All services validated successfully. `openclaw-dev` service unchanged from baseline.
