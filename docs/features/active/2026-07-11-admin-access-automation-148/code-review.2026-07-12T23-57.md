# Code Quality Review — admin-access-automation (Issue #148)

- Timestamp: 2026-07-12T23-57
- Feature branch: `feature/admin-access-automation-148`
- Base: `origin/epic/openclaw-runtime-remediation-integration` (merge-base `f35ee45`)
- Reviewed commit: `fe1ad84`
- Language: PowerShell 7+

## Design and Structure

- Three cohesive, single-purpose scripts split by capability (delivery / rotation / provisioning), consistent with the design-principles ordering in `.claude/rules/general-code-change.md` (simplicity, separation of concerns). Pure URL composition is separated from state-changing rotation and provisioning.
- Reuse over reimplementation: `.env` parsing uses the shared `Get-OpenClawEnvFileMap` seam and restarts use the `Invoke-OpenClawDockerCommand` seam; no duplicated parsers or direct process calls.
- Rotation extracts two focused helpers (`New-OpenClawDeviceToken` pure RNG; `Restart-OpenClawConsumer` seam wrapper), keeping the main flow readable and the write-first ordering explicit.

## Correctness Observations

- Write-first ordering (FR-2.3) is implemented and directly tested: the write is recorded in `RotationCallOrder` before any `restart:` entry, asserted with `IndexOf('write') < FindIndex(restart:*)`.
- Idempotency guards are correct: rotation short-circuits on a non-empty existing token unless `-Force`; provisioning short-circuits when the SecretRef already matches. Provisioning's remove-then-add prevents duplicate entries on a forced re-provision.
- Error paths fail explicitly with runbook-directed messages (absent token file, missing `HOSTADAPTER_TOKEN_FILE`, missing provider env var, invalid JSON, docker restart failure, write failure). No silent placeholder creation.
- JSON handling in `Set-OpenClawWebSearchProvider.ps1` validates input JSON, uses a null-safe `Get-OpenClawChildProperty` traversal helper (avoids strict-mode property-missing errors), and re-parses the serialized output before writing. `ConvertTo-Json -Depth 20` is adequate for the seed's nesting depth.

## Secret Hygiene

- No token/secret is emitted to any log stream. The delivery URL intentionally carries the gateway token in its fragment (the documented delivery artifact); this is the single by-design exposure and is confined to the success-stream return value, consistent with spec FR-1.5.
- The RNG secret is disposed in a `finally` block. Secret shape (base64url charset, no `+/=`) is tested without asserting the exact value, preserving determinism.

## Style, Naming, Conventions

- Advanced functions with `CmdletBinding()`, `PascalCase` public parameters, validation attributes, comment-based help on all three scripts.
- The lone analyzer suppression (`PSUseShouldProcessForStateChangingFunctions` on `New-OpenClawDeviceToken`) is justified in-line: the function has no filesystem/network/process side effects. Appropriate and minimal.
- Format and analyze evidence report zero findings repo-wide on the final pass.

## Test Quality

- Deterministic in-memory pseudo-file model (hashtable + scoped mocks) avoids temp files and real I/O, matching the repository determinism requirements. Mocks target seams, not executables.
- Scenario coverage is thorough: happy path, explicit port, verbatim fragment, missing/empty token, WhatIf, idempotent no-op, unwritable file, docker-restart failure, absent-file runbook redirect, `.env` resolution, missing `HOSTADAPTER_TOKEN_FILE`, secret-not-logged, charset shape, invalid JSON, missing env var, round-trip preservation.
- Module-scope vs script-scope mock placement is handled correctly: `.env` parsing runs inside the module scope, so its file mocks use `-ModuleName OpenClawContainerValidation`, while the rotation script's own file operations are mocked in the default scope.

## Findings

| # | Severity | Finding |
|---|---|---|
| CR-1 | Non-blocking | Branch coverage is not directly measured; INSTRUCTION coverage is used as the branch proxy (no BRANCH counter in the Pester JaCoCo XML). Repo-precedent-accepted, but true branch coverage remains unverified. Both metrics clear thresholds. |
| CR-2 | Non-blocking | Property-based tests: the spec self-declares capabilities 1 and 2 as T1 token handling, but PowerShell scripts are not classified projects in `quality-tiers.yml` (only the .NET solution is), so the T1 property-density CI gate does not formally bind them. The pure functions (URL composition, base64url RNG encoding) are exercised by directed deterministic invariant tests (verbatim-fragment, charset-shape) rather than a property framework. If strict T1 property density is to be claimed, record a dated decision record per the repo's prior non-C# precedent. |
| CR-3 | Non-blocking | `evidence/qa-gates/file-size-check.2026-07-12T23-05.md` lists `Invoke-OpenClawDeviceTokenRotation.Tests.ps1` at 229 lines; the committed file is 260 lines (`wc -l`). Both are well under the 500-line cap, so AC-15 holds, but the evidence line count is stale relative to the committed file. |
| CR-4 | Non-blocking | `Set-OpenClawWebSearchProvider.ps1` reads the provider env var only to assert it is set (fail-fast); the value is never used in the written config (correct — SecretRef is env-interpolated at container runtime). This is intentional and matches FR-3.2; noted so it is not mistaken for a dead read. |

## Summary

No Blocking code-quality findings. The implementation is idiomatic PowerShell 7+, respects the established seams, fails fast with operator-directed messages, and keeps secrets out of log streams. Non-blocking items CR-1..CR-4 are observations and documentation-accuracy notes, not defects in the production code.
