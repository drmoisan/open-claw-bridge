# admin-access-automation — Spec

- **Issue:** #148
- **Parent (optional):** openclaw-runtime-remediation (child C, wave 1)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-11T20-30
- **Status:** Draft
- **Version:** 0.2

## Overview

Provisioning gateway admin access after an OpenClaw install currently requires
undocumented manual steps. The 2026-07-10 runtime diagnosis found that an operator
must hand-deliver the gateway token to the Control UI, has no supported path to
rotate or reissue the HostAdapter device token, and must configure the `web_search`
provider by hand. These gaps break a clean, repeatable end-to-end admin-access flow
and leave human-held-secret steps uncaptured.

Token generation itself is already solved: `scripts/Invoke-OpenClawAgentOnboarding.ps1`
generates the gateway token (`OPENCLAW_GATEWAY_TOKEN`) into the target `.env`. This
feature provisions delivery, rotation, and provider setup only; it does not generate
tokens.

## Problem Statement

An operator completing an OpenClaw install today faces three unautomated,
undocumented tasks:

1. **Gateway-token delivery.** The gateway token exists in the project `.env` after
   onboarding, but the operator must manually construct the Control UI URL or paste
   the token into the Control UI settings to authenticate. There is no supported,
   repeatable delivery path.
2. **Device-token rotation.** The HostAdapter device (bearer) token is a host file
   bind-mounted read-only into the containers. There is no supported path to rotate
   or reissue it; the runbook documents only a manual overwrite-and-restart procedure.
3. **`web_search` provider provisioning.** The `web_search` tool is enabled at the
   `coding` tools profile level, but the underlying search-provider credential and
   provider entry are not configured in the baked agent configuration.

In addition, several steps depend on human-held secrets that cannot be generated or
supplied by automation. Today these are neither automated nor captured in a committed
runbook, so they act as silent manual blockers.

## Dependency on Child B (installer-image-version-alignment)

This feature depends on child B (`installer-image-version-alignment`, wave 0). Child
B aligns the installer's Control UI and gateway container image versions in
`Install.ps1` and the `OpenClawContainerValidation` module, keeping consistency with
the #142/#144 fixes.

Admin-access automation runs against B's version-aligned staging with **matched
Control UI and gateway container images**. This matters because:

- The `#token=` fragment behavior and the Control UI URL/port are upstream and
  version-dependent (`OPENCLAW_AGENT_IMAGE` otherwise defaults to a floating
  `:latest` tag). B's matched images make the URL shape and fragment behavior stable
  at execution time.
- The exact `web_search` provider config keys are upstream/version-dependent. They
  MUST be pinned against B's aligned image at plan/execution time, not assumed here.

Child B's spec/plan are prepared concurrently and fanned in before execution. This
worktree does NOT contain B's files. This spec treats B's matched-image guarantee as
an input assumption and does not read B's artifacts directly.

## Scope

### In Scope

- Gateway-token delivery to the Control UI via the `#token=` URL fragment (delivery
  only; the token already exists in `.env`).
- HostAdapter device-token rotation/reissue (overwrite host token file, then restart
  the consuming processes so both ends read the same value).
- `web_search` provider provisioning in the baked OpenClaw agent configuration
  (`deploy/docker/openclaw-assistant/openclaw.json`): add the provider entry and
  reference the provider API key via a SecretRef-style env interpolation.
- A committed runbook covering the human-held-secret and human-interaction steps that
  cannot be automated.

### Out of Scope

- **Token generation.** `scripts/Invoke-OpenClawAgentOnboarding.ps1` already generates
  `OPENCLAW_GATEWAY_TOKEN`. This feature does not generate any token and does not
  modify the onboarding script.
- Generating or issuing human-held secrets (search-provider API key, Anthropic API
  key, initial device-token secret). These are supplied by the operator.
- Automating the browser-side Control UI authentication and device-pairing handshake
  (upstream/interactive; not scriptable through repo seams).
- Changing branch protection, required checks, or child B's installer files.

