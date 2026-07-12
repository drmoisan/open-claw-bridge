# admin-access-automation - Plan

- **Issue:** #148
- **Parent (optional):** openclaw-runtime-remediation (child C, wave 1)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-11
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata: `- Work Mode: full-feature`)

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- PowerShell Standards: `.claude/rules/powershell.md`
- Quality Tiers: `.claude/rules/quality-tiers.md`
- Evidence Conventions: `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`
- AC source (authoritative, full-feature dual source):
  - `docs/features/active/2026-07-11-admin-access-automation-148/spec.md`, `## Acceptance Criteria` (AC-1 through AC-16).
  - `docs/features/active/2026-07-11-admin-access-automation-148/user-story.md`, `## Acceptance Criteria` (US-1.1 through US-4.3).
  - The two documents are kept in sync; both AC sets are checked off during execution (Phase 5).
- Research: `docs/features/active/2026-07-11-admin-access-automation-148/research/2026-07-11T20-00-admin-access-automation-research.md`

**All work must comply with these policies; do not duplicate their content here.**

## Acceptance Criteria (spec.md AC-1..AC-16, with user-story cross-refs)

- AC-1 (US-1.1): Construct Control UI URL `http://127.0.0.1:<OPENCLAW_AGENT_PORT>/#token=<OPENCLAW_GATEWAY_TOKEN>` from the target `.env`, port default `18789`.
- AC-2 (US-1.2): base64url gateway token used in the fragment without re-encoding or mutation.
- AC-3 (US-1.3): missing/empty `OPENCLAW_GATEWAY_TOKEN` fails with a clear error pointing to `Invoke-OpenClawAgentOnboarding.ps1` and emits no malformed URL.
- AC-4 (US-1.4): gateway token value never appears in output, verbose, debug, or log stream during delivery.
- AC-5 (US-2.1): rotation writes a new cryptographically-generated non-empty secret to the host token file before restarting consumers.
- AC-6 (US-2.2): rotation restarts `openclaw-core` and `openclaw-agent` through the `Invoke-OpenClawDockerCommand` seam (never a direct `docker` call).
- AC-7 (US-2.3, US-2.4): rotation gated by `ShouldProcess` for the file write and each restart, and idempotent (no rotation of an already-valid token without an explicit force flag).
- AC-8 (US-2.5): rotation fails explicitly on unreadable/unwritable token file and on docker restart failure, and directs the operator to the runbook when the host token file is absent (no silent placeholder).
- AC-9 (US-3.1): `web_search` provider provisioning adds/validates a provider entry in `deploy/docker/openclaw-assistant/openclaw.json` referencing the provider API key via a SecretRef-style env interpolation, no hard-coded key.
- AC-10 (US-3.2, US-3.3): provisioning made in the baked seed (persisted via image rebuild), idempotent (no duplicate provider entries), validates the resulting JSON, and fails explicitly on invalid JSON or a missing referenced provider key env var.
- AC-11 (US-4.1, US-4.2, US-4.3): committed runbook covers every enumerated human-interaction / human-held-secret step and is cross-linked from the canonical operator runbook.
- AC-12: fully-automatable vs human-interaction-required steps are distinguished for all three capabilities; human-held-secret steps are covered by the runbook.
- AC-13 (US non-goal): token generation out of scope; `Invoke-OpenClawAgentOnboarding.ps1` unchanged; no new gateway-token generation path.
- AC-14: dependency on child B stated; version-dependent specifics (`web_search` provider config keys, Control UI port/URL) pinned against B's aligned image, not assumed in this worktree.
- AC-15: no production, test, or reusable script file exceeds 500 lines; scripts are PowerShell 7+ advanced functions with `CmdletBinding()` and validated named parameters.
- AC-16 (US-2.6, US-3.4): Pester v5 tests mirror production structure under `tests/scripts/...`, mock the wrapper seams (not executables), are deterministic (no network, no temp files, no sleeps), and achieve line coverage >= 85% and branch coverage >= 75% with no regression on changed lines.

