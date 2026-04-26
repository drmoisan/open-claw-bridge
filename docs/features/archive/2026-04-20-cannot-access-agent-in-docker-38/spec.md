# 2026-04-20-cannot-access-agent-in-docker (Spec)

- **Issue:** #38
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-20T09-21
- **Status:** Draft
- **Version:** 0.1

## Context
The OpenClaw agent is treated as an optional add-on across the runbook, compose stack, and diagnostic steps, yet it is the core end-user surface of the solution. When operators follow the current runbook, the Gateway Dashboard at `http://127.0.0.1:18789/` prompts for a shared-secret credential that no documented step produces, because the mandatory OpenClaw onboarding stage (which generates and persists the gateway token to `.env`) is skipped. Operators cannot reach the agent UI even when every container reports healthy. Port references in the runbook (`http://127.0.0.1:8181/`) also disagree with the actual published port (`18789`) in [docker-compose.yml:71](docker-compose.yml#L71), and the current verification procedure is a loose collection of one-off `curl`/`docker compose exec` calls with no single pass/fail script.

Environment:
- OS/version: Windows 11 Pro 10.0.26200 (operator workstation)
- Python version: not applicable — stack is .NET 10 on Windows host plus Docker Desktop containers
- Command/flags used:
  - `docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up -d openclaw-agent`
  - Browser navigation to `http://127.0.0.1:18789/`
  - Per-runbook verification: `curl.exe http://127.0.0.1:8181/` (from [docs/mailbridge-runbook.md:498](docs/mailbridge-runbook.md#L498))
- Data source or fixture: `OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest` (per [README.md:273](README.md#L273)); HostAdapter token at `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`; assistant workspace `./deploy/docker/openclaw-assistant/` with the unverified placeholder [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) whose `_placeholder` key explicitly warns that values are unverified.

Impact / Severity:
- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Rationale: the agent is the primary end-user surface. An operator who follows the published runbook end-to-end cannot authenticate to the dashboard and therefore cannot use the solution at all.


## Repro & Evidence
Steps to Reproduce:
1. Follow the runbook section "Optional OpenClaw Assistant Service" in [docs/mailbridge-runbook.md:467-513](docs/mailbridge-runbook.md#L467-L513). Set `OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest` in `.env`.
2. Run `docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up -d openclaw-agent`.
3. Wait for `docker ps` to report `openclaw-agent` as healthy.
4. Open `http://127.0.0.1:18789/` in a browser (the actual published port from [docker-compose.yml:71](docker-compose.yml#L71), not the `8181` the runbook quotes at [docs/mailbridge-runbook.md:498](docs/mailbridge-runbook.md#L498)).
5. Observe the Gateway Dashboard credential prompt (see screenshot below).
6. Attempt to authenticate: no shared-secret or password has been generated or written to `.env` by any runbook step, so there is no credential to paste.

Expected:
- Operators who complete the documented install path reach a usable agent UI without an undocumented authentication wall.
- The runbook presents the agent as an integral component of the solution, not an optional overlay, because no supported workflow exists without it.
- A single scripted diagnostic returns a pass/fail result covering container health, HostAdapter reachability from inside the container, gateway readiness (`/readyz`), and the dashboard authentication state.
- All runbook port references match the published ports in [docker-compose.yml](docker-compose.yml) (`18789` for the agent; `8080` for `openclaw-core`; `4319` for the HostAdapter).
- Documented environment configuration matches the official OpenClaw onboarding flow described at [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker): the gateway token used by `gateway.auth.mode=token` in [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) is produced by a one-time onboarding step and written to `.env`, and the operator knows exactly where to find and paste that token.

Actual:
- The runbook section is titled "Optional OpenClaw Assistant Service" ([docs/mailbridge-runbook.md:467](docs/mailbridge-runbook.md#L467)) and its text says the service "sits beside `openclaw-core` as a separate consumer" of the HostAdapter. There is no supported end-user workflow without the agent, so the "optional" framing is incorrect.
- The runbook verification step uses `http://127.0.0.1:8181/` ([docs/mailbridge-runbook.md:498](docs/mailbridge-runbook.md#L498)), but the compose file publishes `127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}:18789` ([docker-compose.yml:71](docker-compose.yml#L71)). The v1 spec used `:8181`; the v2 spec and shipped compose file use `:18789`. The runbook still references the obsolete `8181` value.
- Diagnostics are split across several ad-hoc commands in [docs/mailbridge-runbook.md:494-506](docs/mailbridge-runbook.md#L494-L506) (one `curl` on the host, one `docker compose exec` inside the container, no aggregated result). There is no single `verify-openclaw-agent.ps1` or equivalent that returns an overall pass/fail.
- The Gateway Dashboard at `http://127.0.0.1:18789/` prompts for a shared secret. The current deployment ships [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) with `gateway.auth.mode: "token"` but does not perform the onboarding step that the official OpenClaw Docker install documents. Per [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker), setup is expected to "prompt for provider API keys" and "generate a gateway token and write it to `.env`" before the container starts; the repository's install path starts the container directly from `OPENCLAW_AGENT_IMAGE` and never runs `openclaw onboard`, so no gateway token exists for the operator to paste into Settings.

Logs / Screenshots:
- [x] Attached minimal logs or screenshot
- Snippet:

```text
Browser: http://127.0.0.1:18789/
Dashboard prompt: "Enter shared secret"  (no credential has been issued to the operator)

Docker status (representative):
  openclaw-agent   healthy
  openclaw-core    healthy

docker compose exec openclaw-agent sh -c 'curl -H "Authorization: Bearer $(cat /run/openclaw/hostadapter.token)" http://host.docker.internal:4319/v1/status'
  -> HTTP 200 (HostAdapter reachable; bridge is not the problem)

curl.exe http://127.0.0.1:18789/readyz
  -> HTTP 200 (gateway process is ready; only the dashboard auth path is blocked)
```

- Screenshot referenced in the original request: Gateway Dashboard credential prompt with no documented source for the expected shared secret.


## Scope & Non-Goals

- In scope:
  1. Reframe `openclaw-agent` as an integral service across `README.md`, `docs/mailbridge-runbook.md`, `docs/architecture-diagrams.md`, and `AGENTS.md`.
  2. Add an upstream-conformant onboarding script (`scripts/Invoke-OpenClawAgentOnboarding.ps1`) that runs the OpenClaw gateway onboarding subcommand once, captures the generated `OPENCLAW_GATEWAY_TOKEN`, and writes it to the repository-root `.env`.
  3. Extend `scripts/Invoke-OpenClawContainerPathValidation.ps1` into a single pass/fail diagnostic covering container health, gateway `/readyz`, in-container HostAdapter reachability, token presence in `.env`, and a dashboard-auth probe.
  4. Align every port reference in documentation and validation defaults with the compose stack (`18789` for the agent, `8080` for `openclaw-core`, `4319` for the HostAdapter).
  5. Correct the already-staged work called out by the research artifact: the `gateway.auth.mode` vs runbook-prose contradiction in `openclaw.json`, the hard-coded `OPENCLAW_GATEWAY_TOKEN=openclaw-dev-token` default in `docker-compose.yml`, the `CoreBaseUrl = http://127.0.0.1:8081` default in the validation script, and the unconditional seed-file `cp` behavior in `openclaw-agent-entrypoint.sh`.
- Out of scope / non-goals:
  - Write-plane operations (send, reply, accept, decline) on the agent dashboard.
  - Switching the MailBridge safe-mode behavior to enhanced mode.
  - Windows bridge, MailBridge, Client, or HostAdapter C# source changes.
  - Password-auth mode for the gateway — token-auth is the upstream-sanctioned path and the only mode this bug addresses.
  - Multi-user, remote, or non-loopback deployments.
  - Non-Anthropic provider onboarding paths beyond what the upstream `onboard` subcommand exposes.
- Explicitly excluded systems, integrations, or datasets:
  - `OpenClaw.MailBridge`, `OpenClaw.Client`, `OpenClaw.HostAdapter` C# source.
  - MSIX package and `scripts/Invoke-Publish.ps1`.
  - Scheduled-task installer (`scripts/Install-OpenClawScheduledTasks.ps1`).
  - Historical feature folders under `docs/features/active/2026-04-16-openclaw-agent-docker-30/v1/` and `v2/`; stale `8181` references there are archival and not in scope for this bug.

## Root Cause Analysis
Likely contributing factors, for the implementer to confirm:

1. **Deployment skipped the OpenClaw onboarding step.** The repository's install path pulls `ghcr.io/openclaw/openclaw:latest` and starts it directly via `docker compose up -d openclaw-agent`, but the official Docker install at [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker) specifies a first-run `setup.sh` / `openclaw onboard` step that generates the gateway shared secret and persists it into `.env`. Without that step, the container starts with `gateway.auth.mode=token` but the operator has no token to present.
2. **Agent was modelled as optional from the start.** [docs/features/potential/2026-04-16-openclaw-agent-docker.md](docs/features/potential/2026-04-16-openclaw-agent-docker.md) and the v1/v2 specs under [docs/features/active/2026-04-16-openclaw-agent-docker-30/](docs/features/active/2026-04-16-openclaw-agent-docker-30/) describe the agent as an additive service. The delivered runbook inherits that framing even though operator-facing reality is the opposite.
3. **Port drift between v1 and v2 of the feature.** The v1 spec used port `8181`; v2 and the shipped compose file moved to `18789`. The runbook verification block still references `8181`.
4. **`openclaw.json` is explicitly a placeholder.** Line 2 of [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) warns that all values must be verified against `docs.openclaw.ai/gateway/configuration-reference` before production use. The gateway-auth stanza was never reconciled with the real onboarding behavior.

Files to inspect:

- [docs/mailbridge-runbook.md](docs/mailbridge-runbook.md) — sections "Install Path C" and "Optional OpenClaw Assistant Service"
- [docker-compose.yml](docker-compose.yml) and [docker-compose.dev.yml](docker-compose.dev.yml) — `openclaw-agent` service definition and published ports
- [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) — gateway auth configuration
- [README.md:267-310](README.md#L267-L310) — assistant image guidance
- [scripts/](scripts/) — missing `verify-openclaw-agent.ps1` and missing `onboard-openclaw-agent.ps1`
- [.env.example](.env.example) — missing `OPENCLAW_GATEWAY_TOKEN` (or equivalent) and any onboarding-derived keys


## Proposed Fix

### Design summary (what changes where):

The fix delivers four work streams plus corrections to the already-staged work. A new PowerShell wrapper `scripts/Invoke-OpenClawAgentOnboarding.ps1` runs the upstream OpenClaw onboarding sequence (`docker compose run --rm --no-deps --entrypoint node openclaw-gateway dist/index.js onboard --mode local --no-install-daemon`, per the upstream install doc — binary path `dist/index.js` pending upstream verification against the actual `${OPENCLAW_AGENT_IMAGE}` layer contents; see research artifact §3.1) and persists the generated `OPENCLAW_GATEWAY_TOKEN` to the repository-root `.env`. `deploy/docker/openclaw-assistant/openclaw.json` is reconciled so `gateway.auth.mode` matches the runbook prose, with the token referenced from the `.env`-sourced environment variable. `docker-compose.yml` drops the hard-coded `openclaw-dev-token` default so a misconfigured `.env` fails loudly. `scripts/Invoke-OpenClawContainerPathValidation.ps1` gains `/readyz`, in-container HostAdapter, token-presence, and dashboard-auth probes, and its `CoreBaseUrl` default is corrected from `8081` to `8080`. `deploy/docker/openclaw-agent-entrypoint.sh` becomes idempotent so onboarding state written to `/workspace` is not overwritten on every container start. Documentation (`README.md`, `docs/mailbridge-runbook.md`, `docs/architecture-diagrams.md`, `AGENTS.md`) reframes the agent as required and uses `${OPENCLAW_AGENT_PORT:-18789}` consistently.

### Boundaries and invariants to preserve:

- Non-root container user for `openclaw-agent`.
- Loopback-only port publishing (`127.0.0.1:...` for every published port).
- Read-only root filesystem for `openclaw-agent` (tmpfs for `/.openclaw`).
- Read-only bind mount of the HostAdapter bearer token into the container.
- Existing `openclaw-core` service behavior, health checks, and ports.
- Existing HostAdapter and MailBridge runtime behavior on the Windows host.
- Safe-mode read-only behavior of the MailBridge; no write-plane changes.

### Dependencies or blocked work:

- Upstream OpenClaw gateway image stability at `${OPENCLAW_AGENT_IMAGE}` (default `ghcr.io/openclaw/openclaw:latest`). Specifically the onboarding entrypoint (`node dist/index.js onboard ...`) and the CMD binary path. If upstream renames `dist/index.js`, onboarding breaks — pending upstream verification per research §3.1.
- Docker Desktop with working `host.docker.internal` networking on the operator workstation. Headless Docker Engine and remote Docker hosts are not supported targets for this fix.
- Continued reachability of `docs.openclaw.ai/install/docker` for upstream command verification.

### Implementation strategy (what changes, not sequencing):

#### Files/modules to change:

The pre-existing staged work on branch `bug/cannot-access-agent-in-docker-38` will be stashed before Phase 0; every file below is re-delivered from a clean baseline, with the corrections noted.

Modified (staged work re-delivered with corrections):

- `README.md` — keep the "required service" reframing; ensure no "optional" language remains; replace any "no longer requires a gateway token" prose with the onboarding-based flow.
- `docs/mailbridge-runbook.md` — rename the "Optional OpenClaw Assistant Service" heading (line 475 in the pre-stash tree) to "OpenClaw Agent (Required)"; remove "optional" framing from the purpose block (lines 9-10, 20-21) and from "Install Path C is optional" (line 364); replace the ad-hoc `curl` diagnostic block (pre-stash lines 494-506) with a single invocation of the validation script; use `${OPENCLAW_AGENT_PORT:-18789}` consistently.
- `docs/architecture-diagrams.md` — keep the `18789` port; ensure topology prose describes the agent as required.
- `deploy/docker/openclaw-assistant/openclaw.json` — resolve the `auth.mode` contradiction: either keep `"mode": "token"` with an explicit `"token": "${OPENCLAW_GATEWAY_TOKEN}"` reference (preferred per upstream configuration reference), or align the mode value with onboarding output (pending upstream verification per research §2.2).
- `deploy/docker/openclaw-core.Dockerfile` — commentary-only changes as staged; no behavior change.
- `deploy/docker/openclaw-agent.Dockerfile` (new) — local wrapper image that builds from `${OPENCLAW_AGENT_IMAGE}`, bakes seed files, and sets the gateway `CMD`; entrypoint passthrough must allow `--entrypoint node` onboarding override to reach the underlying binary (pending upstream verification per research §2.1).
- `deploy/docker/openclaw-agent-entrypoint.sh` (new) — replace the unconditional `cp` of every seed file with a conditional copy (copy only when the target does not exist), so onboarding state written to `/workspace` survives container restarts.
- `docker-compose.yml` — keep the `build:` stanza, tmpfs uid/gid, and named volume; remove the hard-coded `OPENCLAW_GATEWAY_TOKEN:-openclaw-dev-token` default (line 75 in the pre-stash tree) so `.env` must supply the token.
- `docker-compose.dev.yml` — keep `extra_hosts: host.docker.internal:host-gateway` and the dev-port repoint as staged.
- `.env.example` — `OPENCLAW_GATEWAY_TOKEN=` (empty value, no default) with a comment pointing to the onboarding script; `OPENCLAW_AGENT_WORKSPACE` removed.
- `scripts/Invoke-OpenClawContainerPathValidation.ps1` (new in staged work, extended here) — correct `CoreBaseUrl` default to `http://127.0.0.1:8080`; add `/readyz`, in-container HostAdapter, `.env` token presence, and dashboard-auth probes.
- `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (new in staged work, extended here) — update hard-coded `8081` URIs to `8080`; add branch coverage for the new probes (see Test Strategy).

New files:

- `scripts/Invoke-OpenClawAgentOnboarding.ps1` — the onboarding wrapper.
- `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` — Pester coverage for the onboarding wrapper.
- `AGENTS.md` update — if the agent-service topology must be reflected, the correct path is via `/.github/instructions/*.instructions.md` plus `scripts/dev-tools/sync-agents-from-instructions.ps1` (pending owner decision per research §2.8).

#### Functions/classes/CLI commands impacted:

- `Invoke-OpenClawAgentOnboarding.ps1` (new) — advanced function with `CmdletBinding(SupportsShouldProcess)`. Parameters: `-AnthropicApiKey` (SecureString or plain; prompt if absent), `-EnvFilePath` (default `./.env`), `-ComposeFiles` (default project compose files), `-Force` (overwrite an existing token). Runs the upstream onboard subcommand via `docker compose run --rm --no-deps --entrypoint node openclaw-gateway dist/index.js onboard --mode local --no-install-daemon --non-interactive --auth-choice apiKey --anthropic-api-key <value> --secret-input-mode plaintext --gateway-port 18789 --gateway-bind loopback --no-install-daemon --skip-skills` (argument set pending upstream verification per research §3.1).
- `Invoke-OpenClawContainerPathValidation.ps1` (extended) — existing probes retained; new probe functions: `Invoke-OpenClawReadyzProbe`, `Invoke-OpenClawHostAdapterInContainerProbe`, `Test-OpenClawGatewayTokenPresence`, `Invoke-OpenClawDashboardAuthProbe`. `CoreBaseUrl` default corrected to `http://127.0.0.1:8080`. If additions push the file over the 500-line cap, extract shared helpers into a module under `scripts/powershell/modules/` and import it.
- `openclaw-agent-entrypoint.sh` — conditional copy logic for every seed file; preserve the `exec docker-entrypoint.sh "$@"` tail.

#### Data flow and validation changes:

1. Operator copies `.env.example` to `.env` (empty `OPENCLAW_GATEWAY_TOKEN`).
2. Operator runs `scripts/Invoke-OpenClawAgentOnboarding.ps1` with an Anthropic API key.
3. The onboarding wrapper executes the upstream `onboard` subcommand in a throwaway container; the upstream flow generates a `OPENCLAW_GATEWAY_TOKEN` and writes it to an `.env`. The wrapper captures that value and writes it to the repository-root `.env`.
4. Operator runs `docker compose up --build -d`.
5. `openclaw-agent` starts with `gateway.auth.token = ${OPENCLAW_GATEWAY_TOKEN}` from `.env`.
6. Operator runs `scripts/Invoke-OpenClawContainerPathValidation.ps1`; all five probes report `Expected`.
7. Operator opens `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/`; the dashboard accepts the stored token without a manual copy-paste.

#### Error handling and logging updates:

- Onboarding script fails fast with a distinct error category for: missing Docker Desktop, unreachable upstream image, upstream onboard command returning non-zero, malformed onboard output, and inability to write to `.env`.
- Validation script fails fast on any probe, reports `OverallResult = Unexpected`, and includes `SupportingDiagnostics` describing which probe failed.
- `docker-compose.yml` no longer provides a silent default for `OPENCLAW_GATEWAY_TOKEN`; an unset variable yields a clear compose-level error.
- Entrypoint script continues to `set -eu`; conditional copy must not swallow errors from failed `cp` on genuine seed-file misconfiguration.

#### Rollback/feature-flag considerations (if applicable):

No feature flag. Operator rollback is `git revert` of the merged PR plus `docker compose down -v` to drop the `openclaw_agent_workspace` named volume and any onboarding state.

### Technical specifications (interfaces/contracts):

#### Inputs/outputs and formats:

- `Invoke-OpenClawAgentOnboarding.ps1`:
  - Inputs: `-AnthropicApiKey [SecureString]` (prompt if absent), `-EnvFilePath [string]` default `./.env`, `-ComposeFiles [string[]]`, `-Force [switch]`.
  - Side effect: writes `OPENCLAW_GATEWAY_TOKEN=<generated-value>` to the target `.env` (never to `.env.example`).
  - Idempotent: if `OPENCLAW_GATEWAY_TOKEN` is already non-empty in `.env`, exits `0` with a `Write-Verbose` note unless `-Force` is supplied.
- `Invoke-OpenClawContainerPathValidation.ps1`:
  - Existing parameter signature preserved: `-CoreBaseUrl`, `-AgentBaseUrl`, `-CoreContainerName`, `-AgentContainerName`, `-DockerPath`, `-TimeoutSeconds`, `-PassThru`, `-AsJson`.
  - Output object retains `OverallResult`, per-probe `IsExpected`, and `SupportingDiagnostics`. New probe names: `AgentReadyz`, `HostAdapterInContainer`, `GatewayTokenPresence`, `DashboardAuth`.

#### Required configuration keys and defaults:

Post-change `.env.example` keys touched by this feature:

| Key | Default in `.env.example` | Source of truth | Notes |
|---|---|---|---|
| `OPENCLAW_AGENT_IMAGE` | `ghcr.io/openclaw/openclaw:latest` | operator `.env` | base image for the wrapper build |
| `OPENCLAW_AGENT_PORT` | `18789` | operator `.env` | published dashboard port |
| `OPENCLAW_HTTP_PORT` | `8080` | operator `.env` | published core port |
| `OPENCLAW_GATEWAY_TOKEN` | empty | **produced by onboarding; no default** | compose does not supply a fallback |
| `OPENCLAW_AGENT_WORKSPACE` | **removed** | n/a | replaced by named volume `openclaw_agent_workspace` |

#### Backward-compatibility expectations:

Operators upgrading from issue #30 must:
1. Remove any `OPENCLAW_AGENT_WORKSPACE=...` line from their local `.env` (silently ignored by compose if left, but documented for clarity).
2. Ensure `OPENCLAW_GATEWAY_TOKEN` is unset or empty in `.env`.
3. Run `scripts/Invoke-OpenClawAgentOnboarding.ps1` once to populate the token.
4. Resume with `docker compose up --build -d`.

Operators who currently rely on the hard-coded `openclaw-dev-token` placeholder will see compose fail until they onboard.

#### Performance constraints (latency/throughput/memory):

None beyond the existing container startup budget. Onboarding runs once; validation probes complete within `-TimeoutSeconds` (default `10`).

## Assumptions, Constraints, Dependencies

- Assumptions (environment, data, access):
  - The pre-existing staged work on branch `bug/cannot-access-agent-in-docker-38` will be stashed before Phase 0 begins; the stash serves as reference material, not a merge source. Phase 0 starts from a clean tree.
  - Docker Desktop with working `host.docker.internal` networking is available on the operator workstation. Headless Docker Engine and remote Docker hosts are unsupported for this bug.
  - Upstream documentation at `docs.openclaw.ai/install/docker` and `docs.openclaw.ai/start/wizard-cli-automation` remains reachable for upstream-command verification during implementation.
  - An Anthropic API key is available for the onboarding integration test; no other provider key is required.
  - Operator has `scripts/` available on `PATH` or runs scripts from the repository root.
- Constraints (budget, performance, compatibility):
  - PowerShell 7+ per `.claude/rules/powershell.md`.
  - 500-line file cap per `.claude/rules/general-code-change.md`. The existing validation script sits at 497 lines, so adding probes may require extracting helpers into a module.
  - Unit tests must not create temporary files per `.claude/rules/general-unit-test.md`; Pester mocks must stub `docker` CLI and `Invoke-WebRequest` calls.
- External dependencies (services, libraries, releases):
  - Upstream OpenClaw image stability at `${OPENCLAW_AGENT_IMAGE}`, specifically the `onboard` entrypoint path and the gateway binary location (pending upstream verification per research §3.1).
  - Pester v5.x and PSScriptAnalyzer via the repo's MCP toolchain (`mcp__drmCopilotExtension__run_poshqc_format`, `_analyze`, `_test`).

## Data / API / Config Impact

- User-facing or API changes:
  - New operator-facing script: `scripts/Invoke-OpenClawAgentOnboarding.ps1`.
  - Extended operator-facing script: `scripts/Invoke-OpenClawContainerPathValidation.ps1`.
  - New `.env` key: `OPENCLAW_GATEWAY_TOKEN` with no default.
  - Removed `.env` key: `OPENCLAW_AGENT_WORKSPACE`.
  - Revised runbook sections: "Optional OpenClaw Assistant Service" renamed to "OpenClaw Agent (Required)"; Install Path C no longer labelled optional.
- Data or migration considerations:
  - Operators must remove any `OPENCLAW_AGENT_WORKSPACE=...` line from their local `.env`.
  - Operators must run `scripts/Invoke-OpenClawAgentOnboarding.ps1` once on upgrade to populate `OPENCLAW_GATEWAY_TOKEN`.
  - Reset command for a clean onboarding: `docker compose down -v` drops the `openclaw_agent_workspace` named volume.
- Logging/telemetry updates (if any):
  - None beyond script `Write-Verbose` output and the structured `PSCustomObject` returned by the validation script.
- Compatibility notes (CLI flags, config schemas, versioning):
  - Validation script adds new probes but preserves its existing parameter contract.
  - Onboarding script is new with a documented parameter contract; no breaking changes to existing scripts.
  - `openclaw.json` schema changes are additive (adds `gateway.auth.token` reference); no existing key is renamed.

## Test Strategy
Seeded from issue:

The fix has four work streams. Each should be validated against the upstream install doc at [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker) before landing.

1. **Reframe the agent as integral, not optional.**
   - Rename the runbook section "Optional OpenClaw Assistant Service" to "OpenClaw Agent (Required)" and fold it into the primary install path rather than an appendix.
   - Update [README.md](README.md), [docs/architecture-diagrams.md](docs/architecture-diagrams.md), and [AGENTS.md](AGENTS.md) to describe `openclaw-agent` as a required service in every deployment.
   - Remove the "Install Path C is optional" framing in [docs/mailbridge-runbook.md:361-465](docs/mailbridge-runbook.md#L361-L465); keep the Windows bridge + HostAdapter + agent as a single install path.

2. **Add a scripted agent onboarding step that matches the upstream setup flow.**
   - Add `scripts/onboard-openclaw-agent.ps1` that reproduces what `setup.sh` does upstream: run the agent image's `onboard` entrypoint once, capture the generated gateway token, and write it to `.env` under the upstream-sanctioned variable name (likely `OPENCLAW_GATEWAY_TOKEN` — confirm from upstream before committing).
   - Add `OPENCLAW_GATEWAY_TOKEN=` (empty placeholder) to `.env.example` with a comment pointing operators at the onboarding script.
   - Document how to rotate the token and how to choose password-auth instead of token-auth if the operator prefers.
   - Update [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) so `gateway.auth` is sourced consistently with whatever the onboarding step produces; remove the `_placeholder` warning once values are verified.

3. **Replace the clunky per-service diagnostic with a single verification script.**
   - Add `scripts/verify-openclaw-agent.ps1` that performs, in order, and returns a single pass/fail:
     1. Confirm `docker compose ps openclaw-agent` reports `healthy`.
     2. Confirm `GET http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/readyz` returns `200`.
     3. From inside the container, confirm `GET http://host.docker.internal:4319/v1/status` with the bind-mounted bearer token returns `200`.
     4. Confirm the dashboard shared-secret variable (`OPENCLAW_GATEWAY_TOKEN` or equivalent) is present in `.env` and non-empty.
     5. Confirm the dashboard accepts that shared secret programmatically (e.g., a POST against the auth endpoint), so the operator is not left to discover auth failure interactively.
   - Replace the ad-hoc `curl`/`docker compose exec` block in [docs/mailbridge-runbook.md:494-506](docs/mailbridge-runbook.md#L494-L506) with a single invocation of the verification script.

4. **Correct the port references and align documentation with the compose stack.**
   - Update every runbook/README reference that still says `8181` to `${OPENCLAW_AGENT_PORT:-18789}`.
   - Audit [docs/mailbridge-runbook.md:413-456](docs/mailbridge-runbook.md#L413-L456) for any other stale port values (HostAdapter `4319`, core `8080` look correct but should be re-verified against [docker-compose.yml](docker-compose.yml)).

Validation ideas:

- [ ] Unit coverage areas: helper functions added to `scripts/` should be covered by Pester tests under `tests/` that stub Docker CLI calls and validate pass/fail branching.
- [ ] Integration scenario to retest: clean workstation, fresh clone, follow runbook end-to-end, reach a working agent dashboard without manual edits to `.env` or `openclaw.json`.
- [ ] Manual verification notes: on a machine with no prior OpenClaw state, confirm that (a) `onboard-openclaw-agent.ps1` generates a token and writes it to `.env`, (b) `verify-openclaw-agent.ps1` returns pass, and (c) pasting the recorded shared secret into the dashboard Settings grants access. Record evidence under `artifacts/` per the repository's evidence-and-timestamp conventions.

- Regression tests to add or update:
  - Expect-fail Pester test (Phase 2 of the plan): invoke the extended validation script against a mocked stack that has **not** been onboarded (empty `OPENCLAW_GATEWAY_TOKEN` in the simulated `.env`); the test must assert `OverallResult = Unexpected` on the `DashboardAuth` probe. This demonstrates that prior to the fix, opening the dashboard with no token produces the credential prompt.
  - Update the five existing tests in `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` so hard-coded `8081` URIs become `8080`.
- Unit tests (Pester v5.x) for the fixed behavior and boundaries:
  - `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`:
    - Branch coverage matrix: missing container / container not running / unhealthy / `docker inspect` returns malformed JSON / URL unreachable / `/readyz` non-200 / in-container HostAdapter exec failure / dashboard-auth probe rejected (401) / token absent from `.env` / token present but empty / all probes pass.
    - Mock `docker` CLI via the existing `Invoke-FakeDocker` injection pattern and `Invoke-WebRequest` via `Mock`.
  - `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1`:
    - Missing Docker Desktop (docker CLI throws) → script fails fast with a distinct error category.
    - Upstream onboard command exits non-zero → script propagates the error.
    - Onboard output is malformed (no `OPENCLAW_GATEWAY_TOKEN=...` line) → script fails fast.
    - Token already present in `.env` and `-Force` not supplied → idempotent no-op.
    - Token already present and `-Force` supplied → token overwritten.
    - Anthropic API key supplied via `-AnthropicApiKey` parameter → no prompt.
    - Anthropic API key absent → script prompts via `Read-Host -AsSecureString` (verify via mock).
- Edge cases and negative scenarios (invalid inputs, missing data, boundary values):
  - Empty `.env`, `.env` with only comments, `.env` with `OPENCLAW_GATEWAY_TOKEN=` set to an empty value, `.env` with the legacy `OPENCLAW_AGENT_WORKSPACE=...` line present.
  - Validation script called with an explicit non-default `-CoreBaseUrl` to confirm the parameter still overrides the corrected default.
  - Onboarding script invoked twice in succession; second invocation must not corrupt the first token.
- Error handling and logging verification:
  - `Write-Error`/`throw` paths covered by explicit `Should -Throw` assertions.
  - `Write-Verbose` output observable under `-Verbose` in tests where idempotent behavior is asserted.
- Coverage impact and targets for changed lines/modules:
  - New and extended scripts must reach `>= 90 %` line coverage per `.claude/rules/general-unit-test.md`.
  - Repository-wide coverage must remain `>= 80 %`.
- Toolchain commands to run (format → lint → type-check → test):
  - `mcp__drmCopilotExtension__run_poshqc_format`
  - `mcp__drmCopilotExtension__run_poshqc_analyze`
  - `mcp__drmCopilotExtension__run_poshqc_test`
  - No type-check step for PowerShell.
  - Restart the loop if any step fails or auto-fixes files.
- Manual validation steps (if required):
  - On a clean workstation: `docker compose down -v`; remove the wrapper image; copy `.env.example` to `.env`; run `scripts/Invoke-OpenClawAgentOnboarding.ps1` with a real Anthropic API key; run `docker compose up --build -d`; run `scripts/Invoke-OpenClawContainerPathValidation.ps1` and confirm `OverallResult: Expected`; open `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` and confirm the dashboard accepts the stored token without operator copy-paste.
  - Record baseline, post-change, and comparison artifacts under `artifacts/evidence/` using ISO-8601 timestamps per the repository's evidence-and-timestamp conventions.


## Acceptance Criteria

- [x] `scripts/Invoke-OpenClawAgentOnboarding.ps1` exists, executes the upstream onboarding command verified at `docs.openclaw.ai/install/docker`, writes `OPENCLAW_GATEWAY_TOKEN` to the repository-root `.env`, and is idempotent when the token is already present unless `-Force` is supplied. Evidence: `scripts/Invoke-OpenClawAgentOnboarding.ps1`; `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1`; `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-onboarding-pass.md`.
- [x] `deploy/docker/openclaw-assistant/openclaw.json` `gateway.auth.mode` is consistent with the runbook prose; the config and documentation do not contradict each other (pending upstream verification of the exact mode value required by the onboarded token flow). Evidence: `deploy/docker/openclaw-assistant/openclaw.json` (mode=token, token=${OPENCLAW_GATEWAY_TOKEN}); `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-openclaw-json-parse.md`.
- [x] `docker-compose.yml` does not provide a hard-coded `OPENCLAW_GATEWAY_TOKEN` default; the token is supplied entirely from `.env`. Evidence: `docker-compose.yml` line `OPENCLAW_GATEWAY_TOKEN: ${OPENCLAW_GATEWAY_TOKEN}`.
- [x] `.env.example` lists `OPENCLAW_GATEWAY_TOKEN=` with no default value and a comment pointing operators at `scripts/Invoke-OpenClawAgentOnboarding.ps1`; `OPENCLAW_AGENT_WORKSPACE` is not present. Evidence: `.env.example`.
- [x] `scripts/Invoke-OpenClawContainerPathValidation.ps1` default `CoreBaseUrl` matches the production compose port (`http://127.0.0.1:8080`). Evidence: `scripts/Invoke-OpenClawContainerPathValidation.ps1` parameter default.
- [x] `scripts/Invoke-OpenClawContainerPathValidation.ps1` probes, in a single invocation: container health, agent `/readyz`, in-container HostAdapter reachability (`GET http://host.docker.internal:4319/v1/status` with the bind-mounted bearer token), presence and non-emptiness of `OPENCLAW_GATEWAY_TOKEN` in `.env`, and dashboard-auth acceptance of that token. Evidence: `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` (probe functions) + `scripts/Invoke-OpenClawContainerPathValidation.ps1` (orchestration); test coverage in `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`; `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-validation-probes-pass.md`.
- [x] `deploy/docker/openclaw-agent-entrypoint.sh` no longer unconditionally overwrites `/workspace` contents on every start; onboarding state written to `/workspace` survives restart. Evidence: `deploy/docker/openclaw-agent-entrypoint.sh` (conditional `if [ ! -e ]` guards around every workspace seed-file copy).
- [x] `docs/mailbridge-runbook.md` heading "Optional OpenClaw Assistant Service" is replaced with "OpenClaw Agent (Required)"; the purpose block (lines 9-10, 20-21) and "Install Path C is optional" (line 364) are reframed as required; every verification step references `${OPENCLAW_AGENT_PORT:-18789}` and not `8181`. Evidence: `docs/mailbridge-runbook.md`.
- [x] `README.md`, `docs/architecture-diagrams.md`, and `AGENTS.md` describe `openclaw-agent` as a required service in every deployment (AGENTS.md propagation path per research §2.8 pending owner decision). Evidence: `README.md` §"Manage the OpenClaw Agent (Required)"; `docs/architecture-diagrams.md` prose update; `AGENTS.md` §"Repository Setup (High-Level)" bullet; `artifacts/evidence/2026-04-20T09-21-issue-38/regression-testing/2026-04-20T09-21-agents-edit.md`.
- [x] `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` updates all hard-coded `8081` URIs to `8080` and adds branch coverage for the four new probes. Evidence: `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (17 tests covering the four new probes).
- [x] `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` exists and covers missing Docker, upstream failure, malformed output, idempotency, and parameter-vs-prompt paths. Evidence: `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` (7 tests, all passing).
- [x] Pester suite passes with `>= 90 %` line coverage on `scripts/Invoke-OpenClawContainerPathValidation.ps1` and `scripts/Invoke-OpenClawAgentOnboarding.ps1`; repository-wide coverage stays `>= 80 %`. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-coverage-comparison.md` (repo=86.97%, validation=90.28%, onboarding=98.55%, module=94.63%).
- [x] Baseline, post-change, and comparison evidence artifacts are stored under `artifacts/evidence/` with ISO-8601 timestamps. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/{baseline,regression-testing,final}/`.
- [x] Full toolchain pass in order: `mcp__drmCopilotExtension__run_poshqc_format` → `mcp__drmCopilotExtension__run_poshqc_analyze` → `mcp__drmCopilotExtension__run_poshqc_test` completes in a single pass without auto-fix file changes. Evidence: `artifacts/evidence/2026-04-20T09-21-issue-38/final/2026-04-20T09-21-final-poshqc-format.md`, `2026-04-20T09-21-final-poshqc-analyze.md`, `2026-04-20T09-21-final-poshqc-test.md`. Tool binding note: MCP tools unavailable in the executor environment; substituted by direct import of the same PoshQC harness.
- [ ] Clean-machine integration: `docker compose down -v` → remove wrapper image → run onboarding script → `docker compose up --build -d` → validation script returns `OverallResult: Expected` → browser opens `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` and dashboard accepts the stored token without operator copy-paste. Note: manual verification gate; documented in `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/pr-notes.md`. Out of scope for automated executor verification.

## Risks & Mitigations

- Technical or operational risks:
  - Upstream OpenClaw changes the `onboard` subcommand path or renames `dist/index.js`. Likelihood: low-to-moderate (image is pinned to `:latest` by default). Impact: onboarding script breaks silently from the operator's perspective.
  - `docker-entrypoint.sh` in the upstream image does not pass through `--entrypoint node` arguments cleanly to the underlying binary, so the onboarding subcommand cannot run via the wrapper image (research §2.1).
  - Operator edits baked-in workspace files after onboarding; a restart overwrites their state because the entrypoint copy is not yet idempotent.
  - Extended validation script exceeds the 500-line cap (currently 497 lines).
  - Pester mocks for the `docker` CLI diverge from real CLI behavior and mask integration failures.
  - Removing the `openclaw-dev-token` default breaks existing operators who have not run the onboarding script.
- Mitigations and rollbacks:
  - Onboarding script fails fast with a specific error category when the upstream command returns non-zero or produces no token; runbook links operators to the upstream doc for the current command.
  - Verify `--entrypoint node` passthrough against the actual `${OPENCLAW_AGENT_IMAGE}` image during implementation; if passthrough is broken, expose a direct `node` entrypoint in the wrapper Dockerfile or run the onboarding against the upstream image directly before the wrapper build.
  - Entrypoint script converts unconditional `cp` to a conditional copy (copy only if the target does not exist) so onboarding state survives restarts.
  - If the validation script breaches the 500-line cap, extract shared helpers into a module under `scripts/powershell/modules/` and import it from the script.
  - Acceptance-criteria checklist includes an explicit clean-machine manual integration step so Pester mock drift is caught.
  - Migration note in the runbook and in the PR description explicitly instructs upgrading operators to run the onboarding script once; removal of the placeholder default is called out as a breaking change.
  - Rollback: `git revert` the PR plus `docker compose down -v` to drop any partial-state named volume.

## Rollout & Follow-up

- Release/rollout steps:
  1. Merge PR into `development`.
  2. Operators pull the latest branch and, if a `.env` file does not exist, copy `.env.example` to `.env`.
  3. Operators remove any `OPENCLAW_AGENT_WORKSPACE=...` line from `.env` and ensure `OPENCLAW_GATEWAY_TOKEN` is empty or absent.
  4. Operators run `scripts/Invoke-OpenClawAgentOnboarding.ps1` once, supplying an Anthropic API key.
  5. Operators run `docker compose up --build -d`.
  6. Operators run `scripts/Invoke-OpenClawContainerPathValidation.ps1` and confirm `OverallResult: Expected`.
  7. Operators open `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/`.
- Post-fix monitoring or clean-up tasks:
  - No automated telemetry added. The validation script is the manual health check operators run after any stack change. Audit of historical `8181` references in `docs/features/active/2026-04-16-openclaw-agent-docker-30/v1/` and `v2/` remains as a follow-up item (research §2.9) and is not in scope for this bug.
- Links:
  - Issue: https://github.com/drmoisan/open-claw-bridge/issues/38
  - Research artifact: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/2026-04-20T14-00-cannot-access-agent-in-docker-38-research.md` (mirrored at `artifacts/research/2026-04-20T14-00-cannot-access-agent-in-docker-38-research.md`)
  - Upstream onboarding doc: https://docs.openclaw.ai/install/docker#docker
  - Upstream wizard CLI automation doc: https://docs.openclaw.ai/start/wizard-cli-automation