## Gateway Token vs Device Token (explicit distinction)

The two tokens are distinct and this feature treats them separately. Capability 1
operates on the gateway token (delivery only); capability 2 operates on the device
token (rotation).

| Aspect | Gateway token | Device (HostAdapter) token |
|---|---|---|
| Identifier | `OPENCLAW_GATEWAY_TOKEN` (env value in project `.env`) | host file at `HOSTADAPTER_TOKEN_FILE` |
| Storage | value in project `.env` | file `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`, bind-mounted read-only to `/run/openclaw/hostadapter.token` |
| Authenticates | operator/browser -> Control UI / gateway | container agent + core -> HostAdapter |
| Endpoint / port | agent gateway, port 18789 | HostAdapter, port 4319 |
| Consumed via | `openclaw.json` `gateway.auth.token`; `#token=` URL fragment | `Authorization: Bearer <token>` header; `OpenClaw__HostAdapter__TokenFile` |
| Generated by | `Invoke-OpenClawAgentOnboarding.ps1` (RNG) — out of scope here | operator-supplied initial value; RNG on rotation |
| This feature | delivery only (capability 1) | rotation/reissue (capability 2) |

## Functional Requirements

### FR-1: Gateway-token delivery via `#token=` URL fragment

- FR-1.1 The system SHALL construct the Control UI authentication URL from the target
  `.env`, of the shape:
  `http://127.0.0.1:<OPENCLAW_AGENT_PORT>/#token=<OPENCLAW_GATEWAY_TOKEN>`, where
  `<OPENCLAW_AGENT_PORT>` resolves from `OPENCLAW_AGENT_PORT` (default `18789`) and
  `<OPENCLAW_GATEWAY_TOKEN>` is read from the same `.env`.
- FR-1.2 The base64url token requires no percent-encoding (no `+`, `/`, or `=`); it is
  fragment-safe. The system SHALL NOT re-encode or mutate the token value.
- FR-1.3 When `OPENCLAW_GATEWAY_TOKEN` is missing or empty, the system SHALL fail with
  a clear error that points the operator to `Invoke-OpenClawAgentOnboarding.ps1`,
  rather than emitting a malformed URL.
- FR-1.4 URL construction SHALL be a pure function over `.env` inputs. Reuse the
  existing `.env` parser seam (`Get-OpenClawEnvFileMap` in
  `OpenClawContainerValidation.psm1`) rather than re-implementing parsing.
- FR-1.5 The token value SHALL NOT be written to any log, verbose, or debug stream.
  The constructed URL is the delivery artifact and is handed off to the operator.
- FR-1.6 Opening the URL in a browser to complete Control UI authentication is
  human-interaction-required and is covered by the runbook (see Automation
  Feasibility).

### FR-2: Device-token (HostAdapter bearer token) rotation/reissue

- FR-2.1 The system SHALL rotate the device token by (1) writing a new non-empty
  random secret to the host token file (`HOSTADAPTER_TOKEN_FILE`, e.g.
  `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`), then (2) restarting the
  consuming processes so every end reads the same value.
- FR-2.2 The consuming container processes to restart SHALL be `openclaw-core` and
  `openclaw-agent`. The HostAdapter process on the Windows host SHALL also be
  restarted; where HostAdapter runs interactively (`dotnet run`), that restart is
  operator-driven and covered by the runbook (see Automation Feasibility).
- FR-2.3 Ordering: the system SHALL write the new secret first, then restart the
  consumers. The old token is invalidated implicitly by overwrite once every consumer
  has restarted; there is no separate revocation endpoint.
- FR-2.4 Rotation SHALL be state-changing and gated by `ShouldProcess`
  (`SupportsShouldProcess`) for both the file write and each restart action.
- FR-2.5 Rotation SHALL be idempotent: re-running without an explicit force flag SHALL
  NOT rotate an already-valid token, mirroring the `-Force` guard in the onboarding
  script.
