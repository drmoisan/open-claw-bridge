# container-validation-stray-v1-and-env-target (Issue #144)

- Date captured: 2026-07-10
- Author: drmoisan
- Status: Promoted -> docs/features/active/container-validation-stray-v1-and-env-target/ (Issue #144)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #144
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/144
- Last Updated: 2026-07-11
- Work Mode: minor-audit

## Summary

`Invoke-OpenClawContainerPathValidation.ps1` reports two false-negative failures against a correctly installed, healthy stack: (1) the in-container HostAdapter probe requests `/v1/status`, but HostAdapter serves `/status` at root (the stray-`/v1` defect class fixed in issue #137, which did not cover this validation module), so the probe gets HTTP 404; (2) the gateway-token presence check defaults `-EnvFilePath` to the repo-root `./.env` template (empty token) instead of the deployed operator `.env` that actually carries `OPENCLAW_GATEWAY_TOKEN`.

## Environment

- OS/version: Windows 11 Pro 10.0.26200
- Python version: n/a (PowerShell 7 / Docker Desktop)
- Command/flags used: `pwsh -NoProfile -File scripts\Invoke-OpenClawContainerPathValidation.ps1 -PassThru`
- Data source or fixture: live installed stack (openclaw-core + openclaw-agent running healthy)

## Steps to Reproduce

1. Complete a successful scripted-bundle install (containers running and healthy).
2. Run `scripts\Invoke-OpenClawContainerPathValidation.ps1 -PassThru` from the repo root with default parameters.
3. Observe `OverallResult: Unexpected` with `HostAdapterInContainer` and `GatewayTokenPresence` failing.

## Expected Behavior

Against a healthy install, the in-container HostAdapter probe returns HTTP 200 (HostAdapter root route `/status`), and the gateway-token check reads the deployed operator `.env` where the token is provisioned, so `OverallResult` is `Expected`.

## Actual Behavior

- `HostAdapterInContainer`: `Unexpected: in-container HostAdapter returned HTTP 404.` The probe curls `http://host.docker.internal:4319/v1/status`.
- `GatewayTokenPresence`: `Unexpected: OPENCLAW_GATEWAY_TOKEN is present but empty in './.env'.` — the repo-root template, not the deployed operator `.env`.

All other probes (Docker engine, container exists/running/healthy for both services, core `/health/live`, `/health/ready`, `/api/status`, agent `/`, `/readyz`) are `Expected`.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet: see Actual Behavior.

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

The install is functional, but the verification tool reports a false failure on every correct install, undermining trust in the verification step.

## Suspected Cause / Notes

- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:282` hardcodes `.../4319/v1/status` in the probe shell command; line 303's `ExpectedCondition` text repeats `/v1/status`.
- `scripts/Invoke-OpenClawContainerPathValidation.ps1:33` defaults `-EnvFilePath` to `./.env` (repo-root template). This file also feeds `OPENCLAW_HTTP_PORT`/CoreBaseUrl resolution.

## Proposed Fix / Validation Ideas

- Strip `/v1` from the in-container probe URL (`/v1/status` -> `/status`) at `OpenClawContainerValidation.psm1:282` and update the `ExpectedCondition` text at line 303.
- Retarget the default `-EnvFilePath` in `Invoke-OpenClawContainerPathValidation.ps1` to the deployed operator `.env` (version-neutral `%LOCALAPPDATA%\OpenClaw\operator-config\.env`), with a documented fallback to `./.env` when the operator file is absent.
- [x] Unit coverage areas: Pester for the probe URL (asserts no `/v1`) and the default env-path resolution.
- [x] Integration scenario to retest: run the validation against a healthy install and confirm `OverallResult: Expected`.
- [ ] Manual verification notes.

## Acceptance Criteria

- [x] AC1 — The in-container HostAdapter probe in `OpenClawContainerValidation.psm1` requests the root `/status` path (`http://host.docker.internal:4319/status`) with no `/v1` segment. Verified by a Pester test asserting the probe shell command contains `/status` and does not contain `/v1`.
- [x] AC2 — The `HostAdapterInContainer` result's `ExpectedCondition` text references `/status` (no `/v1`). Verified by a Pester assertion on the emitted result.
- [x] AC3 — `Invoke-OpenClawContainerPathValidation.ps1` resolves its default `-EnvFilePath` to the version-neutral deployed operator env file (`%LOCALAPPDATA%\OpenClaw\operator-config\.env`) when that file exists, and falls back to `./.env` when it does not. The resolution is a testable pure helper (no ambient side effects) verified by Pester for both the present and absent cases.
- [x] AC4 — No regression: the full `tests/scripts` Pester suite passes and PoshQC format + analyze report clean on all changed PowerShell files in a single pass.
- [x] AC5 — No HostAdapter routing change: no production code under `src/OpenClaw.HostAdapter/**` is modified; the fix is confined to the validation tooling and documentation.
- [x] AC6 — Dashboard-access documentation is corrected. `README.md` (the dashboard section around line 706) and `docs/mailbridge-runbook.md` (Dashboard access section) no longer claim the dashboard "reads the token without an operator paste step" or that the token "is the only credential the dashboard accepts." They document the accurate upstream (OpenClaw 2026.6.11) procedure: open `http://127.0.0.1:18789/#token=<OPENCLAW_GATEWAY_TOKEN>` (token in the URL fragment) or paste the token in Control UI settings; and the device re-pair reset for when the agent container is recreated (`openclaw devices clear` inside the agent container + clear browser site data for the origin + reopen the fragment URL). A note records that `OPENCLAW_AGENT_IMAGE` tracks a floating upstream tag, so the auth flow can change across upgrades.
- [x] AC7 — Dashboard validation reports auth accurately. Because the gateway exposes no auth-gated HTTP endpoint (verified: `/`, `/readyz`, `/healthz`, `/control`, `/gateway/status` return 200 without a token; `/api/*` 404) and operator auth is WebSocket + device-pairing, the `AgentDashboard` probe's `ExpectedCondition`/summary state that a 200 on `/` confirms the Control UI is served but does NOT verify operator authentication (which requires the `#token=` fragment). Additionally, a check verifies `OPENCLAW_GATEWAY_TOKEN` is present and non-empty inside the running agent container (via the docker seam), distinct from the `.env`-file presence check. Verified by Pester with the docker/HTTP seam mocked for present and absent cases. No fragile WebSocket/pairing handshake is added.

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch
