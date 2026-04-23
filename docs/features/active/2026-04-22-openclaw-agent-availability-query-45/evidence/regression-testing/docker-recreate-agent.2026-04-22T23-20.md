# P5-T2 — Recreate openclaw-agent container (operator runbook + evidence template)

Timestamp: 2026-04-22T23-20
Command: `docker compose up -d --force-recreate openclaw-agent`

## Status

PENDING_MANUAL_VERIFY

## Operator Runbook

This step must be executed by the operator after P5-T1 completes successfully.

### Steps

1. From the repository root, run:

   ```
   docker compose up -d --force-recreate openclaw-agent
   ```

2. Verify that the `TZ` variable is resolved inside the new container:

   ```
   docker compose exec openclaw-agent printenv TZ
   ```

3. The second command must print `America/New_York` exactly.

4. Record in this artifact:

   ```
   EXIT_CODE: <0 or actual exit code of the up command>
   TZ inside container: <output of printenv TZ>
   Output Summary: <observed container id, health status>
   Completed: <ISO-8601 timestamp>
   ```

### Expected Outcome

- `docker compose up` exit code 0.
- `printenv TZ` returns `America/New_York`.
- `docker compose ps openclaw-agent` shows `status=running` and `health=healthy` after `start_period` (30s) expires.