- FR-2.6 The new secret SHALL be generated with a cryptographic RNG
  (`System.Security.Cryptography.RandomNumberGenerator`), mirroring the onboarding
  script's token generation. No token value SHALL be logged.
- FR-2.7 Container restarts SHALL go through the existing docker wrapper seam
  (`Invoke-OpenClawDockerCommand` in `OpenClawContainerValidation.psm1`), never a
  direct `docker` invocation.
- FR-2.8 Error paths SHALL fail explicitly: unreadable/unwritable token file, docker
  restart failure, and the missing-token-file case (see FR-2.9).
- FR-2.9 When the host token file is absent, provisioning the initial value is
  human-interaction-required and covered by the runbook; the system SHALL surface a
  clear error directing the operator to the runbook rather than silently creating a
  placeholder.

### FR-3: `web_search` provider provisioning

- FR-3.1 The system SHALL add or validate a `web_search` provider entry in the baked
  agent configuration seed `deploy/docker/openclaw-assistant/openclaw.json`. The
  `web_search` tool is already enabled by the `coding` tools profile; provisioning
  adds the underlying provider entry and credential reference.
- FR-3.2 The provider API key SHALL be referenced by a SecretRef-style env
  interpolation (e.g. `${WEB_SEARCH_API_KEY}`), mirroring how
  `gateway.auth.token` references `${OPENCLAW_GATEWAY_TOKEN}`. The key value SHALL NOT
  be hard-coded in the config.
- FR-3.3 The exact provider name and config key schema are upstream/version-dependent
  and SHALL be pinned against child B's aligned image at plan/execution time. This
  spec fixes the provisioning shape (provider entry + SecretRef + `.env`/secrets-backed
  key), not the exact upstream key names.
- FR-3.4 The configuration change SHALL be made in the baked seed file. The agent
  entrypoint re-seeds `/.openclaw/openclaw.json` from the baked seed on every start,
  so persistence requires editing the seed and rebuilding the image.
- FR-3.5 Provisioning SHALL be idempotent: re-running SHALL yield the same config with
  no duplicate provider entries.
- FR-3.6 Provisioning SHALL validate the resulting JSON and fail explicitly on invalid
  JSON or a missing referenced provider key env var.
- FR-3.7 Editing `openclaw.json` SHALL be gated by `ShouldProcess`.
- FR-3.8 Supplying the search-provider API key is human-interaction-required (external
  SaaS-issued, human-held) and covered by the runbook.

### FR-4: Committed runbook for human-held-secret steps

- FR-4.1 The system SHALL commit a runbook (extending `docs/mailbridge-runbook.md` or a
  dedicated admin-access runbook cross-linked from it) covering every
  human-interaction / human-held-secret step so none is left as a silent manual
  blocker.
- FR-4.2 The runbook SHALL enumerate, at minimum, the steps listed in the Automation
  Feasibility section below.
- FR-4.3 Each runbook step SHALL state what the operator must supply or do, and where
  automation hands off to the operator (and back, where applicable).

## Non-Functional Requirements

### Security (T1 token handling)

- NFR-S1 Token delivery (capability 1) and device-token rotation (capability 2) are
  T1-rigor, security-sensitive token-handling paths.
- NFR-S2 No plaintext secret SHALL be written to any output, verbose, debug, or log
  stream (gateway token, device token, provider API key, Anthropic key).
- NFR-S3 No token or secret SHALL be hard-coded in scripts or configuration; secrets
  are read from `.env`, secrets files, or generated at runtime and referenced by
  SecretRef.
- NFR-S4 Every state-changing action (token file write, container restart, config
  edit) SHALL use `ShouldProcess` / `SupportsShouldProcess`.

### Idempotency and Determinism

- NFR-I1 Delivery, rotation, and provisioning SHALL be safe to re-run. Rotation and
  provisioning SHALL be idempotent as specified in FR-2.5 and FR-3.5.
- NFR-D1 Tests SHALL be deterministic: no network, no temporary files, no sleeps, no
  wall-clock dependence. Use in-memory pseudo-files for `.env` and token-file inputs,
  mirroring the existing onboarding tests.