## Explicit Scope and Non-Goals

- Three new production PowerShell scripts (one per capability), each an advanced function file:
  - `scripts/Get-OpenClawControlUiTokenUrl.ps1` (capability 1, pure read-only URL composition).
  - `scripts/Invoke-OpenClawDeviceTokenRotation.ps1` (capability 2, state-changing rotation with `SupportsShouldProcess`).
  - `scripts/Set-OpenClawWebSearchProvider.ps1` (capability 3, state-changing seed edit with `SupportsShouldProcess`).
- Three mirrored Pester v5 test files under `tests/scripts/`:
  - `tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1`.
  - `tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1`.
  - `tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1`.
- One seed config edit: `deploy/docker/openclaw-assistant/openclaw.json` (JSON seed; not a PowerShell file, not counted against the PowerShell per-batch cap).
- One committed runbook change: `docs/mailbridge-runbook.md` (Markdown; exempt from the 500-line code cap).
- Reuse (no change expected): `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` (`Get-OpenClawEnvFileMap` parser seam; `Invoke-OpenClawDockerCommand` docker seam) and `scripts/Invoke-OpenClawAgentOnboarding.ps1` (token generation unchanged, out of scope).
- Out of scope: token generation, generating/issuing human-held secrets, automating the browser-side Control UI authentication and device-pairing handshake, child B's installer files, branch protection or required checks.

## Conventions Used by This Plan

- `FEATURE` = `docs/features/active/2026-07-11-admin-access-automation-148`.
- `<ts>` = ISO-8601 timestamp `yyyy-MM-ddTHH-mm` captured at artifact-creation time.
- All evidence artifacts live under `FEATURE/evidence/<kind>/` (canonical kinds used by this plan: `baseline/`, `regression-testing/`, `qa-gates/`, `other/`). No evidence may be written under any `artifacts/` sub-path. Every command-step evidence artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. No EVIDENCE_LOCATION_OVERRIDE_REJECTED is recorded: the delegation supplied only canonical `evidence/<kind>/` paths.
- **PowerShell toolchain loop:** run `mcp__drm-copilot__run_poshqc_format` -> `mcp__drm-copilot__run_poshqc_analyze` -> `mcp__drm-copilot__run_poshqc_test` in that order, repo-wide. If any step fails or changes files, restart the loop from format. Type checking is not applicable to PowerShell.
- **PowerShell coverage tooling note (established workaround):** `mcp__drm-copilot__run_poshqc_test` in coverage mode fails on every invocation in this repository because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to files that exist only in the `drm-copilot` source repository (reproduced defect F11 `#111`, F16 `#125`, `#135`, `#137`, `#139`). The numeric-coverage source for both baseline and final-QC PowerShell tasks is: import the bundled `PoshQC.psd1` module directly and call `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>`, where `<corrected>` is a SCRATCHPAD-only copy of the bundled runsettings with `CodeCoverage.Path` rewritten to this repository's actual production PowerShell files under `scripts/**` (full glob, no `ExcludedPath` entry per the Coverage Exclusion Policy). Record both the failing MCP invocation and the corrected-runsettings invocation in the evidence artifact.
- **Change budget:** three new production PowerShell files exceed the direct-mode 2-production-file scope in `.claude/rules/powershell.md`, so this work is routed to `powershell-orchestrator` and executed in three per-capability batches. Each batch adds exactly one production file and one test file (1+1), well within the per-batch cap of 3 production + 3 test files. No batch-cap override is required.
- **Design seams (reuse existing, no new frameworks):**
  - Capability 1 reuses `Get-OpenClawEnvFileMap` from `OpenClawContainerValidation.psm1` (FR-1.4); tests drive it with in-memory pseudo-files by mocking `Test-Path`/`Get-Content` exactly as `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1` does.
  - Capability 2 restarts containers only through `Invoke-OpenClawDockerCommand -ExecutablePath <docker> -CommandArguments @('restart', '<container>')` (FR-2.7); tests mock that wrapper seam with a `param([string]$ExecutablePath, [string[]]$CommandArguments)` signature and never mock `docker` directly.
  - Capability 2 generates the new secret with `System.Security.Cryptography.RandomNumberGenerator` producing a base64url string, mirroring the RNG shape in `scripts/Invoke-OpenClawAgentOnboarding.ps1` (`New-OpenClawGatewayToken`, lines 76-101); the RNG helper is defined locally in the rotation script (the onboarding helper is script-scoped and its file is out of scope). RNG output is tested for shape/charset only (NFR-D2).
  - Capability 3 reads/writes the seed via `Get-Content`/`Set-Content` (mocked in-memory in tests) and parses/serializes with real `ConvertFrom-Json`/`ConvertTo-Json`.
