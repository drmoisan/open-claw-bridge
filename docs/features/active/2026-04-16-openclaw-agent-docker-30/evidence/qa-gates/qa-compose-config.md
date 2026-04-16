# QA Gate — docker compose config (production)

Timestamp: 2026-04-16T00-20
Command: docker compose --env-file .env.example -f docker-compose.yml config
EXIT_CODE: 0
Output Summary: Clean config output with two services (`openclaw-core`, `openclaw-agent`), one named volume (`openclaw_data`), and default network. Both services validated successfully.

## Service Inventory

- `openclaw-core`: present, unchanged from baseline
- `openclaw-agent`: present, new service

## Security Posture Verification (openclaw-agent)

| Property | Expected | Actual | Status |
|---|---|---|---|
| `read_only` | `true` | `true` | PASS |
| `cap_drop` | `[ALL]` | `[ALL]` | PASS |
| `security_opt` | `[no-new-privileges:true]` | `[no-new-privileges:true]` | PASS |
| `user` | non-root (e.g., `1654:1654`) | `1654:1654` | PASS |
| `ports` host_ip | `127.0.0.1` (loopback only) | `127.0.0.1` | PASS |
| `ports` target | `8181` | `8181` | PASS |
| token mount target | `/run/openclaw/hostadapter.token` | `/run/openclaw/hostadapter.token` | PASS |
| token mount `read_only` | `true` | `true` | PASS |

All security properties verified.
