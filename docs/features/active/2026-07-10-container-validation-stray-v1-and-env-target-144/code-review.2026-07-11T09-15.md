# Code Review — Issue #144 (container-validation-stray-v1-and-env-target)

- Reviewed: 2026-07-11T09-15 (RE-AUDIT, remediation pass 1 / R4)
- Base: `main` @ `81debeb1d58dd7226e0eec1bc66aa154047e6a82`
- Head: `bug/container-validation-stray-v1-and-env-target-144` @ `3b0a2b32395d73874b096d9b685fbaf7b0da62d9`

## Executive Summary

This re-audit covers the full branch diff (`81debeb1..3b0a2b3`), which is the prior review's diff (`81debeb1..a79dee48`) plus one additional commit (`3b0a2b3`) that remediates the prior review's single Blocking finding. The remediation is a one-line change to the shared test fixture `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` (adds `-Global` to `Import-Module`) plus a small documentation correction in `docs/mailbridge-runbook.md` (two stale `/v1` references). Both changes were independently verified against the repository's own toolchain (not merely re-read from the executor's committed evidence). The remediation is correct, minimal, and resolves the prior finding without introducing any new production-code risk.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| — | — | — | **No Blocking or Major findings in this re-audit.** | — | — | — |
| Info | `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` | Line 38 (`Import-OpenClawContainerValidationModule`) | The remediation fix (`-Global` added to `Import-Module`) is correct and minimal, exactly matching the prior review's independently-verified scratch-reproduction fix. | No action required. | `-Global` causes `Import-Module` calls executing inside a helper function that itself lives in another module (`Fixtures.psm1`) to register the imported module's exports at global scope rather than as a nested module of the caller, resolving the unscoped-`Mock` visibility failure without altering any test's assertions or the production module's own behavior. | `git diff a79dee48..3b0a2b3 -- tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`; this review's independent standard-invocation re-run confirming both previously-failing tests pass, isolated and full-suite. |
| Info | `docs/mailbridge-runbook.md` | Lines 445, 457 | The two stale `/v1`-suffixed references identified as a non-blocking Minor finding in the prior code review are now corrected (`.../4319/status` and `BaseUrl=http://host.docker.internal:4319`, matching HostAdapter's actual root-level routes). | No action required; finding resolved. | Independently re-confirmed via `grep -n "4319/v1\|/v1/status" docs/mailbridge-runbook.md` (zero matches) and via `src/OpenClaw.HostAdapter/Program.cs`'s route table (all `MapGet` calls are root-level, no `/v1` prefix). | `git diff a79dee48..3b0a2b3 -- docs/mailbridge-runbook.md`. |
| Info | (tooling, not this branch's production code) | `Invoke-PoshQCTest` wrapper re-check | Re-running the MCP wrapper against the post-remediation HEAD produces a distinct 409-passed/7-failed result, reproducible twice, that does not correspond to the previously-remediated defect (a different set of tests) and does not reproduce under a standard `Invoke-Pester` invocation (416/0, both isolated and full-suite). | No action required for this branch; recorded as a documented, non-blocking tooling observation (already captured in agent memory from the prior review cycle as `[[poshqc-test-wrapper-masks-mock-scope-bugs]]`). Independently assessed in `policy-audit.2026-07-11T09-15.md` against the actual `.claude/rules/powershell.md` acceptance bar (Terminal/Test-Explorer parity via standard invocation, matching CI's literal command) and found non-gating. | Independent re-run of `Invoke-PoshQCTest` (409/7, twice) vs. independent re-run of the CI-exact `Invoke-Pester -Path tests/scripts -Output Detailed -CI` command (416/0). |
| Info | (repo-wide convention, carried forward, unchanged) | `CodeCoverage.Path` convention | The one-level glob convention for "repo-wide" PowerShell coverage does not by default recurse into `scripts/powershell/modules/**`; this audit's own independently-regenerated repo-wide figure explicitly widened the path list to include both module directories, demonstrating the fix is straightforward, but the repo's own convention (as used by the executor's evidence) has not itself been updated. | Recommend a follow-up (outside this branch's scope) to widen the repo-wide coverage convention via explicit module-directory globs or `Get-ChildItem -Recurse`-based enumeration. | Pre-existing tooling gap, not introduced or worsened by this branch; carried forward unchanged from the prior review's Info finding. | This audit's independently-regenerated repo-wide coverage run (26 classes, vs. the executor's evidence's 24). |

## Files Reviewed (delta from prior review only)

| File | Change | Assessment |
|---|---|---|
| `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` | +1/-1 | Minimal, surgical, correctly-scoped fix. Adds `-Global` to a single `Import-Module` call; no other lines touched. Independently confirmed to resolve the prior Blocking finding without side effects on any of the 24 other test files sharing this fixture (full-suite pass count unchanged at 416, zero new failures anywhere). |
| `docs/mailbridge-runbook.md` | +2/-2 | Two one-line corrections (stale `/v1` suffix removed from a manual `curl` example and an `.env` example value), matching the prior review's disclosed non-blocking Minor finding exactly. Documentation-only; no code or test impact. |
| `.claude/agent-memory/feature-review/*` (2 files, 1 index update) | New | Agent-memory artifacts recording the prior review's independent findings for future review sessions (the wrapper-quirk mechanism and environment fallbacks). Not application code; not in scope for code-quality assessment, noted for completeness only. |

All other files in the full `81debeb1..3b0a2b3` diff (production logic, other test files, README/runbook dashboard-section corrections) are unchanged from the prior review's `81debeb1..a79dee48` diff and were independently re-examined in this pass; the prior review's assessment of those files (see `code-review.2026-07-11T00-45.md` "Files Reviewed" table) is re-confirmed and carried forward without material change.

## Design Principles

Unchanged from the prior review's assessment — the production-code design (pure helpers, seam reuse, separation of concerns) is untouched by the remediation commit, which contains zero production-code lines.

## Test Quality

- The remediation directly improves test-suite hermeticity and environment-independence: the two previously-failing tests now pass under Terminal, CI, and (implicitly) VS Code Test Explorer invocation paths — the exact parity `.claude/rules/powershell.md` requires.
- No new test files were added by the remediation commit; no new test-quality risk introduced.
- The wrapper-specific 409/7 anomaly (Info finding above) does not indicate a test-quality defect in this branch's own test files; it is isolated to `Invoke-PoshQCTest`'s internal invocation mechanics, independently confirmed non-reproducible under the standard runner.

## Naming and Style

- No new identifiers introduced by the remediation; the single changed line adds a well-known, correctly-spelled cmdlet parameter (`-Global`).

## Error Handling and Logging

- Unaffected by this remediation.

## Dependencies

- No new dependencies.

## I/O Boundaries

- Unaffected by this remediation; the fix concerns PowerShell module-scope import semantics, not I/O.

## Risks Observed During Review

- None newly introduced by this remediation. The one residual risk — the MCP wrapper's own 409/7 quirk — is a review/tooling-environment risk, not a shipped-code risk, and is independently assessed as non-gating against this repository's actual acceptance bar.

## Overall Code Quality Verdict

**PASS — 0 Blocking, 0 Major findings.** The remediation correctly and minimally resolves the prior review's single Blocking finding. The accompanying documentation cleanup is correct. 4 Info findings carried forward or newly observed, none blocking.