- NFR-D2 RNG-dependent output SHALL be tested for shape/charset, not exact value.

### PowerShell and Code Rules

- NFR-P1 PowerShell 7+; advanced functions with `CmdletBinding()`, named parameters,
  and validation attributes. Reference: `.claude/rules/powershell.md`.
- NFR-P2 External executables (docker, and gh/git if used) SHALL be invoked through
  wrapper-function seams, never mocked or called directly. Reuse
  `Invoke-OpenClawDockerCommand`. Reference: `.claude/rules/powershell.md`.
- NFR-P3 No production, test, or reusable script file SHALL exceed 500 lines. The three
  capabilities plus runbook SHALL be split across cohesive, single-purpose files.
  Reference: `.claude/rules/general-code-change.md`.
- NFR-P4 Fail fast and explicitly; no silent error handling or broad catch-all
  handlers without re-raise. Reference: `.claude/rules/general-code-change.md`.

### Testing and Coverage

- NFR-T1 Pester v5 tests SHALL mirror production structure under `tests/scripts/...`
  with `*.Tests.ps1` naming, one behavior per `It`, Arrange-Act-Assert. Reference:
  `.claude/rules/general-unit-test.md`.
- NFR-T2 Tests SHALL mock the wrapper seams (e.g. `Invoke-OpenClawDockerCommand`), not
  the executables; mock signatures SHALL match named parameters exactly.
- NFR-T3 Line coverage SHALL be >= 85% and branch coverage >= 75%, with no regression
  on changed lines. Reference: `.claude/rules/quality-tiers.md`,
  `.claude/rules/general-unit-test.md`.
- NFR-T4 Coverage SHALL include: URL construction with valid/missing/empty token and
  default vs explicit port; rotation happy path, unwritable file, docker restart
  failure, and idempotent no-op without force; provider provisioning with valid and
  invalid JSON and idempotent re-run; and secret-not-logged assertions verifying no
  token value appears in output/verbose streams.

## Automation Feasibility / Human-Interaction

Each capability step is classified as **fully automatable**, **partially
automatable**, or **human-interaction-required**. Human-interaction and human-held-
secret steps are covered by the committed runbook (FR-4) and are not left as silent
manual blockers.

### Capability 1 — gateway-token delivery

- Read token + port from `.env` and construct the `#token=` URL: **fully automatable**.
- Open the URL in a browser to complete Control UI authentication:
  **human-interaction-required** (runbook).
- Device re-pair after container recreation: `openclaw devices clear` via the docker
  exec seam is **fully automatable**; clearing browser site data and reopening the URL
  are **human-interaction-required** (runbook).

### Capability 2 — device-token rotation

- Generate a new secret and overwrite the host token file: **fully automatable**
  (RNG + file write with `ShouldProcess`).
- Restart `openclaw-core` and `openclaw-agent` via the docker seam: **fully
  automatable**.
- Restart the HostAdapter process on the host: **partially automatable** — automatable
  if HostAdapter runs as a managed service/task; **operator-driven** when launched
  interactively via `dotnet run` (runbook).
- Provision the initial host token file value when absent:
  **human-interaction-required** (runbook).

### Capability 3 — `web_search` provider provisioning

- Add/validate the provider entry and SecretRef in `openclaw.json`: **fully
  automatable**.
- Supply the search-provider API key (external SaaS-issued):
  **human-interaction-required** (runbook).

### Runbook-covered manual steps (enumerated)

1. Open the `#token=` URL in a browser to complete Control UI operator authentication.
2. After container recreation, clear browser site data for the dashboard origin and
   reopen the `#token=` URL to re-pair (paired with the automatable
   `openclaw devices clear`).
3. Supply the search-provider API key (Firecrawl or equivalent) into `.env`/secrets as
   a SecretRef for `web_search` provisioning.