- **Line-count check (performed during planning):** all six target files are new, so none is at or near the 500-line cap; no sibling-add split is required. Existing files listed as reuse are not modified.
- **Child B dependency (AC-14):** child B (`installer-image-version-alignment`, wave 0) provides matched Control UI + gateway container images. B's files are NOT present in this worktree; this plan cites B's matched-image guarantee as an input assumption and does not read B's artifacts. The concrete `web_search` provider name and config key schema and the Control UI port/URL are pinned against B's aligned image at execution time (P3-T1), with the research fallback shape (`plugins.entries.<provider>.config.webSearch.apiKey` = `${WEB_SEARCH_API_KEY}`, research section D.2) used only if B confirms it.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [ ] [P0-T1] Read `CLAUDE.md` at the repository root.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` is created (or appended to) recording that `CLAUDE.md` was read, with a `Timestamp:` field.
- [ ] [P0-T2] Read `.claude/rules/general-code-change.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-code-change.md` as read.
- [ ] [P0-T3] Read `.claude/rules/general-unit-test.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-unit-test.md` as read.
- [ ] [P0-T4] Read `.claude/rules/powershell.md` and `.claude/rules/quality-tiers.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/powershell.md` and `.claude/rules/quality-tiers.md` as read.
- [ ] [P0-T5] Finalize the Phase 0 policy-read evidence artifact covering P0-T1 through P0-T4.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` exists containing `Timestamp:`, `Policy Order:` (listing the files in the exact order read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`, `.claude/rules/quality-tiers.md`), and an explicit list of files read.
- [ ] [P0-T6] Capture the PowerShell format baseline: run `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) against the current pre-change repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail status and count of files changed by the run).
- [ ] [P0-T7] Capture the PowerShell analyze baseline: run `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) against the current pre-change repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (diagnostic counts by severity, expected 0 errors).
- [ ] [P0-T8] Capture the PowerShell test-and-coverage baseline: run `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround (see Conventions) and record the numeric result.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>` invocation), `EXIT_CODE:` for each, and `Output Summary:` containing pass/fail test counts and the numeric repo-wide baseline line-coverage and branch-coverage percentages (no placeholders such as UNVERIFIED).

### Phase 1 — Capability 1: Gateway-Token Delivery via `#token=` URL (AC-1, AC-2, AC-3, AC-4, AC-13)

- [ ] [P1-T1] Create `tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1` with a `#Requires -Version 7.0` header, the `PSAvoidGlobalVars` suppression convention used by `tests/scripts/Invoke-OpenClawAgentOnboarding.Tests.ps1`, a `BeforeAll` defining `$script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Get-OpenClawControlUiTokenUrl.ps1'` and importing `OpenClawContainerValidation.psm1`, and a top-level `Describe 'scripts/Get-OpenClawControlUiTokenUrl.ps1'` block with in-memory `Test-Path`/`Get-Content` mocks serving a `$global:DeliveryTestFiles` hashtable.
  - Acceptance: the file exists and contains `Describe 'scripts/Get-OpenClawControlUiTokenUrl.ps1'` and the in-memory file-mock scaffold; no temp files are created.
