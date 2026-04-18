# QA Gate — openclaw-core regression check

Timestamp: 2026-04-16T00-22

## Comparison

Baseline source: `evidence/baseline/baseline-openclaw-core-service.md` (P0-T5)
Post-change source: `docker compose --env-file .env.example -f docker-compose.yml config` output (P5-T1)

## Result

The `openclaw-core` service block in the parsed compose config output is semantically identical to the baseline snapshot. All properties match:

- `image`: `openclaw/core:pre-mvp`
- `container_name`: `openclaw-core`
- `user`: `1654:1654`
- `read_only`: `true`
- `cap_drop`: `[ALL]`
- `security_opt`: `[no-new-privileges:true]`
- `ports`: `127.0.0.1:8080:8080`
- `environment`: all 14 keys unchanged
- `volumes`: `openclaw_data:/data` + token bind mount unchanged
- `healthcheck`: unchanged
- `tmpfs`: unchanged
- `init`: `true`
- `restart`: `unless-stopped`

**Verdict: IDENTICAL — zero semantic changes.**
