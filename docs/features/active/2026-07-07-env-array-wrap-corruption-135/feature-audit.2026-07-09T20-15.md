# Feature Audit — Issue #135 (env-array-wrap-corruption) — Re-Audit (Cycle 2)

- Feature folder: `docs/features/active/2026-07-07-env-array-wrap-corruption-135`
- Audit timestamp: 2026-07-09T20-15

## Scope and Baseline

- Resolved base branch: `main`
- Merge-base SHA: `5c7e4e154b8f6791bfff868e41bc02b3c28f8c10`
- Feature branch: `bug/env-array-wrap-corruption-135`
- Head SHA: `ad542d7f483b7fffb6b61d832ce7df1e86d2cb7d` (PR #136 already open; this branch already passed a prior review cycle — PASS/GO at commit `34dfc7e` / audit `417be1b` — and a new commit has since landed on top of that reviewed state)
- Work mode: `minor-audit` (persisted marker `- Work Mode: minor-audit` in `issue.md`)
- AC source (minor-audit rule): `issue.md` `## Acceptance Criteria` section only. `spec.md` and `user-story.md` are intentionally absent for this work mode and are not treated as AC sources.
- Full branch diff (`git diff --stat 5c7e4e154b8f6791bfff868e41bc02b3c28f8c10..ad542d7`): 37 files changed, 1286 insertions(+), 7 deletions(-). Production/test PowerShell code: `scripts/Publish.ps1` (+1/-1), `scripts/New-MsixDevCert.ps1` (+1/-1), `scripts/Publish.Env.psm1` (+1/-0), `tests/scripts/Publish.Tests.ps1` (+70/-3), `tests/scripts/New-MsixDevCert.Tests.ps1` (+64/-1), `tests/scripts/Publish.Env.Tests.ps1` (+5/-0). Remaining files are `README.md`, feature-tracking docs/plans, evidence artifacts under `docs/features/active/2026-07-07-env-array-wrap-corruption-135/` and `docs/features/potential/promoted/`, and 3 agent-memory files.
- This audit reviews the **full branch diff** against `main`, not merely the newest commit (`ad542d7`, 15 files). No language other than PowerShell has changed files on this branch (`git diff --name-only <merge-base>..HEAD` confirmed; no `.ts`/`.tsx`/`.py`/`.cs` files changed).

## Acceptance Criteria Inventory

Source: `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md`, `## Acceptance Criteria` section (AC-1 through AC-10).

1. AC-1: `scripts/Publish.ps1` no longer wraps the `Read-EnvFileContent` call in `@()`; the assignment reads `$envContent = Read-EnvFileContent -Path $EnvFilePath`.
2. AC-2: `scripts/New-MsixDevCert.ps1` no longer wraps the `Read-EnvFileContent` call in `@()`; the assignment reads `$content = Read-EnvFileContent -Path $EnvPath`.
3. AC-3: `scripts/Publish.Env.psm1` is unchanged (its unary-comma return idiom is the correct file-I/O seam), and the `README.md` example is left as-is (not reverted).
4. AC-4: The `Read-EnvFileContent` mocks in `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` return via the production-parity unary-comma idiom.
5. AC-5: Each of the two test files contains a multi-line `.env` regression test asserting line preservation, single in-place key update, no space-joined collapse, and no duplicate key.
6. AC-6: The full PowerShell toolchain passes with no regression: format clean, PSScriptAnalyzer 0 errors, all Pester tests pass, and coverage meets repository policy.
7. AC-7: `scripts/Publish.Env.psm1`'s `Write-EnvFileContent -Content` parameter carries `[AllowEmptyString()]` in addition to its existing `[AllowEmptyCollection()]`, matching the pattern already used by `Get-EnvFileMap` and `Set-EnvFileValue`. No other function or file is changed.
8. AC-8: `tests/scripts/Publish.Env.Tests.ps1` contains a test asserting `Write-EnvFileContent -WhatIf` accepts a `-Content` array containing an empty-string element without throwing a parameter-binding error.
9. AC-9: `tests/scripts/Publish.Tests.ps1` contains an end-to-end regression test covering the stage 0c path (`Read-EnvFileContent` -> `Set-EnvFileValue` -> `Write-EnvFileContent`) with a blank-line `.env` fixture, asserting no parameter-binding error occurs and the blank line is preserved verbatim in the persisted content.
10. AC-10: The full PowerShell toolchain passes with no regression: format clean, PSScriptAnalyzer 0 errors, all Pester tests pass with no drop from this cycle's repo-wide baseline pass count, and coverage meets repository policy with no regression on the changed lines.

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 | **PASS** | `git diff 5c7e4e15..ad542d7 -- scripts/Publish.ps1` shows the single changed line: `$envContent = Read-EnvFileContent -Path $EnvFilePath` (no `@()`). No other line in the file differs from the merge-base except this one (re-confirmed at current HEAD). |
| AC-2 | **PASS** | `git diff 5c7e4e15..ad542d7 -- scripts/New-MsixDevCert.ps1` shows the single changed line: `$content = Read-EnvFileContent -Path $EnvPath` (no `@()`). No other line in the file differs. |
| AC-3 | **PASS** | Direct read of `scripts/Publish.Env.psm1` confirms `Get-EnvFileMap`, `Set-EnvFileValue`, `Read-EnvFileContent`, and `Step-PackageVersion` are unchanged from the documented correct idiom (unary-comma return `return , ([string[]]@(...))` on `Read-EnvFileContent`); only `Write-EnvFileContent`'s parameter block gained the AC-7 attribute. The `README.md` `.env`-example correction (`@()` removed) from cycle 1 remains present and intact at current HEAD (`git diff 417be1b..b7bb0cd -- README.md` shows the example line preserved, with additional unrelated content appended — see code-review Informational finding). |
| AC-4 | **PASS** | Both test files' `Read-EnvFileContent` mocks use `return , ([string[]]@(...))`. Confirmed unchanged from the cycle-1 PASS verdict and unaffected by cycle 2. |
| AC-5 | **PASS** | Both test files retain their cycle-1 multi-line regression tests, independently re-run and passing (369/369 repo-wide, this audit). |
| AC-6 | **PASS** | Independently re-verified in this audit: format clean (0 diffs via `Invoke-Formatter` idempotency check), 0 PSScriptAnalyzer findings, 369/369 Pester tests passing, repo-wide coverage 89.93% (INSTRUCTION-based) / 90.46% (LINE-based), both exceeding the 85%/75% thresholds. |
| AC-7 | **PASS** | Direct read of `scripts/Publish.Env.psm1` confirms `Write-EnvFileContent`'s `-Content` parameter carries both `[AllowEmptyCollection()]` and `[AllowEmptyString()]`, in that order, matching `Get-EnvFileMap`/`Set-EnvFileValue`. `git diff 5c7e4e15..ad542d7 -- scripts/Publish.Env.psm1` shows exactly one added line; `Get-EnvFileMap`, `Set-EnvFileValue`, `Read-EnvFileContent`, `Step-PackageVersion` byte-identical to merge-base. No other file changed by the cycle-2 commit's production edit. |
| AC-8 | **PASS** | `tests/scripts/Publish.Env.Tests.ps1` contains `It 'accepts a -Content array containing an empty-string element without a parameter-binding error (regression: issue #135 AC-7/AC-8)'`, asserting `{ Write-EnvFileContent -Path 'C:\fake\.env' -Content @('A=1', '', 'B=2') -WhatIf } | Should -Not -Throw` with `Set-Content` mocked and asserted not called. Independently re-run and passing (this audit's 4-file targeted run, 80/80, and full repo-wide run, 369/369). Independently reproduced fail-before: the identical call against the pre-cycle-2 module (`git show 34dfc7e:scripts/Publish.Env.psm1`) throws `Cannot bind argument to parameter 'Content' because it is an empty string.` |
| AC-9 | **PASS** | `tests/scripts/Publish.Tests.ps1` contains `It 'preserves a blank line in the .env verbatim through the stage 0c path without a parameter-binding error (regression: issue #135 AC-9)'`, using a fixture (`'# leading comment', 'OPENCLAW_PACKAGE_VERSION=1.0.2.0', '', 'OTHER_KEY=unchanged'`) with a blank-line element, asserting the script invocation does not throw, the persisted line count equals 4 (blank line preserved, not dropped), the blank line is present verbatim (`$written -contains ''`), and the target key is updated exactly once. Independently re-run and passing. |
| AC-10 | **PASS** | Independently re-verified in this audit (not solely the executor's self-reported figures): format clean (`Invoke-Formatter` idempotency, 0 diffs on all 6 branch-changed PowerShell files), 0 `Invoke-ScriptAnalyzer` findings, 369/369 Pester tests passing repo-wide (up from the cycle-2 baseline of 367, +2 new regression tests, no drop), and coverage independently parsed from `artifacts/pester/powershell-coverage.xml`: repo-wide 89.93% INSTRUCTION-coverage / 90.46% LINE-coverage (both >= 85%), `Publish.Env.psm1` per-file 59/61 INSTRUCTION (96.72%) / 51/51 LINE (100%) with 0.00pp regression versus the executor's pre-change isolation figures. No production PowerShell file excluded from coverage measurement (confirmed 30/30 files measured, 0 `ExcludedPath` entries). |

## Summary

All ten acceptance criteria (AC-1 through AC-10) are satisfied with independently reproduced evidence, not solely the executor's self-reported figures. This re-audit covers the full branch diff against `main` (both work cycles), not merely the newest commit. Cycle 1's redundant-`@()` fix and cycle 2's `[AllowEmptyString()]` fix both directly address their respective confirmed root causes; the out-of-scope constraints from both cycles' plans (no edits to `Get-EnvFileMap`, `Set-EnvFileValue`, `Read-EnvFileContent`, `Step-PackageVersion`, or unrelated files) are honored; and the toolchain (format/lint/test/coverage) is clean with no regression, independently confirmed via a full repo-wide Pester re-run (369/369, matching the executor's evidence exactly) and a direct parse of the committed coverage XML.

One Informational (non-blocking) observation is recorded in the code review and policy audit: commit `b7bb0cd` bundled an unrelated 84-line `README.md` documentation addition and 3 agent-memory files onto this branch. This does not affect any acceptance criterion and introduces no policy violation.

**Go/No-Go recommendation: GO.** No remediation is required for this feature. `remediation-inputs.<timestamp>.md` is not produced for this review.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md`
- Total AC items: 10
- Checked off (delivered): 10
- Remaining (unchecked): 0
- Items remaining: none

## Acceptance Criteria Check-off

All ten AC items (AC-1 through AC-10) were already checked `- [x]` in `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md` at the start of this review (AC-1..AC-6 checked off during cycle 1; AC-7..AC-10 checked off by the executor per `FEATURE/evidence/qa-gates/ac10-acceptance-summary.2026-07-09T19-13.md`). This reviewer independently re-verified each criterion per the Acceptance Criteria Evaluation table above and confirms all ten PASS verdicts hold; no checkbox state change was required in `issue.md`.
