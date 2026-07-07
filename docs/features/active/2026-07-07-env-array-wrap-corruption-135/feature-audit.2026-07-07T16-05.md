# Feature Audit — Issue #135 (env-array-wrap-corruption)

- Feature folder: `docs/features/active/2026-07-07-env-array-wrap-corruption-135`
- Audit timestamp: 2026-07-07T16-05

## Scope and Baseline

- Resolved base branch: `main`
- Merge-base SHA: `5c7e4e154b8f6791bfff868e41bc02b3c28f8c10`
- Feature branch: `bug/env-array-wrap-corruption-135`
- Head SHA: `34dfc7ef3dd48c56bfd78f6394311ae55f1ace75`
- Work mode: `minor-audit` (persisted marker `- Work Mode: minor-audit` in `issue.md`)
- AC source (minor-audit rule): `issue.md` `## Acceptance Criteria` section only. `spec.md` and `user-story.md` are intentionally absent for this work mode and are not treated as AC sources.
- Full branch diff (`git diff --stat 5c7e4e154b8f6791bfff868e41bc02b3c28f8c10..34dfc7e`): 17 files changed, 561 insertions(+), 6 deletions(-). Production/test code: `scripts/Publish.ps1` (+1/-1), `scripts/New-MsixDevCert.ps1` (+1/-1), `tests/scripts/Publish.Tests.ps1` (+36/-3), `tests/scripts/New-MsixDevCert.Tests.ps1` (+64/-1). Remaining files are feature-tracking docs and evidence artifacts under `docs/features/active/2026-07-07-env-array-wrap-corruption-135/` and `docs/features/potential/promoted/`.
- No language other than PowerShell has changed files on this branch (`git diff --name-only <merge-base>..HEAD` confirmed).

## Acceptance Criteria Inventory

Source: `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md`, `## Acceptance Criteria` section.