- [ ] [P1-T2] [expect-fail] Author the capability-1 behavior tests in `tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1` as `It` blocks (one behavior each): (a) valid token + default port returns `http://127.0.0.1:18789/#token=<token>`; (b) valid token + explicit `OPENCLAW_AGENT_PORT` returns the URL with that port; (c) base64url token appears verbatim in the fragment (no re-encoding); (d) missing `OPENCLAW_GATEWAY_TOKEN` throws an error whose message names `Invoke-OpenClawAgentOnboarding.ps1` and emits no URL; (e) empty/whitespace token throws the same guided error; (f) the token value never appears in the captured output/verbose/debug streams. Run the file in targeted mode against the not-yet-created production script and confirm failure.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-c1-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:` (targeted Pester invocation), `EXIT_CODE:` (non-zero / test-failure), `Output Summary:` recording that the six `It` blocks fail because the production script is absent.
- [ ] [P1-T3] Create `scripts/Get-OpenClawControlUiTokenUrl.ps1` with `#Requires -Version 7`, comment-based help, `[CmdletBinding()]` (read-only, no `ShouldProcess`), and a `param()` block declaring `-EnvFilePath [string]` (default `'./.env'`, `[Parameter(Mandatory = $false)]`); import `OpenClawContainerValidation.psm1` by path.
  - Acceptance: Grep of `scripts/Get-OpenClawControlUiTokenUrl.ps1` matches `CmdletBinding()`, `-EnvFilePath`, and an `Import-Module` of `OpenClawContainerValidation`.
- [ ] [P1-T4] Implement URL composition in `scripts/Get-OpenClawControlUiTokenUrl.ps1`: read the `.env` via `Get-OpenClawEnvFileMap`, resolve `OPENCLAW_AGENT_PORT` (default `18789` when absent/empty), read `OPENCLAW_GATEWAY_TOKEN`, and return `http://127.0.0.1:<port>/#token=<token>` with the token inserted verbatim (no encoding).
  - Acceptance: AC-1, AC-2 — the capability-1 tests (a), (b), (c) from P1-T2 pass in targeted mode; the returned string matches the required shape for both default and explicit port.
- [ ] [P1-T5] Implement the missing/empty-token guard in `scripts/Get-OpenClawControlUiTokenUrl.ps1`: when `OPENCLAW_GATEWAY_TOKEN` is absent or `[string]::IsNullOrWhiteSpace`, `throw` a clear error naming `Invoke-OpenClawAgentOnboarding.ps1` and return no URL.
  - Acceptance: AC-3 — capability-1 tests (d) and (e) pass; no URL string is emitted on the error paths.
- [ ] [P1-T6] Ensure the token value is never written to any output, verbose, debug, or log stream in `scripts/Get-OpenClawControlUiTokenUrl.ps1`: the only object returned is the constructed URL, and no `Write-Verbose`/`Write-Debug`/`Write-Host`/`Write-Output` statement emits the raw token value.
  - Acceptance: AC-4 — capability-1 test (f) passes; a Grep of the script shows no logging statement that references the token value.
- [ ] [P1-T7] Verify capability-1 file sizes and the out-of-scope invariant.
  - Acceptance: AC-15 — `(Get-Content scripts/Get-OpenClawControlUiTokenUrl.ps1).Count` <= 500 and `(Get-Content tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1).Count` <= 500; AC-13 — `git diff scripts/Invoke-OpenClawAgentOnboarding.ps1` returns no output.