4. Supply the Anthropic API key in `secrets/.env.anthropic`.
5. Provide/keep the initial HostAdapter device-token secret and, where HostAdapter is
   launched interactively, restart it during rotation.
6. Provision the initial HostAdapter token file value on the host
   (`C:\ProgramData\OpenClaw\HostAdapter\adapter.token`) if it does not already exist.

## Inputs / Outputs

- Inputs: target `.env` (`OPENCLAW_GATEWAY_TOKEN`, `OPENCLAW_AGENT_PORT` default
  `18789`, `HOSTADAPTER_TOKEN_FILE`); the host device-token file; the baked agent
  config seed `deploy/docker/openclaw-assistant/openclaw.json`; `.env`/secrets-backed
  provider and Anthropic keys (human-held).
- Outputs: the constructed `#token=` Control UI URL (capability 1); an updated host
  device-token file plus restarted consumers (capability 2); an updated agent config
  seed with a `web_search` provider entry (capability 3); a committed runbook
  (capability 4). No secret value is emitted to logs.
- Config keys and defaults: `OPENCLAW_AGENT_PORT` default `18789`; token generation
  reuses the onboarding script's RNG (base64url, byte length per onboarding default).
- Versioning constraint: provider config keys and the Control UI URL/port are pinned
  against child B's aligned image; do not assume floating `:latest` behavior.

## API / CLI Surface

The concrete function/parameter names are finalized at plan time. The expected shape:

- A pure URL-composition function (e.g. `Get-OpenClawControlUiTokenUrl`) reading the
  `.env` and returning the `#token=` URL; no `ShouldProcess` (read-only).
- A rotation function (e.g. `Invoke-OpenClawDeviceTokenRotation`) with
  `SupportsShouldProcess` and an idempotency/force guard, using the RNG and the docker
  seam.
- A provisioning/validation function editing/validating the `web_search` provider
  entry in the seed config with `SupportsShouldProcess` and idempotency.
- All functions are advanced functions with `CmdletBinding()`, validated named
  parameters, and explicit failure on invalid input.

## Data & State

- Rotation state model: `absent -> written -> propagated(restarted)`; the exit gate is
  all consumers restarted so every end reads the same token.
- Provisioning state model: `unset -> configured -> validated`.
- Persistence note: agent-config changes must be made in the baked seed because the
  entrypoint re-seeds `/.openclaw/openclaw.json` on every start; the image must be
  rebuilt for the change to persist.

## Constraints & Risks

- PowerShell 7+; repository PowerShell rules and quality-tier gates apply.
- Security-sensitive token paths (T1): no plaintext secret logging, no hard-coded
  tokens, `ShouldProcess` on all writes/restarts.
- Depends on installer-image-version-alignment (child B): admin-access automation runs
  against the version-aligned staging behavior B establishes, with matched Control UI
  and gateway container images. Where scope touches the installer staging path or
  container image versions (notably the `web_search` provider config keys and the
  Control UI port/URL), the plan pins those against B's aligned image; do not assume
  B's files are present in this worktree.
- Idempotency: delivery, rotation, and provisioning must be safe to re-run.
- The `#token=` fragment parsing and provider config schema are upstream and
  version-dependent; repo evidence fixes the URL shape and the provisioning shape, and
  version-dependent specifics are pinned against B's aligned image at plan time.
- Do not assume a tracked `.env` or `.env.example` in this worktree.

## Implementation Strategy

- Implementation scope: three focused advanced-function PowerShell scripts/modules
  (delivery, rotation, provisioning) plus a committed runbook; reuse existing seams.
- Reuse (no change expected): `OpenClawContainerValidation.psm1` docker seam and `.env`
  parser; `Invoke-OpenClawAgentOnboarding.ps1` (token generation unchanged).
- Files likely to change: new `scripts/` script(s) and mirrored `tests/scripts/`
  tests; `deploy/docker/openclaw-assistant/openclaw.json` (seed, requires image
  rebuild); `docs/mailbridge-runbook.md` (or a dedicated cross-linked runbook).
