# Plan: install-hostadapter-preflight-ordering (Issue #52)

- Feature: 2026-04-25-install-hostadapter-preflight-ordering-52
- Work Mode: minor-audit
- Plan Timestamp: 2026-04-25T00-00
- Requirements Source: `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/issue.md` (sole AC source — `spec.md`, `user-story.md`, and `research.md` are not required)

## Overview

`Install.ps1` calls `Assert-HostAdapterRuntimePreflight` at Stage 8, after the MSIX is already installed at Stage 7. When the preflight fails, the MSIX remains installed with no `install-record.json`, leaving a partial state that cannot be cleaned up via `Uninstall.ps1`. The fix moves the preflight call to a new guard block between Stage 6 (`.env` guard) and Stage 7 (MSIX install), consistent with the pattern established by the Stage 4 Docker readiness guard and the Stage 6 gateway token guard.

## Files in Scope

Production:
- `scripts/Install.ps1` — Stage 7–8 ordering block (approximately lines 406–430)

Tests:
- `tests/scripts/Install.Tests.ps1` — context `Docker runtime input preflight`, context `stage ordering (happy path)`

## Acceptance Criteria (from `issue.md` § Acceptance Criteria — sole source)

- [x] AC-1: When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-MsixInstall` must NOT have been called.
- [x] AC-2: When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-ComposeUp` must NOT be called.
- [x] AC-3: When `Assert-HostAdapterRuntimePreflight` fails, `Wait-ComposeHealthy` must NOT be called.
- [x] AC-4: The happy-path stage ordering test passes with `Invoke-WebRequest` (the underlying preflight probe) confirmed to execute before `Invoke-MsixInstall`.
- [x] AC-5: All existing `Install.ps1` tests pass without regression.
- [x] AC-6: The full PoshQC toolchain (format → analyze → test) passes without errors.

---

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read policy files in required compliance order and record findings in a baseline instructions-read artifact
  - Files to read in order: `.github/copilot-instructions.md`, `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`
  - Also confirm that `issue.md` contains the `## Acceptance Criteria` section and note the AC items found.
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/baseline/phase0-instructions-read.md` containing at minimum: `Timestamp: <ISO-8601>`, `Policy Order: <ordered list of files confirmed read>`, and a note confirming the `## Acceptance Criteria` section was found in `issue.md`.

- [x] [P0-T2] Run baseline PoshQC format check and save evidence
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/baseline/baseline-format.md` containing `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_format`, `EXIT_CODE: 0`, `Output Summary: <pass confirmation or count of changed files>`.

- [x] [P0-T3] Run baseline PoshQC analyze check and save evidence
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/baseline/baseline-analyze.md` containing `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_analyze`, `EXIT_CODE: 0`, `Output Summary: <pass confirmation or diagnostic count>`.

