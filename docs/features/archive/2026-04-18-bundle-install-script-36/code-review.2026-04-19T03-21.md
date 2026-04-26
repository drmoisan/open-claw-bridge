# Code Review — Bundle Install Script (Issue #36)

- Date: 2026-04-19
- Review Timestamp: 2026-04-19T03-21
- Feature Branch: `feature/unified-publish-script-34`
- Base Branch: `development`
- Merge-Base SHA: `7bd92a8cb772c8f41a85831416a5fec952a2330b`
- HEAD SHA: `453343e77121d4592e7179dda731a117b3d2b601`
- Files under review: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`, `README.md`, `docs/mailbridge-runbook.md`

## Executive Summary

The feature delivers three new PowerShell production files and three Pester test files that implement a scripted install and uninstall flow for bundles produced by `scripts/Publish.ps1`. Design follows the precedent `Publish.ps1` + `Publish.Helpers.psm1` split and honors the repository's 500-line per-file ceiling. Public helpers use `CmdletBinding`, approved verbs, mandatory-with-validation parameters, and `SupportsShouldProcess` for state-changing actions. Error paths are specific and actionable; the uninstall orchestrator correctly implements fail-collection-then-throw semantics. Test suite passes 143/143 with 86.39% repo-wide coverage and per-new-file coverage of 96.32% / 90.29% / 93.75%.

No blocker findings were identified. One **Minor** finding (documentation cross-reference to Path D omitted from Path A) and three **Informational** observations (diagnostic output level, null-safe spec alignment, and orchestrator happy-path integration testing) are captured below. None block the PR.

Aggregate verdict: ready to merge.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | `docs/mailbridge-runbook.md` | Install Path A section end | Plan task P4-T3/T4/T5 adds a Path D cross-reference to Path B and Path C but not to Path A. Operators reading Path A will not be pointed at the scripted bundle path. | Append a one-line cross-reference to Path D at the end of Install Path A (non-blocking; operator discoverability only). | Keeps the runbook's cross-reference pattern consistent across all manual paths. The plan explicitly scoped the cross-reference to Paths B and C, so strict spec compliance is met; this is a documentation polish item. | `docs/mailbridge-runbook.md` diff; plan task P4-T4 / P4-T5 |
| Informational | `scripts/Install.Helpers.psm1` | `Wait-ComposeHealthy` line 311 | `Write-Information "[compose:health] elapsed=${elapsed}s"` is emitted every poll cycle with `-InformationAction Continue`. On the 30-cycle worst case this produces 30 console lines. | Consider gating the elapsed-time line to every Nth cycle or emitting only at cycle boundaries (e.g., every 10 s) to reduce operator console noise while preserving the audit trail on slower hosts. | Not a policy violation; feedback cadence rationale is documented in the plan Q1 decision. Optional polish. | `scripts/Install.Helpers.psm1:311`; plan Q1 rationale |
| Informational | `scripts/Install.Helpers.psm1` | `Test-ManifestIntegrity` lines 99-101 | Relative-path comparison between on-disk files and manifest entries normalizes `[IO.Path]::DirectorySeparatorChar` to `/` via `-replace` using `[regex]::Escape`. Correct on Windows (`\` -> `/`), but if the helper is ever exercised on a non-Windows host (not in current scope), the replacement is a no-op and the OrdinalIgnoreCase hash-set comparison still works. | Keep the normalization as-is. Document the Windows-only precondition (already stated in spec Constraints & Risks). | Implementation is correct for the current Windows-only scope. Noted for future cross-platform work. | `scripts/Install.Helpers.psm1:99-101`; `spec.md` Constraints & Risks |
| Informational | `tests/scripts/Install.Tests.ps1` | Context `stage ordering (happy path)` lines 130-148 | Happy-path stage-ordering test asserts every helper in the canonical order but filters out `New-Item` and `Remove-Item` when comparing call sequences. End-to-end validation against a live Docker Desktop host and a signed MSIX is deliberately out-of-scope per repo test policy. | No change required. Keep the unit-level stage-ordering assertion. For integration confidence, add a future-scope regression test harness only if the feature ever takes live install responsibility in CI. | Repo policy prohibits external services and real Appx side effects in unit tests, which is the reason the integration test is absent. Same pattern as precedent feature #34. | `tests/scripts/Install.Tests.ps1:130-148`; `.claude/rules/general-unit-test.md` external-dependencies rule |

## Additional Review Observations (non-finding)

These items were considered during review and intentionally judged acceptable; they are recorded here for auditability but do not require changes.

1. **`docker` invocation via `& docker`**: Helpers invoke `docker` and `docker compose` as an external CLI, which is correct for the Windows-only Docker Desktop scope. The tests use a `function global:docker` shim rather than a PowerShell `Mock` because `docker` is an external process, not a PowerShell cmdlet. Pattern is correct and consistent with the Publish helpers precedent.

2. **Path separator handling**: `Join-Path` is used consistently across the codebase. The one spot that interacts with forward-slash paths is `$InstallRecordPath = Join-Path $env:LOCALAPPDATA 'OpenClaw/install-record.json'`. `Join-Path` on Windows accepts forward slashes in the second argument and emits backslashes, so this is benign. On the destination-comparison side, `Test-Path -LiteralPath` accepts either separator. No action required.

3. **JSON record shape**: The install-record object is built with `pscustomobject` property names that match the documented schema in `spec.md` exactly (`installedAt`, `version`, `sourcePath`, `destinationPath`, `packageFullName`, `composeProjectName`, `composeFilePath`, `skipDocker`, `allowUnsigned`). `ConvertTo-Json -Depth 5` is sufficient for the depth of the record.

4. **Test-scope `$global:` usage**: Tests use `$global:InstallTestCalls`, `$global:OriginalLOCALAPPDATA`, and `function global:Test-IsElevatedAdmin` to bridge the mock scope and the orchestrator script scope. Two `PSAvoidGlobalVars` suppressions document this with justification. Production code contains no `$global:` writes.

5. **Uninstall failure collection**: `scripts/Uninstall.ps1` correctly catches each step's exception, appends a `{ Step; Error }` record, and re-throws a single terminating error enumerating all failed steps. This implements the spec's "all steps run regardless of individual failures" contract.

6. **`-Force` prior-install uninstall sequence**: The `Install.ps1` `-Force` path tolerates per-step failures by catching and logging via `Write-Information`. This is appropriate: a prior install may be in a partial state where one step is already unwinding.

7. **Admin precheck via wrapper function**: `Test-IsElevatedAdmin` is defined at script scope only when not already present, which lets tests override it by defining `function global:Test-IsElevatedAdmin { $false }`. This is a clean test seam; the production path always executes the real Windows API call.

8. **Parameter validation**: `-Version` uses `[ValidatePattern('^(\d+\.\d+\.\d+\.\d+)?$')]` which accepts either an empty string or a 4-part version. The pattern correctly rejects 3-part versions as verified by a dedicated test case.

9. **`Find-NewestPublishVersion` output shape**: Returns `[pscustomobject]@{ Version = <System.Version>; Path = <string> }`. The orchestrator consumes `.Path` and derives the version segment from the leaf directory name. Consistent and type-safe.

10. **Write-InstallRecord parent-directory creation**: Uses `Split-Path -Parent` then `New-Item -ItemType Directory -Force` under `ShouldProcess`, which matches the documented behavior and survives the `-WhatIf` test case.

## Test Quality Notes

- Each `Describe`/`Context` is named for a behavior and not a file or function (e.g., `'manifest integrity failure'`, `'-SkipDocker path'`). Failure messages point straight at the failing behavior.
- `BeforeEach` rebuilds mocks per test, preserving independence.
- Stage-ordering tests use `[System.Collections.ArrayList]` call logs and index comparisons to assert ordering, which is robust to unrelated intervening calls (e.g., `New-Item`, `Remove-Item`).
- `-WhatIf` is asserted on both orchestrators and on every state-changing helper, which validates the `SupportsShouldProcess` contract.

## Documentation Review

- `README.md`: the new bullet under "What It Does" and the `scripts/` row expansion are concise and professional. No emojis, no promotional phrasing.
- `docs/mailbridge-runbook.md`: Install Path D is added with prerequisites, invocations, uninstall command, outputs, and operator notes (HostAdapter token + secrets still out-of-band). Troubleshooting rows cover the three new failure modes (no prior install, docker not running, manifest integrity failure). Path A content is preserved verbatim per the plan's preservation evidence (`evidence/other/runbook-path-preservation.2026-04-18T00-00.md`).

## Blockers

None.

## Summary

| Severity | Count |
|---|---|
| Blocker | 0 |
| Major | 0 |
| Minor | 1 (runbook Path A cross-reference polish) |
| Informational | 3 |

Recommended action: merge. The single Minor finding is a documentation polish and is explicitly not required by the plan's Q3 decision, which scoped cross-references to Paths B and C only. It is noted for future consistency.