- [ ] [P1-T8] Run the PowerShell toolchain loop (`run_poshqc_format` -> `run_poshqc_analyze` -> `run_poshqc_test`) for the capability-1 batch; restart from format if any step fails or changes files.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-c1-post-pass.<ts>.md` exists with `Timestamp:`, `Command:` for each of the three steps, `EXIT_CODE: 0` for each, and `Output Summary:` confirming all six capability-1 `It` blocks now pass with no formatting/lint changes required on the recorded clean pass.

### Phase 2 — Capability 2: Device-Token Rotation/Reissue (AC-5, AC-6, AC-7, AC-8)

- [ ] [P2-T1] Create `tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1` with a `#Requires -Version 7.0` header, the `PSAvoidGlobalVars` suppression convention, a `BeforeAll` defining `$script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Invoke-OpenClawDeviceTokenRotation.ps1'` and importing `OpenClawContainerValidation.psm1`, in-memory `Test-Path`/`Get-Content`/`Set-Content` mocks over `$global:RotationTestFiles`, and a mock of `Invoke-OpenClawDockerCommand` with signature `param([string]$ExecutablePath, [string[]]$CommandArguments)` recording calls to `$global:RotationDockerCalls`.
  - Acceptance: the file exists, contains `Describe 'scripts/Invoke-OpenClawDeviceTokenRotation.ps1'`, and mocks the `Invoke-OpenClawDockerCommand` wrapper seam (not `docker`) with matching named parameters.
- [ ] [P2-T2] [expect-fail] Author the capability-2 behavior tests as `It` blocks (one behavior each): (a) with `-Force` and an existing token file, a new non-empty base64url secret is written to the host token file BEFORE any docker restart is recorded; (b) rotation records `Invoke-OpenClawDockerCommand` restart calls for `openclaw-core` and `openclaw-agent` (args `@('restart','openclaw-core')` and `@('restart','openclaw-agent')`); (c) `-WhatIf` performs no file write and no restart (ShouldProcess gate); (d) without `-Force`, an already-valid (present, non-empty) token file is not rotated and no restart occurs (idempotent no-op); (e) an unwritable token file throws explicitly; (f) a docker restart failure (`Succeeded = $false`) throws explicitly; (g) an absent host token file throws an error directing to the runbook and creates no placeholder file; (h) the token value never appears in output/verbose/debug streams; (i) the generated secret matches the base64url charset (shape, not exact value). Run in targeted mode and confirm failure.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-c2-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), `Output Summary:` recording that the nine `It` blocks fail because the production script is absent.
- [ ] [P2-T3] Create `scripts/Invoke-OpenClawDeviceTokenRotation.ps1` with `#Requires -Version 7`, comment-based help, `[CmdletBinding(SupportsShouldProcess = $true)]`, and a `param()` block declaring `-EnvFilePath [string]` (default `'./.env'`), `-TokenFilePath [string]` (optional explicit override), `-DockerExecutablePath [string]` (default resolves `docker`), `-TokenByteLength [int]` (`[ValidateRange(24,128)]`, default 48), `-CoreContainerName [string]` (default `'openclaw-core'`), `-AgentContainerName [string]` (default `'openclaw-agent'`), and `-Force [switch]`; import `OpenClawContainerValidation.psm1`.
  - Acceptance: AC-15 — Grep matches `CmdletBinding(SupportsShouldProcess = \$true)` and each declared parameter with its validation attribute.
- [ ] [P2-T4] Implement token-file-path resolution and the absent-file guard in `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`: resolve the host token file from `-TokenFilePath` or `HOSTADAPTER_TOKEN_FILE` in the `.env` (via `Get-OpenClawEnvFileMap`); when the resolved file does not exist, `throw` an error directing the operator to the runbook and create no file.
  - Acceptance: AC-8 (absent-file half) — capability-2 test (g) passes; the error message references the runbook and no `Set-Content` occurs on the absent-file path.
- [ ] [P2-T5] Implement RNG secret generation and idempotent write-first ordering in `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`: when the file exists and is non-empty and `-Force` is not supplied, no-op with a verbose (value-free) message; otherwise generate a new non-empty base64url secret via `System.Security.Cryptography.RandomNumberGenerator` and write it to the host token file, gated by `$PSCmdlet.ShouldProcess(<path>, 'Write device token')`, before any restart.
  - Acceptance: AC-5, AC-7 (idempotency + file-write ShouldProcess halves) — capability-2 tests (a), (c file-write half), (d), (i) pass; the write occurs strictly before restart calls.
