# Code Review — install-hostadapter-not-started-59

- Artifact type: code-review
- Timestamp: 2026-04-26T02-16
- Branch: bug/install-hostadapter-not-started-59
- Merge base: d2d13b3853538f697d0daadb75b67260019f0abe
- Base branch: development
- Reviewer: feature-review-agent

---

## Scope

Files reviewed: `scripts/Install.ps1` (diff: +57 lines), `scripts/Install.Helpers.psm1` (diff: +1/-1 line), `tests/scripts/Install.HostAdapterStart.Tests.ps1` (new, 64 lines), `tests/scripts/Install.Tests.ps1` (diff: +19/-10 lines).

---

## scripts/Install.ps1

### Finding CR-01: New functions placed in Install.ps1 rather than Install.Helpers.psm1

**Severity:** Medium

The plan (P1-T1 through P1-T4) specified adding `Test-TcpPortOpen`, `Invoke-HostAdapterProcess`, and `Invoke-HostAdapterStart` to `Install.Helpers.psm1` and updating `Export-ModuleMember` to export them. The implementation instead defines all three functions inside `Install.ps1`, each wrapped in an `if (-not (Get-Command -Name '...' -ErrorAction SilentlyContinue))` guard.

Consequences:
- The functions are only available when `Install.ps1` is dot-sourced or executed. They are not exported from the module and cannot be consumed by `Uninstall.ps1` or any future installer script.
- The `if (-not (Get-Command ...))` guard is a test-override pattern used for `Test-IsElevatedAdmin` (which is a script-local admin probe). Applying the same pattern to reusable helpers conflates two different concerns: test mockability and module-boundary design.
- The `Export-ModuleMember` list in `Install.Helpers.psm1` was not updated, which is consistent with the placement choice but conflicts with the plan.

This deviation was implemented consistently (the functions work, tests pass, the `Get-Command` guard is a valid mockability mechanism), but it reduces the modularity of the helpers layer.

**Recommendation:** Move `Test-TcpPortOpen`, `Invoke-HostAdapterProcess`, and `Invoke-HostAdapterStart` to `Install.Helpers.psm1` and add them to `Export-ModuleMember`, consistent with all other shared helpers. Remove the `if (-not (Get-Command ...))` wrappers. Update `Install.HostAdapterStart.Tests.ps1` to import from the module rather than dot-sourcing `Install.ps1`.

### Finding CR-02: Install.Tests.ps1 exceeds 500-line limit

**Severity:** High (policy violation)

`tests/scripts/Install.Tests.ps1` is 506 lines as measured by `wc -l`. The general-code-change policy sets a 500-line hard limit for test files. The file is 6 lines over.

The plan acknowledged this constraint at preflight item 5 and responded by directing `Invoke-HostAdapterStart` unit tests to a new file (`Install.HostAdapterStart.Tests.ps1`). However, the additional mock registration in `BeforeEach` (line 60: `function global:Invoke-HostAdapterStart { ... }`), the `AfterEach` cleanup for `Invoke-HostAdapterStart` (line 109), and the stage-ordering assertion update pushed the file past the limit.

**Recommendation:** Extract one or more `Context` blocks from `Install.Tests.ps1` into a separate file to bring the file under 500 lines. Candidates include the `-Force` prior-install context (lines 358–434) or the `Docker runtime input preflight` context.

### Finding CR-03: Invoke-HostAdapterStart uses global:Invoke-HostAdapterStart override in Install.Tests.ps1

**Severity:** Low

`Install.Tests.ps1` overrides `Invoke-HostAdapterStart` with a global function (`function global:Invoke-HostAdapterStart { ... }`) in `BeforeEach` and cleans it up in `AfterEach`. This differs from the `Mock` pattern used for all other helpers in the same block. The inconsistency arises because `Invoke-HostAdapterStart` is defined in `Install.ps1` at parse time (not imported from a module), which prevents Pester's `Mock` cmdlet from intercepting it via the module scope.

This is a direct consequence of finding CR-01: placing the function in `Install.ps1` rather than `Install.Helpers.psm1` forces a different, less standard mocking approach.

**Recommendation:** Resolving CR-01 (moving the function to the module) will also resolve this inconsistency, allowing `Mock Invoke-HostAdapterStart` to be used like all other helper mocks.

### Finding CR-04: Install.ps1 is exactly 500 lines

**Severity:** Informational

`scripts/Install.ps1` reaches exactly 500 lines (the policy limit). The file has no remaining margin. Any future additions will require extracting existing content to stay within the limit.

