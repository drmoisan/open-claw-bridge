# P5-T1 — Rebuild openclaw-agent image (operator runbook + evidence template)

Timestamp: 2026-04-22T23-20
Command: `docker compose build openclaw-agent`

## Status

PENDING_MANUAL_VERIFY

## Operator Runbook

This step must be executed by the operator on the Docker Desktop workstation. It was not executed by the atomic-executor agent: rebuilding the agent image has operational side effects (cached layers, bandwidth, and a subsequent container swap) and the plan explicitly scopes P5-T3/P5-T4 (and by extension P5-T1/P5-T2) to operator participation.

### Steps

1. From the repository root, run:

   ```
   docker compose build openclaw-agent
   ```

2. Confirm the command exits with status 0 and the final line reports successful build of the `openclaw/agent:pre-mvp` image.

3. When complete, record the results in this artifact by replacing the `PENDING_MANUAL_VERIFY` section above with:

   ```
   EXIT_CODE: <0 or the actual exit code>
   Output Summary: <last 10 lines of build output, or full error output if non-zero>
   Completed: <ISO-8601 timestamp>
   ```

### Expected Outcome

`EXIT_CODE: 0` with the final output line containing `naming to docker.io/openclaw/agent:pre-mvp` or an equivalent confirmation of a successful image build.
