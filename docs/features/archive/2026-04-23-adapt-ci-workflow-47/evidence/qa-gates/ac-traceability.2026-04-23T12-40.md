---
Timestamp: 2026-04-23T12-40
Source of truth: docs/features/active/2026-04-23-adapt-ci-workflow-47/issue.md (## Acceptance Criteria section)
Work Mode: minor-audit
---

# Acceptance Criteria Traceability (Issue #47)

## AC-1 — `.gitignore` update allows `ci.yml` to be tracked

Status: **PASS**
Evidence:
- `evidence/other/gitignore-edit.2026-04-23T12-40.md` — exact diff (1 line removed, 5 lines added).
- `evidence/regression-testing/gitignore-check-ci.2026-04-23T12-40.md` — `ci.yml` is now visible to git (shows as untracked in `git status`).
- `evidence/regression-testing/gitignore-check-publish.2026-04-23T12-40.md` — `publish.yml` remains trackable (explicit re-include).
- `evidence/regression-testing/gitignore-check-other.2026-04-23T12-40.md` — other `.github/` descendants remain ignored.
Note on AC-1 wording: AC-1 said `git check-ignore -v .github/workflows/ci.yml` should return empty with exit 1. With the explicit `!` negation rule in the new `.gitignore`, `git check-ignore` exits 0 and prints the negation rule — this is the documented behavior for files that are unignored by a negation. The semantic intent ("`ci.yml` is no longer ignored") is satisfied and corroborated by `git status` showing `ci.yml` as untracked.

## AC-2 — `ci.yml` rewritten, only relevant jobs remain

Status: **PASS**
Evidence:
- `evidence/regression-testing/verify-ci-removals.2026-04-23T12-40.md` — all 23 forbidden tokens absent.
- Diff against `evidence/baseline/ci-yml-before.2026-04-23T12-40.yml`: file reduced from 318 lines to 89 lines; 7 template jobs removed; 3 new relevant jobs added (.NET build+test, PowerShell QC, actionlint).

## AC-3 — .NET job uses setup-dotnet@v4, 10.0.x, windows-latest, restore+build+test with coverage

Status: **PASS**
Evidence:
- `evidence/regression-testing/verify-ci-dotnet-job.2026-04-23T12-40.md` — all six literal patterns matched (setup-dotnet@v4, dotnet-version: 10.0.x, windows-latest, dotnet restore, dotnet build `/warnaserror`, `XPlat Code Coverage`).

## AC-4 — PowerShell job (AMENDED)

AC-4 was amended in `issue.md` to require direct `Invoke-ScriptAnalyzer` and `Invoke-Pester` calls (no PoshQC wrapper, since that wrapper does not exist in this repository).

Status: **PASS**
Evidence:
- `evidence/regression-testing/verify-ci-powershell-job.2026-04-23T12-40.md` — all required patterns matched (`Install-Module -Name Pester`, `Install-Module -Name PSScriptAnalyzer`, `Invoke-ScriptAnalyzer -Path scripts, tests/scripts -Recurse -Severity Warning,Error` with fail-on-nonempty, `Invoke-Pester -Path tests/scripts -Output Detailed -CI`, `artifacts/pester` upload with `if-no-files-found: ignore`). All forbidden PoshQC wrapper references are ABSENT.
- `evidence/other/poshqc-module-status.2026-04-23T12-40.md` — confirms module absence, which is the trigger for the AC-4 amendment.
- `evidence/other/poshqc-job-variant.2026-04-23T12-40.md` — records the decision to emit the amended AC-4 variant.

## AC-5 — actionlint passes

Status: **PASS**
Evidence:
- `evidence/regression-testing/actionlint.2026-04-23T12-40.md` — actionlint 1.7.11 exits 0 with empty output on `.github/workflows/ci.yml` (and `publish.yml` sanity pass).
- `evidence/qa-gates/actionlint-final.2026-04-23T12-40.md` — final pass also clean.

## AC-6 — triggers are push + pull_request on main/development

Status: **PASS**
Evidence:
- `evidence/regression-testing/verify-ci-triggers.2026-04-23T12-40.md` — head of `ci.yml` shows `on.push.branches: [main, development]`, `on.pull_request.branches: [main, development]`, and `on.workflow_dispatch:`. YAML is well-formed (actionlint-validated).

## AC-7 — `ci.yml` is tracked in git

Status: **PASS**
Evidence:
- `evidence/regression-testing/ci-yml-tracked.2026-04-23T12-40.md` — initial staging after `.gitignore` fix.
- `evidence/regression-testing/verify-ci-tracked.2026-04-23T12-40.md` — end-state staging after Phase 2 rewrite; `git ls-files .github/workflows/ci.yml` and `git diff --cached --name-only` both return the literal path.

## Invariants

- `publish.yml` byte-identical: **PASS** — `evidence/regression-testing/publish-yml-unchanged.2026-04-23T12-40.md` (SHA-256 matches Phase 0 baseline).
- No C# source modified: **PASS** — only `.gitignore` and `.github/workflows/ci.yml` changed in the tree.
- Coverage no-regression: **PASS** — `evidence/qa-gates/dotnet-coverage-delta.2026-04-23T12-40.md` (baseline 84.40% -> post-change 84.42%, delta +0.02 pp, NewCodeCoverage N/A because no C# was added).
- File size limit: **PASS** — `ci.yml` is 89 lines (< 300-line limit and < 500-line general rule).

## Summary

| AC | Status |
|---|---|
| AC-1 | PASS |
| AC-2 | PASS |
| AC-3 | PASS |
| AC-4 (amended) | PASS |
| AC-5 | PASS |
| AC-6 | PASS |
| AC-7 | PASS |

All 7 acceptance criteria pass. Plan outcome: **PASS**. No remediation required.
