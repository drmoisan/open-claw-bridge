# Code Review — Issue #139 (deploy-wrapper-entry-point)

- Reviewed: 2026-07-10T16-30
- Base: `main` @ `c72782357d1cb10848d53d92f0d5cde091cc8c92`
- Head: `feature/deploy-wrapper-entry-point-139` @ `f6c1ca9`

## Executive Summary

The branch adds a new first-class entry point `scripts/Deploy.ps1` that composes `scripts/Publish.ps1` and the staged `Install.ps1` end-to-end, and fixes a bundled defect in `Publish.ps1` where three helper call sites did not suppress their return values, causing a captured invocation to leak intermediate pipeline objects instead of emitting exactly one (the bundle root). The change is minimal, correctly scoped to exactly 2 production files plus 2 test files, uses the established wrapper-function seam pattern for testability, and is well covered by both a fail-before/pass-after regression test for the `Publish.ps1` fix and 9 new tests for `Deploy.ps1`. No design-principle, naming, error-handling, dependency, or I/O-boundary concerns were found.

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Info | `scripts/Deploy.ps1` | Lines 136-140 (comment) / line 143 (call site) | `-WhatIf` reaches `Invoke-PublishScript` only via PowerShell's automatic dynamic-scope inheritance of `$WhatIfPreference`, not an explicit `-WhatIf:$WhatIfPreference` forward (unlike the `Invoke-InstallScript` call at line 165, which does forward explicitly). | No action required; the inline comment at lines 136-140 already documents the rationale, and the `-WhatIf propagation` test (P2-T18) passes. Consider, as a purely stylistic follow-up, forwarding `-WhatIf:$WhatIfPreference` explicitly at both call sites for symmetry and to avoid relying on implicit preference-variable inheritance, which is correct here but easy to break if the wrapper function signature changes in the future. | PowerShell's dynamic scoping does correctly propagate `$WhatIfPreference` to a script-scoped nested function call in the same session; this is verified working behavior, not a defect, but explicit forwarding is more robust to future refactors. | `scripts/Deploy.ps1:136-143`; independently reproduced passing `-WhatIf propagation` test in this audit's repo-wide Pester run. |
| Info | `scripts/Deploy.ps1` | Lines 89-103, 107-121 | The two wrapper-function guard bodies (`$PSCmdlet.ShouldProcess(...)`/`return & ...` inside `Invoke-PublishScript`/`Invoke-InstallScript`) are, by design, exercised only by real (non-mocked) invocation — every unit test pre-registers a global override that bypasses these bodies. | No action required; this is the documented, intentional trade-off of the wrapper-function seam pattern (same accepted pattern as `Test-IsElevatedAdmin` in `scripts/Install.ps1`). Per-file coverage (87.10%) remains above the 85% floor. | Consistent, established repo precedent; disclosed transparently in the branch's own risk notes (`evidence/other/pr-notes.2026-07-10T15-02.md`). | Independently confirmed via per-line JaCoCo parse: lines 99, 100, 117, 118 are the only missed lines in `Deploy.ps1`. |
| Info | `tests/scripts/Publish.Tests.ps1` | Lines 402-417 (new `Context 'output contract'`) | The new regression test relies on the file's pre-existing `Invoke-MakeAppx` mock returning a non-null `$OutputMsixPath`, which is what made the pre-fix leak observable (a mock returning `$null` would have masked the defect even before the fix). | No action required; this is a reasonable, minimal regression test built on existing sibling-file fixture conventions, and its fail-before behavior was independently captured in `evidence/regression-testing/ps-expect-fail.2026-07-10T15-02.md` before being relied upon. | Confirms the test genuinely exercises the defect rather than being tautological. | `evidence/regression-testing/ps-expect-fail.2026-07-10T15-02.md` (Expected 1, but got 2). |

No Blocking, Major, or Minor findings were identified.

## Files Reviewed

| File | Change | Assessment |
|---|---|---|
| `scripts/Publish.ps1` | 3 call sites changed from bare calls to `$null = <call>` (lines 221, 227, 245) | Minimal, correct, exact match to the spec's stated fix. No other line changed (confirmed via `git diff`). |
| `scripts/Deploy.ps1` | New file, 169 lines | See Design Principles and Test Quality below. |
| `tests/scripts/Publish.Tests.ps1` | New `Context 'output contract'` block (+18 lines) | See Test Quality below. |
| `tests/scripts/Deploy.Tests.ps1` | New file, 187 lines | See Test Quality below. |

## Design Principles (general-code-change.md)