- Rollout: the three capabilities plus runbook likely exceed a single direct-mode
  change budget and should be planned as separate cohesive files.

## Acceptance Criteria

- [ ] AC-1 The system constructs the Control UI URL of the shape
  `http://127.0.0.1:<OPENCLAW_AGENT_PORT>/#token=<OPENCLAW_GATEWAY_TOKEN>` from the
  target `.env`, resolving `OPENCLAW_AGENT_PORT` (default `18789`) and reading
  `OPENCLAW_GATEWAY_TOKEN` from the same `.env`.
- [ ] AC-2 The base64url gateway token is used in the fragment without re-encoding or
  mutation.
- [ ] AC-3 When `OPENCLAW_GATEWAY_TOKEN` is missing or empty, URL construction fails
  with a clear error pointing to `Invoke-OpenClawAgentOnboarding.ps1` and emits no
  malformed URL.
- [ ] AC-4 The gateway token value never appears in any output, verbose, debug, or log
  stream during delivery.
- [ ] AC-5 Device-token rotation writes a new cryptographically-generated non-empty
  secret to the host token file (`HOSTADAPTER_TOKEN_FILE`) before restarting consumers.
- [ ] AC-6 Rotation restarts `openclaw-core` and `openclaw-agent` through the
  `Invoke-OpenClawDockerCommand` seam (never a direct `docker` call) so all consumers
  read the rotated value.
- [ ] AC-7 Rotation is gated by `ShouldProcess` for the file write and each restart,
  and is idempotent: re-running without an explicit force flag does not rotate an
  already-valid token.
- [ ] AC-8 Rotation fails explicitly on an unreadable/unwritable token file and on a
  docker restart failure, and directs the operator to the runbook when the host token
  file is absent (no silent placeholder creation).
- [ ] AC-9 `web_search` provider provisioning adds or validates a provider entry in
  `deploy/docker/openclaw-assistant/openclaw.json` referencing the provider API key via
  a SecretRef-style env interpolation, with no hard-coded key.
- [ ] AC-10 Provisioning is made in the baked seed file (persisted via image rebuild),
  is idempotent (no duplicate provider entries), validates the resulting JSON, and
  fails explicitly on invalid JSON or a missing referenced provider key env var.
- [ ] AC-11 A committed runbook covers every enumerated human-interaction /
  human-held-secret step (delivery browser auth; re-pair site-data clear and reopen;
  search-provider API key; Anthropic key; interactive HostAdapter restart; initial
  host token file provisioning).
- [ ] AC-12 The spec distinguishes fully-automatable steps from
  human-interaction-required steps for all three capabilities, and human-held-secret
  steps are covered by the committed runbook rather than left as silent manual
  blockers.
- [ ] AC-13 Token generation remains out of scope: `Invoke-OpenClawAgentOnboarding.ps1`
  is unchanged and no new token-generation path is introduced for the gateway token.
- [ ] AC-14 The dependency on child B (installer-image-version-alignment) is stated,
  and version-dependent specifics (`web_search` provider config keys, Control UI
  port/URL) are pinned against B's aligned image rather than assumed in this worktree.
- [ ] AC-15 No production, test, or reusable script file exceeds 500 lines; scripts are
  PowerShell 7+ advanced functions with `CmdletBinding()` and validated named
  parameters.
- [ ] AC-16 Pester v5 tests mirror the production structure under `tests/scripts/...`,
  mock the wrapper seams (not executables), are deterministic (no network, no temp
  files, no sleeps), and achieve line coverage >= 85% and branch coverage >= 75% with
  no regression on changed lines.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (runbook, docs/features/active/... links)
- [ ] Toolchain pass completed (format -> lint -> test)

## Seeded Test Conditions (from potential)
- [ ] Unit coverage for token-delivery fragment construction and device-token rotation.
- [ ] Negative/error paths (missing token, unreadable token file, invalid provider config).
- [ ] Idempotency of rotation and provider provisioning.
- [ ] `web_search` provider config produces a valid agent configuration.