1. AC-1: `scripts/Publish.ps1` no longer wraps the `Read-EnvFileContent` call in `@()`; the assignment reads `$envContent = Read-EnvFileContent -Path $EnvFilePath`.
2. AC-2: `scripts/New-MsixDevCert.ps1` no longer wraps the `Read-EnvFileContent` call in `@()`; the assignment reads `$content = Read-EnvFileContent -Path $EnvPath`.
3. AC-3: `scripts/Publish.Env.psm1` is unchanged, and the `README.md` example is left as-is (not reverted).
4. AC-4: The `Read-EnvFileContent` mocks in `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` return via the production-parity unary-comma idiom (`return , ([string[]]@(...))`).
5. AC-5: Each of the two test files contains a multi-line `.env` regression test (at least one comment line plus at least two key lines) asserting line preservation, single in-place key update, no space-joined collapse, and no duplicate key; the test fails if the redundant `@()` is reintroduced and passes with the fix applied.
6. AC-6: The full PowerShell toolchain passes with no regression: format clean, PSScriptAnalyzer 0 errors, all Pester tests pass, and coverage meets repository policy (line coverage >= 85%, no coverage regression on changed files).

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|---|---|---|
| AC-1 | **PASS** | `git diff 5c7e4e15..34dfc7e -- scripts/Publish.ps1` shows exactly one changed line: `$envContent = @(Read-EnvFileContent -Path $EnvFilePath)` -> `$envContent = Read-EnvFileContent -Path $EnvFilePath`. No other line in the file differs. |
| AC-2 | **PASS** | `git diff 5c7e4e15..34dfc7e -- scripts/New-MsixDevCert.ps1` shows exactly one changed line: `$content = @(Read-EnvFileContent -Path $EnvPath)` -> `$content = Read-EnvFileContent -Path $EnvPath`. No other line in the file differs. |
| AC-3 | **PASS** | `git diff 5c7e4e15..34dfc7e -- scripts/Publish.Env.psm1 README.md` returns no output; both files are byte-identical to the merge-base within this branch's commits. (Note: an unrelated, uncommitted working-tree edit to `README.md` exists at review time but is not part of this branch's commits — see policy-audit Gaps and Exceptions item 6 and code-review Informational finding 2.) |
| AC-4 | **PASS** | Both test files' `Read-EnvFileContent` mocks were read directly: `tests/scripts/Publish.Tests.ps1` line ~57 (`return , ([string[]]@($global:PublishTestEnvContent))`) and `tests/scripts/New-MsixDevCert.Tests.ps1` (two occurrences: the existing `Save-CertThumbprintToEnv (AC-5)` context mock and the new regression context mock), both using `return , ([string[]]@(...))`. All pre-existing `It` blocks in both files pass post-change (confirmed in the reviewer's independent 54/54 re-run). |
| AC-5 | **PASS** | `tests/scripts/Publish.Tests.ps1` contains `It 'preserves a multi-line .env verbatim and updates only OPENCLAW_PACKAGE_VERSION in place (regression: redundant @() wrap)'` using a 3-line fixture (1 comment + 2 keys) and asserting all four required conditions. `tests/scripts/New-MsixDevCert.Tests.ps1` contains the equivalent `It` block in a new `Context 'Save-CertThumbprintToEnv multi-line regression (AC-5)'`, using the real (unmocked) `Set-EnvFileValue`. Both tests independently re-run and pass (this audit, Pester 5.6.1, 54/54). Both tests are structurally capable of catching regression: reverting the `@()` removal would nest the mocked return, causing the assertions on line count / no-duplicate-key / verbatim-preservation to fail. |
| AC-6 | **PASS** | Format: `FEATURE/evidence/qa-gates/final-poshqc-format.2026-07-07T15-45.md` (EXIT_CODE 0, 0 files changed) — independently reconfirmed via `Invoke-Formatter` idempotency check (clean on all 4 edited files). Analyze: `FEATURE/evidence/qa-gates/final-poshqc-analyze.2026-07-07T15-46.md` (EXIT_CODE 0, `ok=true`) — independently reconfirmed via `Invoke-ScriptAnalyzer` default rules (0 findings on all 4 edited files). Test: `FEATURE/evidence/qa-gates/final-poshqc-test.2026-07-07T15-49.md` (367/367 passing repo-wide) — independently reconfirmed via targeted Pester re-run (54/54 passing on the 3 in-scope test files). Coverage: `FEATURE/evidence/qa-gates/coverage-comparison.2026-07-07T15-52.md` reports repo-wide line coverage 89.93% (>= 85% threshold) and the command-coverage branch-proxy 89.93% (>= 75% threshold), with both changed lines confirmed covered (`mi="0" ci="1"`) — independently re-verified by this reviewer by parsing `artifacts/pester/powershell-coverage.xml` directly. |

## Summary

All six acceptance criteria are satisfied with independently reproduced evidence (not solely the executor's self-reported figures). The fix directly addresses the confirmed root cause, the out-of-scope constraints (no edits to `scripts/Publish.Env.psm1` or `README.md`) are honored, and the toolchain (format/lint/test/coverage) is clean with no regression. Two Informational (non-blocking) observations are recorded in the code review and policy audit: a pre-existing, unrelated test-discovery ordering coupling, and an unrelated uncommitted working-tree `README.md` edit that is outside this branch's commit scope.

**Go/No-Go recommendation: GO.** No remediation is required for this feature. `remediation-inputs.<timestamp>.md` is not produced for this review.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md`
- Total AC items: 6
- Checked off (delivered): 6
- Remaining (unchecked): 0
- Items remaining: none

## Acceptance Criteria Check-off

All six AC items (AC-1 through AC-6) were already checked `- [x]` in `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md` at the start of this review (checked off by the executor per `FEATURE/evidence/qa-gates/ac6-acceptance-summary.2026-07-07T15-54.md`). This reviewer independently re-verified each criterion per the Acceptance Criteria Evaluation table above and confirms all six PASS verdicts hold; no checkbox state change was required in `issue.md`.
