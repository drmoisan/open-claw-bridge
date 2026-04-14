# bootstrap-install (Issue #26)

- Date captured: 2026-04-13
- Author: drmoisan
- Status: Promoted -> docs/features/active/bootstrap-install/ (Issue #26)
- Issue: #26
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/26
- Last Updated: 2026-04-13

## Problem / Why

The repository currently has two separate deployment surfaces:

- the existing Windows bridge install path for `OpenClaw.MailBridge` plus `OpenClaw.MailBridge.Client`
- the optional additive path for `OpenClaw.HostAdapter` on Windows plus `OpenClaw.Core` in Docker Desktop

Those paths are documented, but they are not orchestrated by one repo-supported bootstrap flow. An operator who wants the full local stack must manually combine MSIX or script-based bridge installation, HostAdapter token and config provisioning, HostAdapter startup registration, `.env` setup, Docker Compose startup, and post-install validation. That increases setup variance, complicates support, and leaves no canonical stack-level install or uninstall path.

## Proposed Behavior

Add a stack bootstrap feature that installs or verifies all current components without changing the architecture that already exists in `README.md`, `docs/mailbridge-runbook.md`, `scripts/install-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `docker-compose.yml`, and `.env.example`.

The feature should treat the deployment as three coordinated units:

- `OpenClaw.MailBridge` plus `OpenClaw.MailBridge.Client` on Windows
- `OpenClaw.HostAdapter` on Windows
- `OpenClaw.Core` in Docker Desktop

The bootstrapper should orchestrate those units rather than attempting to force all components into one MSIX package. The bridge MSIX remains scoped to the bridge host and client because Outlook COM requires the interactive user session and the current MSIX startup-task model is already aligned to that constraint. `OpenClaw.Core` remains a Docker deployment that talks to the Windows host through `host.docker.internal` and the existing bind-mounted token contract.

The completed feature should:

- install or validate the bridge using the existing supported bridge deployment path
- provision `C:\ProgramData\OpenClaw\HostAdapter\adapter.token` and `C:\ProgramData\OpenClaw\HostAdapter\appsettings.json`
- install or publish `OpenClaw.HostAdapter` and register it for automatic startup on Windows
- generate or update `.env` from `.env.example` with the correct HostAdapter token path and port settings
- start `openclaw-core` through the existing Docker Compose files
- include a documented step for manually adding any required creative assets that are intentionally curated by an operator rather than generated or provisioned by the bootstrapper
- run deterministic validation checks for bridge readiness, HostAdapter HTTP readiness, and Core health endpoints
- provide a matching stack-level uninstall path and operator documentation

## Acceptance Criteria (early draft)

- [ ] A canonical stack bootstrap entry point exists, such as `scripts/install-openclaw-stack.ps1`, and coordinates bridge, HostAdapter, and Core installation without replacing the existing bridge-only deployment paths.
- [ ] The stack bootstrap flow can use the existing bridge installation surface and verifies that `OpenClaw.MailBridge.Client.exe` is installed at the path required by the HostAdapter configuration.
- [ ] The feature provisions HostAdapter configuration under `C:\ProgramData\OpenClaw\HostAdapter\`, including a bearer token file and `appsettings.json`, using the existing `OpenClaw:HostAdapter` configuration contract.
- [ ] The feature adds a supported HostAdapter deployment path, including binary placement or publish output selection and automatic startup registration on Windows.
- [ ] The feature generates or updates `.env` from `.env.example` so `docker compose` can start `openclaw-core` with the correct `OpenClaw__HostAdapter__BaseUrl`, `HOSTADAPTER_TOKEN_FILE`, and `OPENCLAW_HTTP_PORT` values.
- [ ] The bootstrapper can start `openclaw-core` with the existing Compose files and detect whether Docker Desktop or Docker Compose prerequisites are missing before claiming success.
- [ ] The stack install documentation identifies any creative assets that must be added manually, states where they must be placed, and distinguishes those operator-supplied assets from files that the bootstrapper creates automatically.
- [ ] The bootstrapper runs deterministic post-install validation for the bridge client `status` command, `http://127.0.0.1:4319/v1/status`, and `OpenClaw.Core` health endpoints such as `/health/live`, `/health/ready`, and `/api/status`.
- [ ] A canonical stack uninstall or teardown entry point exists, such as `scripts/uninstall-openclaw-stack.ps1`, and documents which state is removed versus intentionally preserved.
- [ ] `README.md` and `docs/mailbridge-runbook.md` document the stack bootstrap path, its prerequisites, fallback behavior, and operational limitations.

## Constraints & Risks

- Do not widen the current MSIX feature scope by forcing `OpenClaw.HostAdapter` or `OpenClaw.Core` into the existing `OpenClaw.MailBridge` MSIX package. The bootstrapper should orchestrate multiple deployment units instead.
- `OpenClaw.MailBridge` must remain in the interactive user session because Outlook COM cannot run correctly in Session 0. This constraint still applies even if the stack installer adds more automation.
- `OpenClaw.Core` currently depends on Docker Desktop, `host.docker.internal`, a bind-mounted HostAdapter token file, and the existing Compose topology. Those dependencies are real prerequisites, not installer implementation details.
- HostAdapter auto-start design needs an explicit decision between service-style startup and task-style startup. That decision affects required privileges, recovery behavior, and operator expectations.
- A full stack install may require mixed privilege boundaries: user-level bridge launch behavior, machine-level `ProgramData` writes, Windows startup registration, and Docker access.
- If the final stack experience depends on logos, icons, splash images, sample media, or other creative assets that are not generated by the codebase, those assets should be treated as explicit manual inputs with documented source, placement, and ownership.
- End-to-end validation is materially harder than the bridge-only install path because it spans Windows process management, HTTP, Docker networking, and container health.

## Test Conditions to Consider

- [ ] Unit coverage for bootstrap helper functions such as prerequisite detection, path resolution, token generation or validation, HostAdapter config generation, `.env` generation, and startup-registration argument construction
- [ ] Integration scenarios for fresh-machine install, rerun or idempotent install, bridge-only prerequisite failure, missing Docker Desktop, missing token file, HostAdapter startup failure, successful Core readiness after bootstrap, and operator confirmation that required creative assets were added to the documented locations
- [ ] CLI examples for full stack install, full stack uninstall, targeted validation commands, recovery commands when only one layer of the stack is degraded, and the documented manual creative-asset handoff points

## Next Step

- [ ] Promote to a GitHub feature issue with the bootstrapper defined as an orchestration layer over the existing bridge, HostAdapter, and Docker Core architecture
- [ ] Create the active feature folder and derive `issue.md`, `user-story.md`, and `spec.md` with explicit decisions for HostAdapter startup registration, stack-level validation evidence, and uninstall behavior