- **Simplicity first**: `Deploy.ps1` is a thin orchestrator — parameter forwarding, two wrapper-function calls, a fail-fast guard, and a return. No unnecessary abstraction is introduced.
- **Reusability**: the wrapper-function seam pattern (`Invoke-PublishScript`/`Invoke-InstallScript`) directly reuses the established `Get-Command`-guard convention from `scripts/Install.ps1`'s `Test-IsElevatedAdmin`/`Invoke-HostAdapterStart`, rather than inventing a new mocking mechanism.
- **Extensibility**: all parameters are optional with sensible defaults (`Configuration` defaults to `'Release'`, matching `Publish.ps1`); the `$publishParams`/`$installParams` hashtable-building pattern (only including bound parameters) correctly preserves each child script's own defaults when a `Deploy.ps1` parameter is not supplied.
- **Separation of concerns**: `Deploy.ps1` performs no data transformation of its own (per spec.md's explicit statement); it captures and forwards the bundle-root string unmodified. I/O (the actual publish/install work) is fully delegated to the two child scripts.
- **Non-goals honored**: `Install.ps1`, `Publish.Helpers.psm1`, and `Publish.Msix.psm1` are confirmed byte-identical (`git diff` empty for all three) — the fix touches only the three `Publish.ps1` call sites, not the helper functions' own `return` statements.

## Test Quality

### `tests/scripts/Deploy.Tests.ps1`

- One behavior per `It`, organized into clear `Context` blocks (`parameter forwarding`, `-SkipSign to -AllowUnsigned mapping`, `publish-failure short-circuit`, `-WhatIf propagation`, `return value`) — good readability and traceability to individual acceptance criteria (each `It` name references its AC).
- Independence/isolation: `BeforeEach` resets `$global:DeployTestCalls` and re-registers both global stub functions for every test, so no test can leak state into another.
- Determinism: no network, no clock, no external process, no temp files (independently confirmed via `grep` in this audit — zero matches for `New-TemporaryFile`/`GetTempPath`/`$env:TEMP`).
- Mock signature parity: both `global:Invoke-PublishScript` and `global:Invoke-InstallScript` stub signatures match the production wrapper functions' named parameters exactly (`PublishScriptPath`/`PublishParams`, `InstallScriptPath`/`InstallParams`).
- The `PSAvoidGlobalVars` suppression at the file top is justified with a clear, specific comment (global stub functions and the script-under-test run in different scopes) rather than a blanket suppression — appropriate use of `SuppressMessageAttribute`.
- Negative/failure-path coverage: both the "publish throws" and "publish returns empty string" fail-fast paths are tested independently (`publish-failure short-circuit` context), each asserting `Invoke-InstallScript` was never called — directly verifies the fail-fast contract, not just the exception message.
- The `-SkipSign` -> `-AllowUnsigned` mapping test correctly also asserts the negative case (mapping is absent when `-SkipSign` is not supplied), guarding against a hypothetical always-true regression.

### `tests/scripts/Publish.Tests.ps1` (new `Context 'output contract'`)

- Single, focused `It` with a clear docstring-style comment explaining the regression scenario, why it fails pre-fix, and why it passes post-fix — strong traceability.
- Assertion (`@($result).Count | Should -Be 1`) directly targets the defect's observable symptom (pipeline object count), not an implementation detail.
- Regression evidence quality: the branch captured explicit red (`evidence/regression-testing/ps-expect-fail.2026-07-10T15-02.md`, showing the actual `Expected 1, but got 2` failure) and green (`evidence/regression-testing/ps-post-fix-pass.2026-07-10T15-02.md`) evidence — independently reviewed and consistent with genuine fail-before/pass-after verification, not a tautological test.

## Naming and Style

- All identifiers follow repository convention: `PascalCase` for function/parameter names (`Invoke-PublishScript`, `PublishScriptPath`), approved verbs (`Invoke-`, per PSScriptAnalyzer's verb-noun enforcement, confirmed zero `PSUseApprovedVerbs` findings in this audit's independent `Invoke-ScriptAnalyzer` run).
- No abbreviations introduced beyond the already-standard repo convention (`AC` in test names, `PSCmdlet`).
- Comment-block documentation (`.SYNOPSIS`/`.DESCRIPTION`/`.PARAMETER`/`.EXAMPLE`) on `Deploy.ps1` is thorough and explains the wrapper-seam design rationale, matching the repo's existing comment-based-help convention on sibling scripts.

## Error Handling and Logging (general-code-change.md)

- Fail-fast is explicit and diagnostic: `if ([string]::IsNullOrWhiteSpace($bundleRoot)) { throw "Publish.ps1 did not return a bundle root; refusing to invoke the staged Install.ps1. Verify $publishScriptPath completed successfully and returned its bundle root." }` — a clear, specific error message naming the exact script path, not a generic message.
- A publish-stage exception is allowed to propagate uncaught (no broad catch-all), consistent with the "fail fast and explicitly" and "do not silently ignore errors" requirements.
- `Write-Information`-based logging uses the established `[deploy]`/`[deploy:publish]`/`[deploy:install]`-prefixed convention, matching `Publish.ps1`'s and `Install.ps1`'s existing `[publish]`/`[install]`-prefixed logging.

## Dependencies

- No new package dependencies introduced. `Deploy.ps1` uses only built-in PowerShell cmdlets (`Join-Path`, `Write-Information`, `Get-Command`) already used throughout the repo's scripting layer.

## I/O Boundaries (general-code-change.md)

- `Deploy.ps1` performs no direct I/O of its own; both actual I/O-bearing operations (publish, install) are delegated through the wrapper-function seam to the two existing scripts. This isolates `Deploy.ps1`'s own logic (parameter forwarding, fail-fast guard) from the I/O boundary, matching the "isolate I/O into specific classes or modules" requirement (adapted here to a PowerShell wrapper-function seam rather than a class, which is the established idiom for this codebase's scripting layer).
- `Deploy.ps1` does not read, copy, or otherwise open the files referenced by `-AnthropicEnvFilePath`/`-DockerEnvFilePath` — confirmed via source read (no `Get-Content`/`Copy-Item`/`Test-Path` against these two parameters anywhere in the file) — honoring the spec's explicit secret-handling constraint.

## Risks Observed During Review

- None rise to a blocking level. The three Info-severity notes above are disclosed design trade-offs, not defects, and are consistent with established repository precedent (`Test-IsElevatedAdmin`-style wrapper seams, implicit `$WhatIfPreference` inheritance).

## Overall Code Quality Verdict

**PASS.** The change is minimal, correctly scoped within the direct-mode change budget, well-tested with genuine fail-before/pass-after regression evidence for the `Publish.ps1` fix and comprehensive positive/negative coverage for `Deploy.ps1`, and respects all stated non-goals and design constraints from `spec.md`. No design-principle, naming, error-handling, dependency, or I/O-boundary concerns rise above informational severity.
