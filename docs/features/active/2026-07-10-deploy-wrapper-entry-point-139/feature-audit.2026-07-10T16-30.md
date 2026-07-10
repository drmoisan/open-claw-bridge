# Feature (Acceptance Criteria) Audit — Issue #139 (deploy-wrapper-entry-point)

- Reviewed: 2026-07-10T16-30
- Work mode: `full-feature`
- AC sources (authoritative, per the persisted `- Work Mode: full-feature` marker in `issue.md`): `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/spec.md` (`## Seeded Test Conditions (from potential)`, mirroring `spec.md`'s earlier prose AC narrative) and `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/user-story.md` (`## Acceptance Criteria`)

## Scope and Baseline

- Base branch: `main` (merge-base `c72782357d1cb10848d53d92f0d5cde091cc8c92`)
- Head: `feature/deploy-wrapper-entry-point-139` @ `f6c1ca9`
- Diff scope: `git diff --stat c72782357d1cb10848d53d92f0d5cde091cc8c92..HEAD` — 24 files changed (1,356 insertions / 3 deletions), of which 4 are PowerShell production/test files (`scripts/Deploy.ps1` new, `scripts/Publish.ps1` modified, `tests/scripts/Deploy.Tests.ps1` new, `tests/scripts/Publish.Tests.ps1` modified) and the remainder are Markdown feature-folder documentation/evidence artifacts.

## Acceptance Criteria Inventory

Both AC source files carry an identical set of 6 checkbox items (verified via direct read of both files; criterion text is character-for-character identical between `spec.md`'s `## Seeded Test Conditions (from potential)` section and `user-story.md`'s `## Acceptance Criteria` section):

| # | Criterion |
|---|---|
| AC-1 | `Publish.ps1` emits exactly one pipeline object (the bundle root) when its output is captured; helper return behavior is unchanged. |
| AC-2 | New `scripts/Deploy.ps1` runs `Publish.ps1`, captures the bundle root, then invokes `<bundleRoot>\Install.ps1` (the staged copy) without changing the caller's working directory. |
| AC-3 | `Deploy.ps1` forwards publish parameters (`-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign`) and install parameters (`-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`); `-SkipSign` maps to `-AllowUnsigned` on `Install.ps1`. |
| AC-4 | `Deploy.ps1` uses `CmdletBinding(SupportsShouldProcess = $true)` and propagates `-WhatIf` to both child invocations. |
| AC-5 | `Deploy.ps1` fails fast: if publish throws or returns no bundle root, it does not attempt the install; on success it returns the bundle root. |
| AC-6 | Pester tests cover both scripts; child-script invocations are mocked via a wrapper-function seam; no temp files; coverage floors met. |

All 6 items were already marked `- [x]` in both `spec.md` and `user-story.md` at the start of this review.

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 | **PASS** | `scripts/Publish.ps1` lines 221, 227, 245: `$null = Invoke-VersionStamp ...`, `$null = Invoke-MakeAppx ...`, `$null = Write-PublishManifest ...` (confirmed via `git diff`). New regression test `tests/scripts/Publish.Tests.ps1` `Context 'output contract'` asserts `@($result).Count | Should -Be 1`; independently re-run in this audit (`Invoke-Pester`, 116/116 passed including this test). Fail-before evidence (`evidence/regression-testing/ps-expect-fail.2026-07-10T15-02.md`, "Expected 1, but got 2") and pass-after evidence (`evidence/regression-testing/ps-post-fix-pass.2026-07-10T15-02.md`) independently reviewed and consistent. Helper return behavior unchanged: `git diff scripts/Publish.Msix.psm1 scripts/Publish.Helpers.psm1` returns no output. |
| AC-2 | **PASS** | `scripts/Deploy.ps1` line 143 (`$bundleRoot = Invoke-PublishScript ...`), line 163 (`Join-Path $bundleRoot 'Install.ps1'`), line 165 (`Invoke-InstallScript -InstallScriptPath $installScriptPath ...`) — confirmed via direct source read: the staged `<bundleRoot>\Install.ps1` is invoked, never `scripts/Install.ps1` directly. No `Set-Location`/`Push-Location` call exists anywhere in the file (confirmed via source read and `grep`). Test `'does not change the caller working directory (AC-2)'` independently re-run and passing in this audit's repo-wide Pester run. |
| AC-3 | **PASS** | `scripts/Deploy.ps1` lines 130-134 (`$publishParams` hashtable, conditionally populated from `$PSBoundParameters`) and lines 156-161 (`$installParams` hashtable, including the `-SkipSign` -> `AllowUnsigned = $true` mapping at line 161). Tests `'forwards -Version, -Configuration, -CertThumbprint to Invoke-PublishScript (AC-3)'`, `'forwards -SkipDocker, -DockerEnvFilePath, -AnthropicEnvFilePath, -Force to Invoke-InstallScript (AC-3)'`, `'maps -SkipSign to AllowUnsigned = $true in -InstallParams (AC-3)'`, and the negative case `'does not set a truthy AllowUnsigned key when -SkipSign is not supplied (AC-3)'` all independently re-run and passing. |
| AC-4 | **PASS** | `scripts/Deploy.ps1` line 63: `[CmdletBinding(SupportsShouldProcess = $true)]` on the top-level param block. `-WhatIf` reaches `Invoke-InstallScript` via explicit `-WhatIf:$WhatIfPreference` forwarding (line 165) and reaches `Invoke-PublishScript` via PowerShell's dynamic-scope inheritance of `$WhatIfPreference` (documented inline at lines 136-140; `Invoke-PublishScript` is a script-scoped nested function called within the same script session). Test `'does not throw and records no Invoke-InstallScript call under -WhatIf (AC-4)'` independently re-run and passing. |
| AC-5 | **PASS** | `scripts/Deploy.ps1` line 148-150: `if ([string]::IsNullOrWhiteSpace($bundleRoot)) { throw ... }` and line 168: `return $bundleRoot`. Tests `'propagates a publish throw and does not invoke Install.ps1 (AC-5)'`, `'throws and does not invoke Install.ps1 when publish returns no bundle root (AC-5)'`, and `'returns exactly the bundle root on success (AC-5)'` all independently re-run and passing; each explicitly asserts `Invoke-InstallScript` was never called in the two failure-path tests. |
| AC-6 | **PASS** | `tests/scripts/Deploy.Tests.ps1` (9 tests, global-scoped `Invoke-PublishScript`/`Invoke-InstallScript` overrides pre-registered before `&`-invocation — the wrapper-function seam) and the new `tests/scripts/Publish.Tests.ps1` regression test. No temp files: independently re-confirmed via `grep -nE "New-TemporaryFile\|\[System\.IO\.Path\]::GetTempPath\|\$env:TEMP"` against both test files — zero matches. Coverage floors: independently re-parsed from `artifacts/pester/powershell-coverage.xml` — repo-wide 89.94% (>= 85%, no regression vs. 89.93% baseline), `scripts/Deploy.ps1` 87.10% line coverage (>= 85%), `scripts/Publish.ps1` 97.47% line coverage (>= 85%, no regression on the 3 changed lines, confirmed `mi=0 ci=1` for lines 221/227/245 in the per-line JaCoCo data). |

## Root-Cause / Non-Goal Verification

- Root cause matches spec: the leak was isolated to exactly 3 call sites in `Publish.ps1`'s stages 4 and 6 (`Invoke-VersionStamp`, `Invoke-MakeAppx`, `Write-PublishManifest`); `git diff scripts/Publish.ps1` confirms no other line changed.
- Non-goal 1 (no change to `Install.ps1`): honored — `git diff scripts/Install.ps1` returns no output.
- Non-goal 2 (no change to `Publish.Helpers.psm1`/`Publish.Msix.psm1` return behavior, bundle layout, or manifest schema): honored — both files confirmed byte-identical via `git diff`.
- Non-goal 3 (no third production module introduced): honored — `Deploy.ps1` implements the child-invocation seam as script-scope wrapper functions inside itself, not a separate `Deploy.Helpers.psm1`, matching the spec's explicit constraint.
- Non-goal 4 (no staging/reading of operator secret files): honored — confirmed via source read that `-AnthropicEnvFilePath`/`-DockerEnvFilePath` are forwarded as path strings only, with no `Get-Content`/`Copy-Item` against them anywhere in `Deploy.ps1`.

## Summary

All 6 acceptance criteria are independently verified PASS against direct source review, independently re-run Pester tests (116/116 targeted, 380/380 repo-wide), and independently re-parsed coverage data. All 4 stated non-goals are honored. No remediation is required.

## Acceptance Criteria Check-off

All 6 `- [ ]` checkboxes in both `spec.md`'s `## Seeded Test Conditions (from potential)` section and `user-story.md`'s `## Acceptance Criteria` section were already changed to `- [x]` by the branch itself prior to this review (cross-referenced in the branch's own `evidence/qa-gates/ac-summary.2026-07-10T15-02.md`), with criterion text unchanged in both files. Independent re-verification against each item's supporting evidence (table above) confirms every check-off is warranted; no reviewer-side check-off changes were required in either file.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/spec.md`, `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/user-story.md`
- Total AC items: 6 (identical set, tracked independently in both files per `acceptance-criteria-tracking`)
- Checked off (delivered): 6 (both files)
- Remaining (unchecked): 0
- Items remaining: none

## Overall Feature Audit Verdict

**PASS.** All 6 acceptance criteria are verified PASS in both authoritative AC source files against independently reviewed evidence, all 4 non-goals are honored, and the root cause matches the spec's analysis. No remediation is required.