- [x] [P0-T4] Run baseline PoshQC test suite and save evidence including numeric coverage headline
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/baseline/baseline-test.md` containing `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: 0`, `Output Summary: <pass count, fail count, baseline coverage percent>`.

---

### Phase 1 — Bug Fix Implementation

> Per the repo bugfix workflow: regression tests are introduced first (P1-T1 through P1-T3), before the fix is implemented (P1-T4). Each test-change task below is tagged `[expect-fail]` because it produces a test that will fail against the current production code and pass only after P1-T4 is applied.

- [x] [P1-T1] [expect-fail] Update the assertion in test `'throws before compose up when the HostAdapter status probe is not ready'` in `tests/scripts/Install.Tests.ps1` to expect MSIX was NOT installed when preflight fails
  - Change at approximately line 290: `$global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeTrue` → `$global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse`
  - This assertion currently passes (MSIX is installed before the preflight runs); after this edit it will fail against the unchanged production code, confirming the regression test is correctly positioned.
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` exits non-zero; the test `'throws before compose up when the HostAdapter status probe is not ready'` fails with a `Should -BeFalse` assertion failure; evidence artifact saved at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/regression-testing/fail-before-p1t1.<timestamp>.md` with `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: <non-zero int>`, `Failure: <test name and assertion excerpt from Pester output>`.

- [x] [P1-T2] [expect-fail] Update the `'calls helpers in the correct order'` test in context `'stage ordering (happy path)'` in `tests/scripts/Install.Tests.ps1` to track `Invoke-WebRequest` in `$global:InstallTestCalls` and assert it precedes `Invoke-MsixInstall`
  - In the `BeforeEach` block (approximately line 86): update the existing `Mock Invoke-WebRequest` to also append `'Invoke-WebRequest'` to `$global:InstallTestCalls` while still returning `[pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{}' }`.
  - In the `'calls helpers in the correct order'` `It` block: insert `'Invoke-WebRequest'` into the `$expected` array immediately before `'Invoke-MsixInstall'`.
  - Also update any inline comment within that `It` block that documents the expected call sequence to reflect the new ordering.
  - Before the fix, `Invoke-WebRequest` is recorded AFTER `Invoke-MsixInstall` in `$global:InstallTestCalls`, so this updated assertion will fail.
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` exits non-zero; the test `'calls helpers in the correct order'` fails because `Invoke-WebRequest` appears after `Invoke-MsixInstall` in the actual call sequence; evidence artifact saved at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/regression-testing/fail-before-p1t2.<timestamp>.md` with `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: <non-zero int>`, `Failure: <test name and assertion excerpt from Pester output>`.

- [x] [P1-T3] [expect-fail] Add a new regression test in context `'Docker runtime input preflight'` in `tests/scripts/Install.Tests.ps1` that verifies MSIX is NOT installed when the preflight endpoint is unreachable
  - Test name: `'does not install MSIX when the HostAdapter status probe throws on unreachable endpoint'`
  - The test body: mock `Invoke-WebRequest` to throw `[System.Net.WebException] "Connection refused"`, run `& $script:ScriptPath`, assert it throws with an expected message matching `'*HostAdapter preflight failed*'`, then assert `$global:InstallTestCalls -contains 'Invoke-MsixInstall' | Should -BeFalse`.
  - Before the fix, MSIX is installed before the preflight runs, so `Invoke-MsixInstall` IS in `$global:InstallTestCalls`; the `Should -BeFalse` assertion will fail.
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` exits non-zero; the new test `'does not install MSIX when the HostAdapter status probe throws on unreachable endpoint'` fails because `Invoke-MsixInstall` is present in the call log; evidence artifact saved at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/regression-testing/fail-before-p1t3.<timestamp>.md` with `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: <non-zero int>`, `Failure: <test name and assertion excerpt from Pester output>`.

- [x] [P1-T4] Implement the fix in `scripts/Install.ps1`: move `Assert-HostAdapterRuntimePreflight` and its `Write-Information` log line from the Stage 8 compose-up block to a new guard block between Stage 6 and Stage 7
  - Remove from the Stage 8 `if (-not $SkipDocker)` block: `Write-Information '[install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before compose up' -InformationAction Continue` and `Assert-HostAdapterRuntimePreflight -DestDockerDir $DestDockerDir`.
  - Insert the following new block after the closing `}` of Stage 6 and immediately before the `# Stage 7: MSIX install + capture.` comment:
    ```powershell
    # Stage 7 preflight: HostAdapter readiness guard (must succeed before any state-changing install).
    if (-not $SkipDocker) {
        Write-Information '[install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before MSIX install' -InformationAction Continue
        Assert-HostAdapterRuntimePreflight -DestDockerDir $DestDockerDir
    }
    ```
  - The Stage 8 comment should no longer reference `hostadapter-check`; update it to read `# Stage 8: compose up + health poll (skipped when -SkipDocker).` only.
  - Acceptance: `mcp_drmcopilotext_run_poshqc_test` exits 0; all three previously failing tests from P1-T1, P1-T2, and P1-T3 now pass; `Invoke-MsixInstall` is not present in `$global:InstallTestCalls` when the preflight mock returns 503 or throws.

---

### Phase 2 — Final QC Loop

> All Phase 2 tasks are unconditional. `EXIT_CODE: SKIPPED` is not a valid outcome for any task in this phase. If any step changes files or fails, restart the loop from P2-T1.

- [x] [P2-T1] Run final-QC PoshQC format check via `mcp_drmcopilotext_run_poshqc_format` and save evidence
  - If format changes any file, all subsequent Phase 2 tasks must be re-run from this step.
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/qa-gates/final-qc-format.md` containing `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_format`, `EXIT_CODE: 0`, `Output Summary: <pass confirmation; note any files reformatted>`.

- [x] [P2-T2] Run final-QC PoshQC analyze check via `mcp_drmcopilotext_run_poshqc_analyze` and save evidence
  - If analyze reports errors or applies autofixes, restart the loop from P2-T1.
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/qa-gates/final-qc-analyze.md` containing `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_analyze`, `EXIT_CODE: 0`, `Output Summary: <pass confirmation; zero diagnostics or diagnostic count>`.

- [x] [P2-T3] Run final-QC PoshQC test suite via `mcp_drmcopilotext_run_poshqc_test` and save evidence including numeric post-change coverage headline
  - If any test fails, fix the failure and restart the loop from P2-T1.
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/qa-gates/final-qc-test.md` containing `Timestamp: <ISO-8601>`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: 0`, `Output Summary: <pass count, fail count, post-change coverage percent>`.

- [x] [P2-T4] Record and verify the coverage delta between baseline (P0-T4) and final-QC (P2-T3)
  - Acceptance: Evidence artifact exists at `docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/evidence/qa-gates/coverage-delta.md` containing `BaselineCoverage: <percent from P0-T4>`, `FinalCoverage: <percent from P2-T3>`, `Delta: <signed percent>`, `NewCodeCoverage: <coverage percent for changed files>`, and `Result: PASS` when final coverage is at or above baseline and new/changed-code coverage is at or above 90%; `Result: FAIL` otherwise.
