# QA Gate — openclaw-core Service Regression Check

Timestamp: 2026-04-18T00-00

EXIT_CODE: IDENTICAL

## Comparison

The `openclaw-core` service block extracted from the P3-T1 rendered config output is semantically identical to the baseline snapshot captured in P0-T5.

**P0-T5 baseline (from docker-compose.yml source):** The block contains all expected fields — `build`, `image`, `container_name`, `init`, `restart`, `user`, `read_only`, `cap_drop`, `security_opt`, `tmpfs`, `environment` (13 env vars), `ports`, `volumes` (named volume + token bind-mount), and `healthcheck`. No fields are missing or added.

**P3-T1 output (rendered openclaw-core block):** All rendered fields match exactly:
- `cap_drop: - ALL`
- `read_only: true`
- `security_opt: - no-new-privileges:true`
- `user: 1654:1654`
- `ports: host_ip: 127.0.0.1, target: 8080, published: "8080"`
- All 13 environment variables with the same resolved values
- Token file bind-mount `read_only: true` at `/run/openclaw/hostadapter.token`
- Named volume `openclaw_data` at `/data`
- `healthcheck: CMD /app/healthcheck.sh, interval 30s, timeout 5s, retries 5, start_period 20s`

**Diff result:** No semantic differences. The `openclaw-core` service was not modified by any v2 change task.
