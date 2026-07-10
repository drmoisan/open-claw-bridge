# Code Review — Issue #135 (env-array-wrap-corruption) — Re-Audit (Cycle 2)

- Feature folder: `docs/features/active/2026-07-07-env-array-wrap-corruption-135`
- Base branch (resolved): `main` @ merge-base `5c7e4e154b8f6791bfff868e41bc02b3c28f8c10`
- Feature branch / head: `bug/env-array-wrap-corruption-135` @ `ad542d7f483b7fffb6b61d832ce7df1e86d2cb7d`
- Review timestamp: 2026-07-09T20-15

## Executive Summary

This re-audit reviews the full branch diff against `main`, which now contains two work cycles. Cycle 1 (previously reviewed and PASS'd at commit `34dfc7e`) fixed a redundant `@(...)` wrapper at two call sites that corrupted the repository `.env` file. Cycle 2 (new commit `ad542d7`) fixes a second, independently confirmed defect: `Write-EnvFileContent`'s `-Content` parameter carried only `[AllowEmptyCollection()]`, not `[AllowEmptyString()]`, so a `.env` fixture containing a blank line threw a parameter-binding error (`Cannot bind argument to parameter 'Content' because it is an empty string.`) instead of persisting the blank line verbatim.

The cycle-2 fix is minimal and precise: a single attribute addition, matching the pattern already used by `Get-EnvFileMap` and `Set-EnvFileValue` in the same module. It is paired with two regression tests that directly encode the previously-erroring scenario — one at the seam level (`Write-EnvFileContent` accepting an empty-string element) and one end-to-end (the full `Publish.ps1` stage-0c path with a blank-line fixture). This reviewer independently reproduced the pre-fix throw against the pre-cycle-2 module and confirmed the post-fix module no longer throws.

Code quality is good; no Blocking or Major findings. Two Informational findings are recorded below.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Informational | `tests/scripts/Publish.Tests.ps1` | Repo-wide test-discovery ordering (pre-existing, carried forward from cycle 1, not introduced by this branch) | `Publish.Tests.ps1`'s `Mock Invoke-VersionStamp` implicitly relies on `Invoke-VersionStamp` (defined in `scripts/Publish.Msix.psm1`) already being resolvable in the Pester session, which only holds when `Publish.Msix.Tests.ps1` is discovered first. A narrow scope omitting that file reproduces unrelated failures. Independently reproduced again in this cycle-2 re-audit. | No action required for this branch. Continue to include `Publish.Msix.Tests.ps1` (or run repo-wide) when validating `Publish.Tests.ps1` in isolation. | Latent test-suite ordering fragility, unrelated to either cycle's fix; the repo-wide Phase 2 QC loop (natural file discovery order) passed 369/369 both per the executor and this reviewer's independent re-run. | `FEATURE/evidence/regression-testing/targeted-verification.2026-07-09T19-13.md` documents the coupling and its workaround (adding `Publish.Msix.Tests.ps1` to the targeted scope); this reviewer's independent 4-file re-run (`Publish.Msix.Tests.ps1` first) passed 80/80. |
| Informational | `README.md`, `.claude/agent-memory/orchestrator/*` | Committed via `b7bb0cd` (between the cycle-1 review commit `417be1b` and the cycle-2 fix commit `ad542d7`) | An 84-line "manual uninstall troubleshooting" section was added to `README.md`, unrelated to issue #135, alongside 3 agent-memory files. This content existed only as an *uncommitted working-tree edit* at the time of the cycle-1 review (where it was recorded as an out-of-scope observation, not part of the branch diff) and has since been committed onto this branch, expanding the diff beyond the issue's stated AC scope. | No action required to merge this PR — the content is documentation-only, does not touch any acceptance criterion, and introduces no policy violation. For future minor-audit bug-fix branches, consider keeping unrelated documentation additions on a separate branch/PR to preserve a minimal, auditable diff. | Scope discipline: `.claude/rules/general-code-change.md` favors simplicity and avoiding incidental changes; while this addition is not itself harmful, it broadens the branch beyond the issue's declared scope (AC-3 requires only that the existing `.env`-related README example remain uncorrupted, which it does). | `git diff 417be1b..b7bb0cd -- README.md` (86 lines added); `git log --oneline 5c7e4e15..ad542d7 -- README.md` (single commit `b7bb0cd`); the `.env`-example correction from cycle 1 (`@()` removed) remains present and intact within this addition, satisfying AC-3. |

No Blocking or Major findings were identified.

## Positive Observations

- **Root-cause fix targets the actual defect, not a symptom.** The issue's root-cause notes for cycle 2 confirm `Write-EnvFileContent`'s `-Content` parameter rejected an empty-string array element; the fix adds exactly the missing `[AllowEmptyString()]` attribute, matching the identical pattern already present on `Get-EnvFileMap` and `Set-EnvFileValue` in the same file — no divergent or ad hoc validation approach introduced.
- **Change is minimal and auditable.** `git diff scripts/Publish.Env.psm1` (cycle 2 only) shows exactly one added line; `Get-EnvFileMap`, `Set-EnvFileValue`, `Read-EnvFileContent`, and `Step-PackageVersion` are byte-identical to their pre-fix state, matching the plan's explicit out-of-scope constraint.
- **Regression tests are behaviorally meaningful and target both the seam and the end-to-end path.** AC-8's test (`Publish.Env.Tests.ps1`) exercises `Write-EnvFileContent` directly with an empty-string element; AC-9's test (`Publish.Tests.ps1`) exercises the full stage-0c call chain (`Read-EnvFileContent` -> `Set-EnvFileValue` -> `Write-EnvFileContent`) with a blank-line fixture, asserting both no throw and verbatim blank-line preservation. Both tests fail against the pre-fix module and pass against the post-fix module — independently reproduced by this reviewer.
- **Fail-before/pass-after discipline is genuinely met, not merely asserted.** This reviewer independently imported the pre-cycle-2 module (`git show 34dfc7e:scripts/Publish.Env.psm1`) and confirmed the isolated repro throws; the current module does not.
- **Change budget compliance:** cycle 2 touches exactly 1 production file and 2 test files, within the direct-mode budget defined in `.claude/rules/powershell.md`. Combined with cycle 1, the full branch remains within the per-batch cap (3 production + 3 test files).
- **Coverage reasoning is correctly explained, not just reported.** The evidence correctly identifies that a declarative parameter-validation attribute is not itself an executable command/line under Pester's command-coverage instrumentation, so the numerically-identical pre/post coverage figures are expected, not a red flag. This reviewer independently confirmed the absence of `<line>` entries for the attribute's line range in the coverage XML.

## Verification Performed by This Reviewer

- Read the full diff for the branch (`git diff 5c7e4e154b8f6791bfff868e41bc02b3c28f8c10..ad542d7`), covering both cycle 1 and cycle 2 production/test changes.
- Confirmed `Get-EnvFileMap`, `Set-EnvFileValue`, `Read-EnvFileContent`, and `Step-PackageVersion` in `scripts/Publish.Env.psm1` are unchanged; only `Write-EnvFileContent`'s parameter block gained one line.
- Re-ran `Invoke-Formatter -ScriptDefinition` (idempotency check, PSScriptAnalyzer 1.24.0 bundled formatter, default settings) against all 6 branch-changed PowerShell files: clean on all 6.
- Re-ran `Invoke-ScriptAnalyzer` (default rule set, PSScriptAnalyzer 1.24.0) against all 6 files: 0 findings.
- Re-ran the affected Pester suite independently in two scopes: (a) targeted 4-file scope (`Publish.Msix.Tests.ps1`, `Publish.Env.Tests.ps1`, `Publish.Tests.ps1`, `New-MsixDevCert.Tests.ps1`) — 80/80 passed; (b) full repo-wide scope (32 test files) — 369/369 passed, numerically identical to the executor's committed repo-wide evidence.
- Independently reproduced the fail-before condition: imported `git show 34dfc7e:scripts/Publish.Env.psm1` and confirmed `Write-EnvFileContent -Path 'C:\fake\.env' -Content @('A=1','','B=2') -WhatIf` throws `Cannot bind argument to parameter 'Content' because it is an empty string.`; the same call against the current module (post-fix) throws no error.
- Extracted and manually verified the JaCoCo XML data in `artifacts/pester/powershell-coverage.xml`: repo-wide `<counter type="INSTRUCTION" missed="203" covered="1812" />` (1812/2015 = 89.93%, matching the executor's reported figure exactly) and the `Publish.Env.psm1` class-level `<counter type="INSTRUCTION" missed="2" covered="59" />` / `<counter type="LINE" missed="0" covered="51" />` (matching the executor's per-file figures exactly).
- Confirmed no real `.env` file or other repository state is touched by the independent full repo-wide Pester re-run (`git status --porcelain` reported no changes afterward).
- Confirmed the branch does not modify any path under `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` (the `modified-workflow-needs-green-run` rule does not fire) and does not touch `artifacts/orchestration/orchestrator-state.json`.
- Confirmed no evidence artifact in the branch diff is located under a forbidden `artifacts/` sub-path (`git diff --name-only` filtered against the forbidden-path patterns returned no matches).

## Recommendation

No changes requested. Both cycles' fixes are minimal, correct, and well-tested, with independently reproduced fail-before/pass-after evidence and independently cross-checked coverage figures. **Ready for merge from a code-quality standpoint**, pending the overall PR-readiness recommendation in the feature audit. The one Informational finding regarding the unrelated `README.md`/agent-memory commit (`b7bb0cd`) does not block merge.
