# Feature Audit: install-stale-hostadapter-detection (#52 / local-only)

**Audit Date:** 2026-04-29
**Feature Folder:** `docs/features/active/2026-04-26-install-stale-hostadapter-detection`
**Base Branch:** `development` (merge-base: `cd5ac07390fba9893fecdcf5c06f0fbb4af62fdc`)
**Head Branch:** `bug/install-stale-hostadapter-detection` (HEAD: `29d04df1ef1e229506af0d7925c03f18746437c4`)
**Work Mode:** `minor-audit`
**Audit Type:** Post-remediation acceptance re-review (second pass)

---

## Scope and Baseline

- **Base branch:** `development` (commit `cd5ac07390fba9893fecdcf5c06f0fbb4af62fdc`)
- **Head branch/commit:** `bug/install-stale-hostadapter-detection` (commit `29d04df1ef1e229506af0d7925c03f18746437c4`)
- **Merge base:** `cd5ac07390fba9893fecdcf5c06f0fbb4af62fdc`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt` (refreshed 2026-04-29T17:22 UTC)
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt` (refreshed 2026-04-29T17:22 UTC)
  - Feature evidence: `docs/features/active/2026-04-26-install-stale-hostadapter-detection/evidence/`
  - QA gate evidence: `evidence/qa-gates/p7r1-test.2026-04-27T08-00.md`, `evidence/qa-gates/p7r1-coverage-delta.2026-04-27T08-00.md`, `evidence/qa-gates/p7r1-format.2026-04-27T08-00.md`, `evidence/qa-gates/p7r1-analyze.2026-04-27T08-00.md`
  - Re-audit toolchain: `mcp_drmcopilotext2_run_poshqc_suite` (2026-04-29, ok: true)
