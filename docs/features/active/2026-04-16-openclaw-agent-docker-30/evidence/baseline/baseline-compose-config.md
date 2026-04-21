# Baseline — docker compose config (production)

Timestamp: 2026-04-16T00-16
Command: docker compose --env-file .env.example -f docker-compose.yml config
EXIT_CODE: 0
Output Summary: Clean config output with one service (`openclaw-core`), one named volume (`openclaw_data`), and default network. Service uses loopback-only port `127.0.0.1:8080`, non-root user `1654:1654`, `read_only: true`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`, and read-only token bind mount.