- [ ] [P2-T6] Implement the consumer restarts in `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`: after the secret is written, restart `-CoreContainerName` then `-AgentContainerName` via `Invoke-OpenClawDockerCommand -ExecutablePath $DockerExecutablePath -CommandArguments @('restart', <name>)`, each gated by `$PSCmdlet.ShouldProcess(<name>, 'Restart container')`; treat a returned `Succeeded = $false` (or null exit) as failure and `throw` explicitly.
  - Acceptance: AC-6, AC-7 (restart ShouldProcess half), AC-8 (docker-failure half) — capability-2 tests (b), (c restart half), (f) pass; a Grep confirms no direct `docker ` invocation appears in the script.
- [ ] [P2-T7] Implement the unwritable-file and no-secret-logging guarantees in `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`: a failed token-file write surfaces an explicit error, and no code path writes the token value to output/verbose/debug/log streams.
  - Acceptance: AC-8 (unwritable half), AC-16 (secret-not-logged half) — capability-2 tests (e) and (h) pass; a Grep shows no logging statement referencing the secret value.
- [ ] [P2-T8] Verify capability-2 file sizes.
  - Acceptance: AC-15 — `(Get-Content scripts/Invoke-OpenClawDeviceTokenRotation.ps1).Count` <= 500 and `(Get-Content tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1).Count` <= 500.
- [ ] [P2-T9] Run the PowerShell toolchain loop for the capability-2 batch; restart from format if any step fails or changes files.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-c2-post-pass.<ts>.md` exists with `Timestamp:`, `Command:` for each step, `EXIT_CODE: 0` for each, and `Output Summary:` confirming all nine capability-2 `It` blocks pass with no formatting/lint changes required on the recorded clean pass.

### Phase 3 — Capability 3: `web_search` Provider Provisioning (AC-9, AC-10, AC-14)

- [ ] [P3-T1] Pin the version-dependent `web_search` provider name and config key schema and the Control UI port/URL against child B's aligned image (do not read B's artifacts; use B's matched-image guarantee). Record the pinned provider name, the concrete key path, and the SecretRef env var name; if B's schema is unavailable at execution time, record use of the research fallback shape (`plugins.entries.<provider>.config.webSearch.apiKey` = `${WEB_SEARCH_API_KEY}`, research section D.2).
  - Acceptance: AC-14 — `FEATURE/evidence/other/web-search-schema-pin.<ts>.md` exists with `Timestamp:`, the pinned provider name and key path, the SecretRef env var name, and an explicit statement of whether the pin came from B's aligned image or the documented fallback shape.
- [ ] [P3-T2] Create `tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1` with a `#Requires -Version 7.0` header, the `PSAvoidGlobalVars` suppression convention, a `BeforeAll` defining `$script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Set-OpenClawWebSearchProvider.ps1'`, in-memory `Test-Path`/`Get-Content`/`Set-Content` mocks over `$global:ProviderTestFiles` serving JSON strings, and a top-level `Describe 'scripts/Set-OpenClawWebSearchProvider.ps1'` block.
  - Acceptance: the file exists, contains `Describe 'scripts/Set-OpenClawWebSearchProvider.ps1'`, and uses in-memory JSON pseudo-files (no temp files).
- [ ] [P3-T3] [expect-fail] Author the capability-3 behavior tests as `It` blocks (one behavior each): (a) provisioning a seed with no provider entry adds a `web_search` provider entry whose API key is a SecretRef-style `${...}` interpolation and not a literal key; (b) re-running on an already-provisioned seed yields identical JSON with no duplicate provider entry (idempotent); (c) `-WhatIf` writes nothing (ShouldProcess gate); (d) invalid JSON input throws explicitly; (e) a missing referenced provider-key env var throws explicitly; (f) the resulting JSON is validated (round-trips through `ConvertFrom-Json`) and the pre-existing `gateway.auth.token = ${OPENCLAW_GATEWAY_TOKEN}` and `tools.profile = coding` entries are preserved. Run in targeted mode and confirm failure.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-c3-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero), `Output Summary:` recording that the six `It` blocks fail because the production script is absent.
- [ ] [P3-T4] Create `scripts/Set-OpenClawWebSearchProvider.ps1` with `#Requires -Version 7`, comment-based help, `[CmdletBinding(SupportsShouldProcess = $true)]`, and a `param()` block declaring `-ConfigPath [string]` (default the repo seed `deploy/docker/openclaw-assistant/openclaw.json`), `-ProviderName [string]` (the pinned provider name from P3-T1), and `-ApiKeyEnvVar [string]` (default the pinned SecretRef env var name, e.g. `WEB_SEARCH_API_KEY`), each with appropriate validation attributes.
  - Acceptance: AC-15 — Grep matches `CmdletBinding(SupportsShouldProcess = \$true)` and each declared parameter.