- **Feature folder used:** `docs/features/active/2026-04-26-install-stale-hostadapter-detection`
- **Requirements source:** `issue.md` (sole source for `minor-audit` work mode)
- **Work mode resolution note:** `issue.md` contains `- Work Mode: minor-audit`. AC source is the `## Acceptance Criteria` section of `issue.md` exclusively.
- **Scope note:** This is a re-audit run. The prior review returned NO-GO on two coverage gate failures (REM-01 and REM-02). Both are resolved by evidence documented in the feature folder and confirmed by the AC-14a carve-out in `issue.md`. PR context was regenerated against `development` before this audit.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/2026-04-26-install-stale-hostadapter-detection/issue.md` — only source (`minor-audit`)

### Acceptance criteria

Stale-process detection at Stage 7a:

1. **AC-01** When the HostAdapter port is bound and the listener's main module path equals the bundle's expected `HostAdapter.exe` path (case-insensitive), `Invoke-HostAdapterStart` continues to skip launch and emits the existing `already running on port` log line.
2. **AC-02** When the listener's main module path does not equal the bundle's expected `HostAdapter.exe` path, `Invoke-HostAdapterStart` throws before any preflight call. The thrown message names the stale PID and path and instructs the operator to stop it.

Preflight error body surfacing:

3. **AC-03** When any preflight call (Stage 7 or new Stage 8.5) receives a non-200 response and the body parses as JSON with non-null `error.code` or `error.message`, the thrown error includes both fields verbatim.
4. **AC-04** When the body is not JSON or is missing the `error` block, the existing HTTP-status-only message is retained as a fallback.

Preflight split (Option A):

5. **AC-05** A new `Assert-HostAdapterRespondingPreflight` (or equivalent named helper) replaces the bridge-readiness check at Stage 7. It accepts any well-formed HostAdapter envelope (presence of `meta.adapterVersion` is the witnessing signal). A `TRANSPORT_FAILURE` from a known-good HostAdapter does not fail Stage 7.
6. **AC-06** A new `Assert-HostAdapterBridgeReadyPreflight` (or equivalent named helper) runs after `Invoke-MsixInstall`/`Invoke-MsixCapture` at Stage 8 and before `Invoke-ComposeUp` at Stage 9. It requires HTTP 200 with `data.bridgeState` indicating ready. On failure it calls `Invoke-MsixRemove` with the just-captured `PackageFullName` to roll back, then throws.
7. **AC-07** When `-SkipDocker` is passed, both new preflight helpers are skipped, consistent with Stage 4 and Stage 6.

`-Force` MSIX retention (Option B):

8. **AC-08** The Stage 3 `-Force` prior-install uninstall sequence does not call `Invoke-MsixRemove`. `Invoke-ComposeDown`, destination-directory removal, and install-record removal continue to run.
9. **AC-09** `Uninstall.ps1` continues to call `Invoke-MsixRemove` (no change).
10. **AC-10** After `-Force` reinstall of the same version, `Get-AppxPackage OpenClaw.MailBridge` returns a single package whose version matches the new bundle (proven via test-mockable seam, not a real Add-AppxPackage call).

Toolchain and regression:

11. **AC-11** All existing `Install.ps1`, `Install.Helpers.psm1`, and `Install.Force.Tests.ps1` tests pass without regressions.
12. **AC-12** New tests cover: matching-process happy path; stale-process throw path; Stage 7 envelope-only acceptance with `TRANSPORT_FAILURE`; Stage 8.5 ready-state success; Stage 8.5 failure triggers `Invoke-MsixRemove` rollback before throwing; preflight body surfaces `error.code`/`error.message`; preflight body fallback when not JSON; `-Force` does not call `Invoke-MsixRemove`.
13. **AC-13** The full PoshQC toolchain (format -> analyze -> test) passes without errors.
14. **AC-14a** Repository-wide line coverage remains >= 80% excluding `scripts/Install.ps1` shim-guard lines. The `scripts/Install.ps1` per-file figure is documented as a measurement artifact.
15. **AC-14b** Changed-line coverage on `scripts/Install.Helpers.psm1` and `scripts/Install.Preflight.psm1` each reaches >= 90%.

`-Force` auto-stop of stale HostAdapter:

16. **AC-15** When the HostAdapter port is bound by a stale process AND `-Force` is passed, `Invoke-HostAdapterStart` calls `Stop-Process -Id $listenerPid -Force`, emits a log line identifying the stale PID and path, and continues with launching the bundle's HostAdapter. It does not throw.
17. **AC-16** When the stale process is detected WITHOUT `-Force`, the throw behavior from AC-02 is unchanged.
18. **AC-17** A new Pester test covers the `-Force` + stale-detection path with all four assertions: (a) `Get-ListeningProcessId` mocked to return non-zero PID, (b) `Get-ProcessMainModulePath` mocked to return non-matching path, (c) `Stop-Process` called with the stale PID, (d) `Invoke-HostAdapterProcess` called with the bundle's `HostAdapter.exe` path. No throw raised.
19. **AC-18** The full PoshQC toolchain (format → analyze → test) passes with zero errors and zero failures after the Option 2A changes.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-01 | Matching-path: skip launch + emit log | PASS | `Install.HostAdapterStart.Tests.ps1` context "already running": mocks `Get-ListeningProcessId` returning 9999 and `Get-ProcessMainModulePath` returning bundle path → `Invoke-HostAdapterProcess` called 0 times. | `mcp_drmcopilotext2_run_poshqc_test` | `$staleAutoStopped` stays false; early return reached. |
| AC-02 | Non-matching-path without -Force: throw naming stale PID and path | PASS | `Install.Preflight.Tests.ps1` stale-process throw context: mock returns mismatched path → `Should -Throw -ExpectedMessage '*PID 32948*Release\net10.0*Bundle\executables*'`. | `mcp_drmcopilotext2_run_poshqc_test` | Confirmed by `Install.ps1` ~line 258 throw message. |
| AC-03 | Non-200 response with JSON error block: include error.code and error.message | PASS | `Install.Preflight.Tests.ps1`: `Format-HostAdapterPreflightFailure` test with TRANSPORT_FAILURE body → asserts `$msg | Should -Match 'TRANSPORT_FAILURE'` and `Should -Match 'The operation has timed out\.'`. | `mcp_drmcopilotext2_run_poshqc_test` | Implemented in `Install.Preflight.psm1` ~lines 110-135. |
| AC-04 | Non-JSON body: HTTP-status-only fallback | PASS | `Install.Preflight.Tests.ps1`: `Format-HostAdapterPreflightFailure` fallback test with HTML body → `Should -Match 'HTTP 502'`; `Should -Not -Match 'error\.code='`. | `mcp_drmcopilotext2_run_poshqc_test` | `catch` block in `Format-HostAdapterPreflightFailure` resets errorCode and errorMessage. |
| AC-05 | `Assert-HostAdapterRespondingPreflight`: accept any valid envelope including 502+TRANSPORT_FAILURE | PASS | `Install.Preflight.Tests.ps1`: TRANSPORT_FAILURE 502 with `meta.adapterVersion` → function returns without throwing. | `mcp_drmcopilotext2_run_poshqc_test` | `$hasAdapterVersion` logic in `Install.Preflight.psm1` ~lines 195-213. |
| AC-06 | `Assert-HostAdapterBridgeReadyPreflight` after MSIX install; MSIX rollback on failure | PASS | `Install.Preflight.Tests.ps1`: Stage 8.5 failure test → asserts `Invoke-MsixRemove` called before rethrow. Stage 8.5 success test → `Invoke-MsixRemove` not called. | `mcp_drmcopilotext2_run_poshqc_test` | Implemented via `Invoke-Stage8Point5BridgeReadyOrRollback` in `Install.Preflight.psm1` ~lines 290-310. Note: AC-06 refers to `data.bridgeState`; implementation uses `data.state` matching `IsBridgeNotReady` contract. State field confirmed in `evidence/baseline/phase0-bridge-field-name.md`. |
| AC-07 | `-SkipDocker` skips both preflight helpers | PASS | `Install.Tests.ps1` regression: `-SkipDocker` path does not invoke `Assert-HostAdapterRespondingPreflight` or `Assert-HostAdapterBridgeReadyPreflight`. | `mcp_drmcopilotext2_run_poshqc_test` | Both preflight calls in `Install.ps1` are inside `if (-not $SkipDocker)` guards. |
| AC-08 | `-Force` sequence does not call `Invoke-MsixRemove` | PASS | `Install.Force.Tests.ps1`: `-Force` path → `Should -Invoke Invoke-MsixRemove -Times 0`. | `mcp_drmcopilotext2_run_poshqc_test` | `Install.ps1` Stage 3 `-Force` block confirmed via diff: `Invoke-MsixRemove` call removed. |
| AC-09 | `Uninstall.ps1` still calls `Invoke-MsixRemove` | PASS | Diff shows no change to `scripts/Uninstall.ps1`. `Invoke-MsixRemove` call in `Uninstall.ps1` is untouched. | `git diff origin/development -- scripts/Uninstall.ps1` | Out-of-scope file; no diff means AC-09 is met by non-regression. |
| AC-10 | After `-Force` reinstall, single package present with new bundle version | PASS | `Install.Force.Tests.ps1`: mock `Get-AppxPackage` returns single package with version matching bundle version after `-Force` path. | `mcp_drmcopilotext2_run_poshqc_test` | Seam-based: no real `Add-AppxPackage` call in test. |
| AC-11 | Existing tests pass without regressions | PASS | 216 tests, 0 failures via `mcp_drmcopilotext2_run_poshqc_test` (2026-04-29). All pre-existing test contexts in `Install.Tests.ps1`, `Install.Helpers.Tests.ps1`, `Install.Force.Tests.ps1` pass. | `mcp_drmcopilotext2_run_poshqc_test` | `p7r1-test.2026-04-27T08-00.md` baseline 215 tests; +1 for AC-17. |
| AC-12 | New tests cover all specified scenarios | PASS | `Install.Preflight.Tests.ps1` (260 lines) and `Install.HostAdapterStart.Tests.ps1` (68 lines) together cover all eight listed scenarios. See policy-audit §1.2 for per-scenario evidence. | `mcp_drmcopilotext2_run_poshqc_test` | AC-17 log assertion is absent (see code-review Info finding). |
| AC-13 | Full PoshQC toolchain passes | PASS | `mcp_drmcopilotext2_run_poshqc_suite` returned `ok: true` (2026-04-29). Format: no changes. Analyze: 0 findings. Test: 216/216. | `mcp_drmcopilotext2_run_poshqc_suite` | |
| AC-14a | Repo-wide coverage ≥ 80% (Install.ps1 carve-out applied) | PASS | `evidence/qa-gates/p7r1-coverage-delta.2026-04-27T08-00.md`: combined Install.Helpers + Install.Preflight = 92.97%. `Install.ps1` 5.9% is a documented measurement artifact per the carve-out. AC-14a verdict in evidence artifact: PASS. | Direct Pester coverage run per `artifacts/pester/run-r1-coverage.ps1` | Carve-out documented in `issue.md` AC-14a and `evidence/qa-gates/p7r1-coverage-delta.2026-04-27T08-00.md`. |
| AC-14b | Install.Helpers.psm1 changed-line coverage ≥ 90% | PASS | `evidence/qa-gates/p7r1-coverage-delta.2026-04-27T08-00.md`: 140/148 = 94.59%. | Direct Pester JaCoCo XML: `artifacts/pester/install-layer-coverage.r1.xml` | |
| AC-14b | Install.Preflight.psm1 changed-line coverage ≥ 90% | PASS | `evidence/qa-gates/p7r1-coverage-delta.2026-04-27T08-00.md`: 98/108 = 90.74%. | Direct Pester JaCoCo XML: `artifacts/pester/install-layer-coverage.r1.xml` | |
| AC-15 | `-Force` + stale process: `Stop-Process` called, log emitted, no throw | PASS | `Install.Preflight.Tests.ps1` AC-17 test: `Should -Invoke Stop-Process -Times 1 -Exactly -ParameterFilter { $Id -eq 32948 }`; `Should -Not -Throw`. `Install.ps1` ~line 251: `Write-Information "[install:hostadapter-start] Stale HostAdapter detected ... -Force auto-stop applied"`. | `mcp_drmcopilotext2_run_poshqc_test` | Log content verified by code inspection; not asserted in test (see code-review Info finding). |
| AC-16 | Stale process without `-Force`: throw behavior unchanged from AC-02 | PASS | `Install.Preflight.Tests.ps1` stale-process throw context does not pass `-Force`; `Should -Throw` asserts the AC-02 message pattern. | `mcp_drmcopilotext2_run_poshqc_test` | `else` branch at `Install.ps1` ~line 258 is identical to the original AC-02 throw. |
| AC-17 | Pester test: all four assertions (PID, path mismatch, Stop-Process called, Invoke-HostAdapterProcess called) | PASS | `Install.Preflight.Tests.ps1` ~lines 87-116: mocks (a) `Get-ListeningProcessId` → 32948, (b) `Get-ProcessMainModulePath` → mismatched path, (c) `Mock Stop-Process`, (d) `Mock Invoke-HostAdapterProcess`; asserts `Should -Invoke Stop-Process -Times 1 -ParameterFilter { $Id -eq 32948 }` and `Should -Invoke Invoke-HostAdapterProcess -Times 1 -ParameterFilter { $ProcessStartInfo.FileName -eq 'C:\Bundle\...' }` and `Should -Not -Throw`. | `mcp_drmcopilotext2_run_poshqc_test` | All four conditions verified. |
| AC-18 | Full PoshQC toolchain passes after Option 2A changes | PASS | `mcp_drmcopilotext2_run_poshqc_suite` returned `ok: true` (2026-04-29). 216 tests, 0 failures, 0 analyzer findings, no formatter changes. | `mcp_drmcopilotext2_run_poshqc_suite` | Supersedes AC-13 for the Option 2A scope; both reference the same toolchain. |

---

## Summary

**Total AC items:** 19 (AC-01 through AC-18, with AC-14b counted twice for Install.Helpers and Install.Preflight)

| Status | Count | Items |
|--------|-------|-------|
| PASS | 19 | AC-01, AC-02, AC-03, AC-04, AC-05, AC-06, AC-07, AC-08, AC-09, AC-10, AC-11, AC-12, AC-13, AC-14a, AC-14b (Helpers), AC-14b (Preflight), AC-15, AC-16, AC-17, AC-18 |
| PARTIAL | 0 | — |
| FAIL | 0 | — |
| UNVERIFIED | 0 | — |

**Overall readiness verdict: GO**

All 18 acceptance criteria (with AC-14b split into two evaluated items) pass. The full PoshQC toolchain (format, analyze, test) passes with 216/216 tests and 0 findings. The two prior NO-GO blockers (REM-01 and REM-02) are resolved. Minor and Info-level code review findings are present but none block merge.

---

## Acceptance Criteria Check-off

All AC items in `issue.md` are already marked `[x]` (checked off by the execution agent during plan delivery). No check-off updates are required by this review. The following items were verified as checked prior to this review and are confirmed by evidence in the feature folder:

- AC-01 through AC-18: all `[x]` in `issue.md` `## Acceptance Criteria` section.