**Recommendation:** No action required now, but note the constraint for the next change to this file.

### Finding CR-05: Stage 7a block correctly guarded and positioned

**Severity:** None (positive observation)

The Stage 7a block (lines 451–457) is correctly placed immediately before the Stage 7 preflight block and is properly guarded by `if (-not $SkipDocker)`, consistent with Stages 4, 7, and 9. The `Invoke-HostAdapterStart` call correctly derives the `AspNetCoreUrls` argument from `Get-HostAdapterPreflightUri`, ensuring the port used for the TCP probe matches the port checked by the preflight. The implementation correctly isolates the `.env` read from the call site by passing the result of `Get-InstallEnvFileMap` directly.

### Finding CR-06: Test-TcpPortOpen error handling

**Severity:** Informational

`Test-TcpPortOpen` catches any exception and returns `$false`. This is intentional — it treats any non-connected state as "not open". The `finally { $client.Dispose() }` block ensures resource cleanup. The 500 ms timeout is hardcoded, which is reasonable for a localhost probe.

One minor concern: the broad `catch { return $false }` will suppress unexpected exceptions (e.g., `OutOfMemoryException`, `ThreadAbortException`). In practice, for a localhost TCP probe this is acceptable, but it is worth noting for future maintenance.

### Finding CR-07: Install.Helpers.psm1 change is a formatting-only fix

**Severity:** None (positive observation)

The single change to `Install.Helpers.psm1` (line 371: re-indenting `Select-Object -First 1` to align with the pipeline) is a pre-existing formatting issue corrected by the formatter. It has no behavioral impact. The change is clean and correct.

---

## tests/scripts/Install.HostAdapterStart.Tests.ps1

### Finding CR-08: Test file structure and coverage

**Severity:** None (positive observation)

The new test file covers exactly the three required scenarios (exe-not-found, already-running, not-running). Each `It` is in its own `Context`, uses focused mocks, and has a clear failure mode. The `BeforeAll` correctly imports `Install.Helpers.psm1` with `-Force` and dot-sources `Install.ps1` to bring the new functions into scope. The `[Diagnostics.CodeAnalysis.SuppressMessageAttribute]` for `PSAvoidGlobalVars` is present with a justification matching the existing pattern in `Install.Tests.ps1`.

### Finding CR-09: Test file describes wrong module source

**Severity:** Low

The file header (`Describe 'Install.ps1 — Invoke-HostAdapterStart'`) and the synopsis comment correctly identify `Install.ps1` as the source, which is accurate given the current placement. However, the `BeforeAll` imports `Install.Helpers.psm1` with `-Force` before dot-sourcing `Install.ps1`. The `Install.Helpers.psm1` import is technically not required for testing these functions (they are in `Install.ps1`), but it is harmless and consistent with other test files.

If CR-01 is addressed (moving functions to `Install.Helpers.psm1`), the `BeforeAll` and `Describe` label should be updated to reference the module rather than `Install.ps1`.

---

## Summary of Findings

| ID | File | Severity | Description |
|---|---|---|---|
| CR-01 | `scripts/Install.ps1` | Medium | New functions in wrong file; should be in `Install.Helpers.psm1` |
| CR-02 | `tests/scripts/Install.Tests.ps1` | High | File is 506 lines, 6 over the 500-line limit |
| CR-03 | `tests/scripts/Install.Tests.ps1` | Low | `Invoke-HostAdapterStart` uses global function override instead of `Mock` |
| CR-04 | `scripts/Install.ps1` | Informational | File is at exact limit (500 lines); no remaining margin |
| CR-05 | `scripts/Install.ps1` | None | Stage 7a block placement and guard are correct |
| CR-06 | `scripts/Install.ps1` | Informational | `Test-TcpPortOpen` broad catch is acceptable for localhost probe |
| CR-07 | `scripts/Install.Helpers.psm1` | None | Indentation fix is clean |
| CR-08 | `tests/scripts/Install.HostAdapterStart.Tests.ps1` | None | New test file structure is correct |
| CR-09 | `tests/scripts/Install.HostAdapterStart.Tests.ps1` | Low | Describe label and imports will need update if functions are moved to module |

**Blocking findings for merge:** CR-02 (file size violation).
**Non-blocking findings:** CR-01 (plan deviation, medium severity; functional but reduces reusability), CR-03, CR-09 (both follow from CR-01 and would be resolved together).