- [ ] [P3-T5] Implement idempotent provider provisioning in `scripts/Set-OpenClawWebSearchProvider.ps1`: read and parse the seed JSON; if the pinned provider entry already exists with the SecretRef, no-op; otherwise add the provider entry referencing the API key via `${<ApiKeyEnvVar>}` SecretRef (never a literal key) and write the seed gated by `$PSCmdlet.ShouldProcess(<ConfigPath>, 'Provision web_search provider')`.
  - Acceptance: AC-9, AC-10 (idempotency + ShouldProcess halves) — capability-3 tests (a), (b), (c) pass; a Grep of the script shows the SecretRef `${...}` form and no hard-coded key literal.
- [ ] [P3-T6] Implement the validation and failure guards in `scripts/Set-OpenClawWebSearchProvider.ps1`: fail explicitly with `throw` on invalid input JSON and on a missing referenced provider-key env var; validate that the serialized result re-parses via `ConvertFrom-Json` before writing.
  - Acceptance: AC-10 (validation + failure halves) — capability-3 tests (d), (e), (f) pass.
- [ ] [P3-T7] Apply the provisioning edit to the committed seed `deploy/docker/openclaw-assistant/openclaw.json` using the pinned schema from P3-T1 (add the `web_search` provider entry + SecretRef; no literal key).
  - Acceptance: AC-9 — `git diff deploy/docker/openclaw-assistant/openclaw.json` shows exactly the added provider entry referencing `${<ApiKeyEnvVar>}`; the file re-parses as valid JSON; the pre-existing `gateway`, `tools.profile`, and `agents` blocks are unchanged.
- [ ] [P3-T8] Verify capability-3 file sizes.
  - Acceptance: AC-15 — `(Get-Content scripts/Set-OpenClawWebSearchProvider.ps1).Count` <= 500 and `(Get-Content tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1).Count` <= 500.
- [ ] [P3-T9] Run the PowerShell toolchain loop for the capability-3 batch; restart from format if any step fails or changes files.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-c3-post-pass.<ts>.md` exists with `Timestamp:`, `Command:` for each step, `EXIT_CODE: 0` for each, and `Output Summary:` confirming all six capability-3 `It` blocks pass with no formatting/lint changes required on the recorded clean pass.

### Phase 4 — Capability 4: Committed Human-Held-Secret Runbook (AC-11, AC-12)

- [ ] [P4-T1] Add a committed admin-access runbook section to `docs/mailbridge-runbook.md` (or a dedicated admin-access runbook cross-linked from it) enumerating the six human-interaction / human-held-secret steps from the spec Automation Feasibility section: (1) open the `#token=` URL in a browser to complete Control UI authentication; (2) post-recreation site-data clear + reopen URL to re-pair (paired with automatable `openclaw devices clear`); (3) supply the search-provider API key into `.env`/secrets as a SecretRef; (4) supply the Anthropic API key in `secrets/.env.anthropic`; (5) provide/keep the initial HostAdapter device-token secret and restart an interactively-run HostAdapter during rotation; (6) provision the initial host token file value at `C:\ProgramData\OpenClaw\HostAdapter\adapter.token` when absent. Each step states what the operator supplies or does and where automation hands off to the operator.
  - Acceptance: AC-11 — `docs/mailbridge-runbook.md` contains all six enumerated steps, each with an operator action and handoff note, and the section is reachable from the canonical operator runbook (cross-linked or inline).
