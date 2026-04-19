# Code Review — Bundle Install Script (Issue #36) — Refinement Cycle

- Date: 2026-04-19
- Review Timestamp: 2026-04-19T17-50
- Feature Branch: `feature/bundle-install-script-36`
- Base Branch: `development`
- Merge-Base SHA: `7bd92a8cb772c8f41a85831416a5fec952a2330b`
- HEAD SHA: `cda01a8e8e2f829f20e81dfe487ed82b579d1507`
- Commit range: `7bd92a8..cda01a8` (2 commits)
- Files under review:
  - Production: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Publish.ps1` (modified), `scripts/Publish.Helpers.psm1` (modified)
  - Tests: `tests/scripts/Install.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`, `tests/scripts/Publish.Tests.ps1` (modified), `tests/scripts/Publish.Helpers.Tests.ps1` (modified)
  - Documentation: `README.md`, `docs/mailbridge-runbook.md`

## Executive Summary

This review covers the full branch range `7bd92a8..cda01a8` including the post-refinement commit `cda01a8` that restructures bundle discovery: the install scripts are now staged into every bundle by `scripts/Publish.ps1`, `scripts/Install.ps1` self-locates via `$PSScriptRoot`, the pre-refinement auto-detect helper `Find-NewestPublishVersion` is retired, `-Version` is removed from `Install.ps1`, a `Get-ManifestVersion` helper is added, `Test-ManifestIntegrity` is updated for the post-refinement `{ version, files }` manifest schema, and `Write-PublishManifest` emits that schema.

The refinement is coherent and well-tested. The orchestrator stage order is preserved and a new stage 5 (install-script staging) is inserted before stage 6 (manifest emission) so the manifest includes the staged scripts. The `Copy-InstallScriptsIntoBundle` helper uses `SupportsShouldProcess` and fails fast with a specific path when a source script is missing. `Publish.Helpers.Tests.ps1` adds schema-assertion tests for the new manifest shape, and `Publish.Tests.ps1` adds a stage-ordering assertion for stage 5.

Test suite passes 150 / 150 in ~11.7 s. Coverage on the five in-scope files is 95.47 % repo-scoped, with per-file values of 91.01 % / 95.92 % / 93.75 % / 97.14 % / 97.08 %. Formatter and analyzer report zero diagnostics.

No blocker findings were identified. One Minor finding (documentation cross-reference polish, carried forward from the prior cycle) and three Informational observations are captured below. None block the PR.

Aggregate verdict: ready to merge.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | `docs/mailbridge-runbook.md` | Install Path A section | The refinement-updated runbook still adds Path D cross-references to Path B and Path C but not to Path A (the scheduled-task path). Operators reading Path A will not be pointed at the scripted bundle path. | Append a one-line cross-reference to Path D at the end of Install Path A, matching the pattern used on Paths B and C. Non-blocking polish. | Keeps the runbook's cross-reference pattern consistent across all manual install paths. The plan Q3 decision explicitly scoped cross-references to Paths B and C, so strict plan compliance is met; this is a consistency polish. | `docs/mailbridge-runbook.md` Path A (lines around 329 / 469 / 471); plan task P4-T5 |
| Informational | `scripts/Install.Helpers.psm1` | `Wait-ComposeHealthy` line 327 | `Write-Information "[compose:health] elapsed=${elapsed}s (timeout=${TimeoutSeconds}s)"` is emitted every poll cycle with `-InformationAction Continue`. With the refinement's 90 s ceiling and 3 s interval, worst case produces ~30 console lines. | Consider gating the elapsed-time line to every Nth cycle (e.g., every 10 s) to reduce operator console noise while preserving the audit trail. Not a policy violation; documented as an intentional feedback cadence choice (plan Q1). | Cadence rationale is documented in the plan Q1 decision. Optional polish. | `scripts/Install.Helpers.psm1:327`; plan Q1 rationale |
| Informational | `scripts/Install.Helpers.psm1` | `Test-ManifestIntegrity` lines 111-120 | The on-disk file enumeration uses `$file.FullName.Substring($BundleRoot.Length)` to compute the relative path. When `$BundleRoot` has a trailing separator (uncommon, but possible if the caller passes a path with a trailing `\`), the substring computation is still correct because the helper normalizes with `TrimStart(...)`. The comparison is `OrdinalIgnoreCase` against forward-slash relative paths. Correct on Windows; pre-existing behavior. | No change required. The comment in section 8 of the policy audit notes the Windows-only precondition. Flagged for future cross-platform consideration if the script ever runs on non-Windows hosts. | Implementation is correct for the current Windows-only scope (`Add-AppxPackage` restricts the feature to Windows regardless). | `scripts/Install.Helpers.psm1:111-120`; `spec.md` Constraints & Risks |
| Informational | `scripts/Install.ps1` | Stage 3 prior-install detection lines 114-116 | `$InstallRecordPath` and `$DestinationPath` are computed using `Join-Path` with `'OpenClaw/install-record.json'` / `"OpenClaw/$ResolvedVersion"` (forward slash inside the second argument). On Windows, `Join-Path` normalizes to backslashes; on the match side, `Test-Path` and later `Remove-Item` accept either separator. Behavior is correct but the mixed separators can be confusing to readers. | Optional: switch to `Join-Path $env:LOCALAPPDATA 'OpenClaw' 'install-record.json'` (chained `Join-Path` calls) to make the intent explicit. Not a bug; purely stylistic. | The current form is idiomatic PowerShell and every test exercising prior-install detection passes. Flagged for readability only. | `scripts/Install.ps1:114-116` |

## Additional Review Observations (non-finding)

These items were considered during review and intentionally judged acceptable; they are recorded here for auditability.

1. **Self-locating orchestrator via `$PSScriptRoot`**: `Install.ps1` now uses `[string]$SourcePath = $PSScriptRoot` as the default. The test that covers this (`defaults -SourcePath to $PSScriptRoot when not supplied`) resolves the expected path via `Resolve-Path (Split-Path -Path $script:ScriptPath -Parent)`, which correctly models the production invocation. The refinement commit message and `spec.md` clearly document the intent.

2. **`-Version` removal as a breaking change**: `-Version` is removed from `Install.ps1` in the refinement. This is a pre-release breaking change on a feature that has never shipped. No external callers exist. The test suite explicitly asserts `Find-NewestPublishVersion` is never called (`It '-SourcePath overrides the default $PSScriptRoot'` asserts `($global:InstallTestCalls -contains 'Find-NewestPublishVersion') | Should -BeFalse`). Clean cut.

3. **Post-refinement manifest schema `{ version, files }`**: `Write-PublishManifest` builds the manifest as `[pscustomobject]@{ version = $Version; files = @($sortedEntries) }`. `Get-ManifestVersion` reads `$manifest.version` and asserts a 4-part `[System.Version]`. `Test-ManifestIntegrity` asserts both `version` and `files` are present at the top level and throws a schema-violation message otherwise. The schema change is atomic across writer, reader, and integrity checker.

4. **`Copy-InstallScriptsIntoBundle` invariants**: The helper verifies each source path exists before copying and throws with the missing path; this makes the bundle-staging stage fail fast if the repo layout drifts. It uses `Copy-Item -LiteralPath -Force` to overwrite any prior staging residue.

5. **`Wait-ComposeHealthy` deadline math**: The elapsed-seconds computation uses `[int](($TimeoutSeconds) - ($deadline - (Get-Date)).TotalSeconds)`. This is correct when `deadline - now` is positive; the loop exits before negative values can appear. The timeout error path enumerates the last observed state and names the failing service.

6. **JSON payload handling in `Wait-ComposeHealthy`**: The helper accepts two docker compose output shapes: a JSON array starting with `[` and one JSON object per line. Both are parsed into `$entries`, and unrecognized payloads fall through to an empty array with a retry on the next cycle. The try/catch treats malformed JSON as transient, and a dedicated test (`It 'treats malformed JSON as transient and retries until timeout'`) asserts this.

7. **Uninstall failure collection**: `scripts/Uninstall.ps1` catches each step's exception, appends a `{ Step; Error }` record, and re-throws a single terminating error enumerating every failed step. The test suite asserts both "all steps run" and "the message enumerates every failed step" semantics.

8. **Test-scope `$global:` usage**: Tests use `$global:InstallTestCalls`, `$global:UninstallTestCalls`, `$global:UninstallRemoveItemPaths`, and `function global:Test-IsElevatedAdmin` to bridge mock and orchestrator scopes. Two `PSAvoidGlobalVars` suppressions document this with justification. Production code contains no `$global:` writes.

9. **`Publish.ps1` stage ordering**: Stage 5 (`Copy-InstallScriptsIntoBundle`) runs after stage 4 (MSIX pipeline) and before stage 6 (`Write-PublishManifest`) so the manifest lists the staged install scripts. `tests/scripts/Publish.Tests.ps1` asserts this ordering, and the comment block in the orchestrator records the intent.

10. **Install-record destination computation**: `$InstallRecordPath = Join-Path $env:LOCALAPPDATA 'OpenClaw/install-record.json'` produces a path under the OpenClaw parent directory, sibling to `%LOCALAPPDATA%\OpenClaw\MailBridge\`, which is exactly what the uninstall preserves. Tests assert `Remove-Item` is never invoked against a path matching `OpenClaw[\\/]MailBridge`.

## Test Quality Notes

- Test `Describe` / `Context` headings are behavior-named, not file- or function-named. Failure messages point directly at the failing behavior.
- `BeforeEach` rebuilds mocks per test to preserve independence.
- Stage-ordering tests use `[System.Collections.ArrayList]` call logs and index comparisons, robust to unrelated intervening calls (`New-Item`, `Remove-Item`).
- `-WhatIf` is asserted on both orchestrators and on every state-changing helper, validating the `SupportsShouldProcess` contract.
- The new `Get-ManifestVersion` helper has four distinct tests (valid version, missing file, missing field, unparseable value), covering the contract's positive and three negative branches.
- `Test-ManifestIntegrity` has five distinct tests including the new schema-violation case.
- The "bundle-root self-location" context adds coverage for the `$PSScriptRoot` default and the missing-manifest abort, both introduced by the refinement.

## Documentation Review

- `README.md`: the bullet under "What It Does" now reads "Run `.\Install.ps1` from inside an `artifacts/publish/<version>/` bundle", accurately reflecting the post-refinement behavior. The Repository Layout `scripts/` row documents that `Install.ps1` / `Uninstall.ps1` / `Install.Helpers.psm1` are additionally staged into every bundle by `Publish.ps1`. No emojis, no promotional phrasing.
- `docs/mailbridge-runbook.md`: Install Path D is present with prerequisites, invocations, uninstall command, outputs, and operator notes. Path D text has been updated for the refinement (`cd` into bundle, run `.\Install.ps1`). Troubleshooting rows cover the three new failure modes. Path A content is preserved (verified via `evidence/other/runbook-path-preservation.2026-04-18T00-00.md` for the pre-refinement baseline; the refinement commit did not touch Path A content).

## Blockers

None.

## Summary

| Severity | Count |
|---|---|
| Blocker | 0 |
| Major | 0 |
| Minor | 1 (runbook Path A cross-reference polish; carried forward from prior cycle) |
| Informational | 3 (elapsed-time log cadence, relative-path normalization note, Join-Path separator consistency) |

Recommended action: merge. The single Minor finding is a documentation polish outside the plan's explicit scope; it is noted for future consistency.
