# Code Review: install-stale-hostadapter-detection (#52 / local-only)

**Review Date:** 2026-04-29
**Reviewer:** feature_code_review_agent
**Feature Folder:** `docs/features/active/2026-04-26-install-stale-hostadapter-detection`
**Base Branch:** `development` (merge-base: `cd5ac07390fba9893fecdcf5c06f0fbb4af62fdc`)
**Head Branch:** `bug/install-stale-hostadapter-detection` (HEAD: `29d04df1ef1e229506af0d7925c03f18746437c4`)
**Review Type:** Post-remediation re-review (second pass; first pass returned NO-GO)

---

## Executive Summary

This change resolves three adjacent defects in `scripts/Install.ps1` that together made Install Path D unrunnable in the two most common operator scenarios: a stale dev-build HostAdapter occupying the configured port, and a `-Force` reinstall after the prior Stage 3 sequence had removed the MSIX. The implementation spans four commits (2026-04-27 to 2026-04-29) and adds 2473 lines (net: +2022) across 64 files, the majority of which are evidence documents and test files.

**What changed:**
- `scripts/Install.Preflight.psm1` was created (305 lines) to host Stage 7 (`Assert-HostAdapterRespondingPreflight`) and Stage 8.5 (`Assert-HostAdapterBridgeReadyPreflight`, `Invoke-Stage8Point5BridgeReadyOrRollback`) preflight helpers and the shared `Format-HostAdapterPreflightFailure` formatter.
- `scripts/Install.Helpers.psm1` gained three new wrapper seams: `Get-ListeningProcessId`, `Get-ProcessMainModulePath`, and `Invoke-HostAdapterStatusRequest`.
- `scripts/Install.ps1` was refactored: the stale-detection branch in `Invoke-HostAdapterStart` now validates the listener's identity; the `-Force` prior-install sequence no longer calls `Invoke-MsixRemove`; Stage 7 delegates to the new preflight module; Stage 8.5 is a new step that rolls back the MSIX before throwing.
- Option 2A was implemented: when `-Force` is passed and a stale process is detected, `Stop-Process` terminates it and `Invoke-HostAdapterStart` falls through to launch the bundle's HostAdapter.
- Test files were reorganized: `Install.Helpers.Tests.ps1` was split at the 500-line policy boundary; `Install.Preflight.Tests.ps1` was created with 260 lines including the AC-17 `-Force` + stale-detection test.

**Top 3 risks:**
1. `Install.Helpers.psm1` is now at 499 lines — one line below the 500-line policy limit. Any future addition will require a split before it can land.
2. The `Stop-Process -Id $listenerPid -Force` call in `Invoke-HostAdapterStart` has no privilege escalation check. On a machine where the stale process is owned by a different account or has elevated privileges, `Stop-Process` will throw and abort the install. The current error message does not explicitly diagnose the elevation path for this case.
3. `Assert-HostAdapterBridgeReadyPreflight` infers bridge readiness solely from the `data.state` field. If the HostAdapter introduces a new not-ready state string in a future release, the check will pass prematurely. The contract comment acknowledges this and ties it to `IsBridgeNotReady` at `src/OpenClaw.HostAdapter/Program.cs:444`.