- [ ] [P4-T2] Cross-link the runbook to the three automation entry points and record the automatable-vs-human-interaction distinction for each capability (delivery URL emission automatable, browser auth human; rotation container restarts automatable, interactive HostAdapter restart + initial provisioning human; provisioning seed edit automatable, provider API key human).
  - Acceptance: AC-12 — the runbook section references `scripts/Get-OpenClawControlUiTokenUrl.ps1`, `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`, and `scripts/Set-OpenClawWebSearchProvider.ps1`, and explicitly separates automatable steps from human-interaction-required steps for all three capabilities.

### Phase 5 — Final QC (Full PowerShell Toolchain Loop + Coverage Verification + AC Sync)

- [ ] [P5-T1] Run the full repo-wide PowerShell toolchain loop in order: `mcp__drm-copilot__run_poshqc_format`, then `mcp__drm-copilot__run_poshqc_analyze`, then `mcp__drm-copilot__run_poshqc_test`. If any step fails or changes files, restart from format until a single clean pass completes.
  - Acceptance: `FEATURE/evidence/qa-gates/final-format.<ts>.md`, `FEATURE/evidence/qa-gates/final-analyze.<ts>.md`, and `FEATURE/evidence/qa-gates/final-test.<ts>.md` each exist with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:`; the format and analyze artifacts confirm 0 changes / 0 errors on the clean pass, and the test artifact confirms the full Pester suite passes.
- [ ] [P5-T2] Capture final-QC numeric coverage: run `mcp__drm-copilot__run_poshqc_test` in coverage mode and, when it fails on the known coverage-path defect, apply the corrected-runsettings workaround (see Conventions) to record numeric post-change coverage.
  - Acceptance: `FEATURE/evidence/qa-gates/final-coverage.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>` invocation), `EXIT_CODE:` for each, and `Output Summary:` recording numeric post-change repo-wide line-coverage and branch-coverage percentages (no placeholders).
- [ ] [P5-T3] Verify the coverage delta and thresholds: compare the P0-T8 baseline against the P5-T2 post-change coverage and compute new/changed-code coverage for the three new scripts.
  - Acceptance: AC-16 — `FEATURE/evidence/qa-gates/coverage-delta.<ts>.md` exists reporting baseline line/branch coverage, post-change line/branch coverage, and new/changed-code line/branch coverage for `scripts/Get-OpenClawControlUiTokenUrl.ps1`, `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`, and `scripts/Set-OpenClawWebSearchProvider.ps1`; line coverage >= 85% and branch coverage >= 75% with no regression on changed lines. If any required value is unavailable, this task is remediation-required and MUST NOT be reported as PASS.
- [ ] [P5-T4] Verify the 500-line cap across all six new files in a single check.
  - Acceptance: AC-15 — line counts for `scripts/Get-OpenClawControlUiTokenUrl.ps1`, `scripts/Invoke-OpenClawDeviceTokenRotation.ps1`, `scripts/Set-OpenClawWebSearchProvider.ps1`, `tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1`, `tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1`, and `tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1` are each <= 500; recorded in `FEATURE/evidence/qa-gates/file-size-check.<ts>.md`.
- [ ] [P5-T5] Confirm the out-of-scope reuse invariant: `scripts/Invoke-OpenClawAgentOnboarding.ps1` and `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` are unchanged.
  - Acceptance: AC-13 — `git diff scripts/Invoke-OpenClawAgentOnboarding.ps1 scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` returns no output.
- [ ] [P5-T6] Check off both acceptance-criteria sources and reconcile them: mark AC-1..AC-16 in `spec.md` and US-1.1..US-4.3 in `user-story.md` as satisfied, each mapped to the task(s) and evidence artifact(s) that verify it.
  - Acceptance: `FEATURE/evidence/qa-gates/ac-signoff.<ts>.md` exists with a per-AC mapping to task IDs and evidence paths for both `spec.md` and `user-story.md`; every AC is either checked with evidence or flagged remediation-required with the specific gap.
