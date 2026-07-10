# Code Review — Issue #135 (env-array-wrap-corruption)

- Feature folder: `docs/features/active/2026-07-07-env-array-wrap-corruption-135`
- Base branch (resolved): `main` @ merge-base `5c7e4e154b8f6791bfff868e41bc02b3c28f8c10`
- Feature branch / head: `bug/env-array-wrap-corruption-135` @ `34dfc7ef3dd48c56bfd78f6394311ae55f1ace75`
- Review timestamp: 2026-07-07T16-05

## Executive Summary

This branch fixes a real production defect: a redundant `@(...)` wrapper at two call sites (`scripts/Publish.ps1:118`, `scripts/New-MsixDevCert.ps1:72`) nested the already-array-safe `string[]` return of `Read-EnvFileContent` inside a one-element array. When that nested array was bound to a `[string[]]`-typed parameter, PowerShell's `$OFS`-based stringification collapsed every line of the repository `.env` file into a single space-joined line and appended the target key as a duplicate line — a real data-corruption bug in a state-persisting script. The fix itself is a minimal, surgical one-line change per call site. It is paired with a mock-shape parity correction (the existing test mocks did not match the production return shape, which is exactly why the bug was undetected) and two new regression tests that directly encode the corruption scenario (multi-line fixture with a comment plus two keys) and would fail if the redundant wrap were reintroduced.

The change is well-scoped: `scripts/Publish.Env.psm1` (the correct, unmodified I/O seam) and `README.md` are confirmed untouched, matching the issue's explicit constraint. No new dependencies, no API changes, no dead code. Code quality is good; no Blocking or Major findings.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Informational | `tests/scripts/Publish.Tests.ps1` | Repo-wide test-discovery ordering (pre-existing, not introduced by this branch) | `Publish.Tests.ps1`'s `Mock Invoke-VersionStamp` implicitly relies on `Invoke-VersionStamp` (defined in `scripts/Publish.Msix.psm1`) already being resolvable in the Pester session, which only holds when `Publish.Msix.Tests.ps1` is discovered first. A narrow 2-file `Run.Path` (just the two files this branch touches) reproduces 25 unrelated failures; a 3-file scope (adding `Publish.Msix.Tests.ps1`) or a full repo-wide run does not. Independently reproduced by this reviewer. | No action required for this branch. If a future change further narrows targeted test scopes, be aware this coupling exists and include `Publish.Msix.Tests.ps1` (or run repo-wide) when validating `Publish.Tests.ps1` in isolation. | This is a latent test-suite ordering fragility unrelated to the `@()`-wrap fix; it already existed before this branch and does not affect the repo-wide Phase 2 QC loop, which passed 367/367. | `FEATURE/evidence/regression-testing/targeted-verification.2026-07-07T15-42.md` documents the same coupling and its accepted workaround; reviewer independently reproduced both the failure (wrong file order) and the pass (correct file order). |
| Informational | (working tree, not part of branch diff) | `README.md`, `.claude/agent-memory/orchestrator/MEMORY.md`, new untracked `.claude/agent-memory/orchestrator/feedback_no-direct-typed-engineer-delegation.md` | Uncommitted local modifications exist in the working tree at review time. These are unrelated to issue #135 and are not part of the committed branch diff (`5c7e4e15..34dfc7e`). | No action required for this review; flagged so a future commit on this branch does not accidentally bundle unrelated changes. | Out of scope per the review's scope invariant (audit is against the committed diff, not working-tree state), but worth surfacing so it is not silently carried into a future commit. | `git status --porcelain` at review time; `git diff <merge-base>..HEAD -- README.md` returns no output (confirms these files are not part of the reviewed commit). |

No Blocking or Major findings were identified.

## Positive Observations

- **Root-cause fix targets the actual defect**, not a symptom: the redundant `@(...)` wrap is removed at both call sites, and the correct, already-safe unary-comma-return seam in `scripts/Publish.Env.psm1` is explicitly left untouched (confirmed via `git diff`), matching the issue's root-cause analysis exactly.
- **Test-mock parity fix addresses the actual reason the bug was undetected.** The issue's root-cause notes explain that the pre-existing mocks returned a flat `[string[]]` without the unary-comma idiom, which does not match `Read-EnvFileContent`'s real return shape and therefore could not exercise the collapse. Both mocks are corrected to `return , ([string[]]@(...))`, restoring production-return-shape parity.
- **Regression tests are behaviorally meaningful, not just line-count checks.** Each new `It` block asserts (a) no space-joined collapse (line count preserved), (b) every non-target line preserved verbatim, and (c) the target key updated exactly once with no duplicate — directly encoding the four symptoms described in the issue's "Actual Behavior" section.
- **`New-MsixDevCert.Tests.ps1`'s new test deliberately exercises the real `Set-EnvFileValue`** (left unmocked) rather than mocking it, which is a stronger regression guard than mocking the update step, since it also protects against the mocked-update-masks-drift class of test blind spot this issue exists because of.
- **Change budget compliance:** exactly 2 production files + 2 test files, within the direct-mode budget defined in `.claude/rules/powershell.md`.
- **No incidental refactor or scope creep** — the diff for the production files is a true one-line change per file with all comments and surrounding code left untouched.

## Verification Performed by This Reviewer

- Read the full diff for all 4 changed PowerShell files (`git diff <merge-base>..HEAD -- scripts/Publish.ps1 scripts/New-MsixDevCert.ps1 tests/scripts/Publish.Tests.ps1 tests/scripts/New-MsixDevCert.Tests.ps1`).
- Confirmed `scripts/Publish.Env.psm1` and `README.md` are byte-identical to the merge-base (`git diff <merge-base>..HEAD -- scripts/Publish.Env.psm1 README.md` returns no output).
- Re-ran `Invoke-Formatter -ScriptDefinition` (idempotency check, PSScriptAnalyzer 1.24.0 bundled formatter, default settings) against all 4 edited files: clean on all 4 (no formatting diff).
- Re-ran `Invoke-ScriptAnalyzer` (default rule set, PSScriptAnalyzer 1.24.0) against all 4 edited files: 0 findings on all 4.
- Re-ran the affected Pester suite independently (Pester 5.6.1, `Run.Path` = `Publish.Msix.Tests.ps1`, `Publish.Tests.ps1`, `New-MsixDevCert.Tests.ps1`): 54/54 passed, matching the executor's targeted-verification evidence exactly.
- Extracted and manually verified the JaCoCo XML line-hit data for the two changed lines (`Publish.ps1:118`, `New-MsixDevCert.ps1:72`) in `artifacts/pester/powershell-coverage.xml`: both `mi="0" ci="1"` (covered, no regression).
- Confirmed no real `.env` file on disk is touched by the test suite (both `Read-EnvFileContent` and `Write-EnvFileContent` are mocked in every context that exercises the fixed call sites; `git status --porcelain` showed no `.env` change after the independent Pester re-run).

## Recommendation

No changes requested. The fix is minimal, correct, and well-tested. **Ready for merge from a code-quality standpoint**, pending the overall PR-readiness recommendation in the feature audit.