**PR readiness recommendation:** **Go** — All acceptance criteria are met, the full PoshQC toolchain passes with 0 findings and 216/216 tests, and no blockers or major findings are present. The two noted risks are minor and within acceptable tolerance for the feature scope.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | `scripts/Install.Helpers.psm1` | File level | File is at 499 lines — one line below the 500-line policy limit. Any addition without a split will breach the policy. | Open a follow-up issue or add a comment noting the boundary before the next change to this file. | Policy in `general-code-change.instructions.md` prohibits files exceeding 500 lines. The file reached this state through the deliberate split plan in `split-plan.2026-04-27T09-30.md`. | Line count: 499 lines confirmed by `Get-ChildItem` inspection on 2026-04-29. |
| Minor | `scripts/Install.ps1` | `Invoke-HostAdapterStart`, `if ($Force)` branch (~line 251) | `Stop-Process -Id $listenerPid -Force` has no catch block. If the caller lacks permission to terminate the stale process (e.g., process owned by a different user), `Stop-Process` will throw with an access-denied error. The thrown message does not identify the privilege gap. | Add a catch block that rethrows with a message naming the stale PID and recommending elevation or manual stop. | Operators running without elevated rights on shared machines would receive a raw .NET exception rather than a diagnostic remediation message. | `scripts/Install.ps1` ~line 251-253; `tests/scripts/Install.Preflight.Tests.ps1` AC-17 test does not mock the failure case. |
| Nit | `scripts/Install.Preflight.psm1` | `Get-PreflightTokenAndUri` (~lines 148-170) | Function is marked `# Internal helper` via comment but is not excluded from `Export-ModuleMember`. The `Export-ModuleMember` at the bottom of the file lists only the intended public functions, which correctly excludes `Get-PreflightTokenAndUri`. However, functions not explicitly listed in `Export-ModuleMember` are still exported by default in a module without a manifest. | Confirm the module is loaded with `-Function` restriction or add `Get-PreflightTokenAndUri` to a negation list, or add a `.psd1` manifest with an explicit `FunctionsToExport`. | Callers could inadvertently depend on the internal helper. This is low risk given the module is bundle-internal, but it is inconsistent with the stated intent. | `scripts/Install.Preflight.psm1` Export-ModuleMember block (final lines); `Import-Module ... -Force` in test files. |
| Info | `scripts/Install.Preflight.psm1` | `Assert-HostAdapterBridgeReadyPreflight` (~lines 218-270) | The not-ready state list (`@('starting', 'waiting_for_outlook')`) is a local constant. If `IsBridgeNotReady` in `src/OpenClaw.HostAdapter/Program.cs:444` is extended with new states, this list must be updated manually. | Document the dependency on `IsBridgeNotReady` explicitly in a comment, or extract the list to a shared constant. | Low risk within the current codebase, but a future reviewer will not find the coupling without the comment. | `scripts/Install.Preflight.psm1` ~line 230; `evidence/baseline/phase0-bridge-field-name.md` confirms the field name. |
| Info | `tests/scripts/Install.Preflight.Tests.ps1` | AC-17 test (~lines 87-116) | The AC-17 test does not assert that a log message identifying the stale PID and the `-Force` auto-stop action was emitted (AC-15 requires a `[install:hostadapter-start]` log line). The test correctly asserts `Stop-Process` was called and `Invoke-HostAdapterProcess` was called; the log assertion is absent. | Consider adding a `Should -Invoke Write-Information` assertion to fully verify the AC-15 log requirement. | Low impact — the `Write-Information` call is visible in the production code and the functional assertions are comprehensive — but the omission leaves the log content unverified. | `scripts/Install.ps1` ~line 251; `tests/scripts/Install.Preflight.Tests.ps1` lines 87-116. |

No Blockers or Major findings.

---

## Implementation Audit

### PowerShell implementation audit

#### What changed well

- The preflight split into `Install.Preflight.psm1` is clean and cohesive. The module has a single clear purpose (preflight verification), and the internal/public boundary is expressed via the `Export-ModuleMember` block. The 305-line file is well within policy limits with room to grow.
- `Format-HostAdapterPreflightFailure` correctly follows the AC-04 fallback contract: JSON parsing is wrapped in a `try/catch` that silently reverts to the legacy HTTP-status-only message on parse failure. The boundary cases (`$errorCode` present only, `$errorMessage` present only) are explicitly tested.
- The `Invoke-HostAdapterStart` stale-detection logic uses `[string]::Equals(..., OrdinalIgnoreCase)` for path comparison rather than `-eq`, which is the correct choice for Windows filesystem paths.
- `$staleAutoStopped = $false` / `if (-not $staleAutoStopped) { ... return }` is a clear state flag that avoids deeply nested conditionals and preserves the early-return for the legitimate already-running path.
- `Invoke-Stage8Point5BridgeReadyOrRollback` wraps the MSIX rollback in a `try/catch` that swallows rollback errors and logs them before re-throwing the original bridge-ready error. This correctly prioritizes the original error message in operator-facing output while still attempting cleanup.

#### Error handling and logging

- `$ErrorActionPreference = 'Stop'` is set at the module level in both `Install.Helpers.psm1` and `Install.Preflight.psm1`. This means uncaught errors terminate the module cleanly rather than continuing silently.
- All public throw messages are operator-facing and actionable. They name specific files, PIDs, paths, and remediation steps.
- The `[install:hostadapter-start]` prefix on `Write-Information` calls is consistent with the existing log-level convention in `Install.ps1`.
- One gap: `Stop-Process` in the `-Force` auto-stop branch is not wrapped in a catch. See Minor finding above.

#### Structure and separation of concerns

- `Install.ps1` is the thin orchestrator. All preflight logic is in `Install.Preflight.psm1`; all wrapper seams are in `Install.Helpers.psm1`. The script imports both modules at startup and delegates appropriately. The orchestrator is 426 lines and well within the policy limit.
- The `if (-not (Get-Command -Name '...' -ErrorAction SilentlyContinue))` shim guard pattern in `Install.ps1` preserves testability via dot-sourcing. This pattern is consistent with the existing codebase convention.
- `Get-PreflightTokenAndUri` is an internal helper that consolidates token-resolution and URI-derivation logic shared by both preflight functions. Extracting it avoids duplication without over-abstracting.
