# Issue Update Mirror — Issue #144 (P2-T10)

- Timestamp: 2026-07-10T20-30
- PostedAs: unknown

## POSTING DEFERRED

This executor does not run `git commit` or post to GitHub; the orchestrator owns commit and any GitHub interaction. The exact text intended for the issue update is recorded below for the orchestrator/pr-author to post as a comment (or issue-body update) at merge time. If posted as `body`, mirror the same text into `issue.md` at that time.

## Intended Update Text

Fix for issue #144 (container-validation false negatives) is complete on the feature branch. Summary of changes:

- **Probe URL corrected (AC1/AC2):** the in-container HostAdapter probe in `OpenClawContainerValidation.psm1` now requests `http://host.docker.internal:4319/status` (stray `/v1` removed); the `HostAdapterInContainer` `ExpectedCondition` text was updated to reference `/status`.
- **Default `-EnvFilePath` retargeted (AC3):** `Invoke-OpenClawContainerPathValidation.ps1` now resolves its default env file to the version-neutral deployed operator env file (`%LOCALAPPDATA%\OpenClaw\operator-config\.env`) when present, falling back to `./.env` otherwise. Resolution is implemented as two pure, exported, unit-tested helpers (`Get-OpenClawOperatorEnvFilePath`, `Resolve-OpenClawDefaultEnvFilePath`).
- **Dashboard-auth documentation corrected (AC6):** `README.md` and `docs/mailbridge-runbook.md` no longer claim tokenless/auto dashboard access or a single-credential dashboard. They document the accurate upstream (OpenClaw 2026.6.11) procedure — the `http://127.0.0.1:18789/#token=<OPENCLAW_GATEWAY_TOKEN>` fragment URL or Control UI paste, the device re-pair reset (`openclaw devices clear` + clear browser site data + reopen the fragment URL), and the floating-`OPENCLAW_AGENT_IMAGE`-tag caveat.
- **Validation reports auth accurately (AC7):** the `AgentDashboard` probe text now states that a 200 on `/` confirms the Control UI is served but does not verify operator sign-in. A new `GatewayTokenInContainer` probe verifies `OPENCLAW_GATEWAY_TOKEN` is present and non-empty inside the running agent container via the docker seam, distinct from the `.env`-file presence check. No WebSocket/device-pairing handshake was added.
- **No regression (AC4/AC5):** full `tests/scripts` Pester suite passes (416 passed / 0 failed); PoshQC format + analyze clean; line coverage 92.41% and command/instruction coverage 91.73% on the two production files (no regression vs baseline); no change under `src/OpenClaw.HostAdapter/**`.

Follow-up (non-blocking): the `docs/mailbridge-runbook.md` `HostAdapterInContainer` expectation bullet still references `/v1/status` and should be updated to `/status` for consistency with the AC1 fix (outside the enumerated plan scope).

Post-merge integration retest: run `scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru` against a healthy install and confirm `OverallResult: Expected`, including the new `GatewayTokenInContainer` probe.
