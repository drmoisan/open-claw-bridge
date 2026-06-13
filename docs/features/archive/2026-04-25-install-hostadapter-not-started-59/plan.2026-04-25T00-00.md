# Plan: install-hostadapter-not-started (Issue #59)

- Feature folder: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/`
- Work mode: `minor-audit`
- Plan path: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/plan.2026-04-25T00-00.md`
- Branch: `bug/install-hostadapter-not-started-59`
- Date: 2026-04-25

---

## PREFLIGHT — Invariants to Verify Before Execution

1. `issue.md` contains an explicit `## Acceptance Criteria` section — **verified** (lines 64–73).
2. No `spec.md` or `user-story.md` exists in the feature folder — **verified** (folder contains only `issue.md`).
3. `scripts/Install.Helpers.psm1` is currently 466 lines. Adding `Invoke-HostAdapterStart` (~30 lines) plus an export entry must remain ≤ 500.
4. `scripts/Install.ps1` is currently 445 lines. Adding Stage 7a block (~7 lines) must remain ≤ 500.
5. `tests/scripts/Install.Tests.ps1` is currently 503 lines. New tests for `Invoke-HostAdapterStart` (~60–70 lines) will exceed 500 — a **split is required**: extract `Invoke-HostAdapterStart` tests into `tests/scripts/Install.HostAdapterStart.Tests.ps1`.
6. `Assert-HostAdapterRuntimePreflight` must not be modified.
7. `-SkipDocker` must gate the new Stage 7a block, consistent with Stages 4, 7, 9.
8. `Invoke-HostAdapterStart` must use `CmdletBinding(SupportsShouldProcess)` per state-changing policy.
9. TCP check must use a .NET socket, not network cmdlets, to avoid external-process dependencies in tests.
10. `Export-ModuleMember` in `Install.Helpers.psm1` must include `Invoke-HostAdapterStart`.

---

## PLANNER QUESTIONS (answered inline)

**Q1: How to derive `ASPNETCORE_URLS` from the preflight URI?**
`Get-HostAdapterPreflightUri` returns a URI of the form `http://127.0.0.1:4319/v1/status`. The base URL for `ASPNETCORE_URLS` is `"$($uri.Scheme)://$($uri.Host):$($uri.Port)"` (no path). This is derived in the Stage 7a block in `Install.ps1` before passing to `Invoke-HostAdapterStart`.

**Q2: TCP-check implementation?**
Use `[System.Net.Sockets.TcpClient]::new()` with a `ConnectAsync` / `Wait` call with a short timeout (~500 ms). This is .NET-only, no external processes, and easily mockable via a wrapper function seam `Test-TcpPortOpen`.

**Q3: Test file size — split required?**
Yes. `Install.Tests.ps1` is already at 503 lines. New `Invoke-HostAdapterStart` unit tests go into a new file `tests/scripts/Install.HostAdapterStart.Tests.ps1`. This file is within the test-file budget (3 test files per batch: `Install.Tests.ps1` additions + new file = 2 test files, within budget).

**Q4: Mock seam for `[System.Diagnostics.Process]::Start`?**
Extract the call into a wrapper function `Invoke-HostAdapterProcess -ProcessStartInfo <psi>` in `Install.Helpers.psm1`. Tests mock `Invoke-HostAdapterProcess`. This follows the wrapper-function-seam pattern required by `.claude/rules/powershell.md`.

**Q5: Mock seam for TCP check?**
Extract into `Test-TcpPortOpen -IpAddress <string> -Port <int>` in `Install.Helpers.psm1`. Tests mock `Test-TcpPortOpen`. Both wrapper functions are exported from `Install.Helpers.psm1`.

---

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read policy file `C:\Users\DanMoisan\repos\open-claw-bridge\CLAUDE.md` and record its standing instructions in the Phase 0 evidence artifact at `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/phase0-instructions-read.md`.

- [x] [P0-T2] Read policy file `.claude/rules/general-code-change.md` and append the read confirmation to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/phase0-instructions-read.md`.

- [x] [P0-T3] Read policy file `.claude/rules/general-unit-test.md` and append the read confirmation to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/phase0-instructions-read.md`.

- [x] [P0-T4] Read policy file `.claude/rules/powershell.md` and append the read confirmation to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/phase0-instructions-read.md`. Finalize `phase0-instructions-read.md` with `Timestamp:` and `Policy Order:` fields.

- [x] [P0-T5] Run PoshQC formatter (`mcp__drmCopilotExtension__run_poshqc_format`) against the repository. Record the result in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-format.2026-04-25T00-00.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

- [x] [P0-T6] Run PoshQC analyzer (`mcp__drmCopilotExtension__run_poshqc_analyze`) against the repository. Record the result in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-analyze.2026-04-25T00-00.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

- [x] [P0-T7] Run PoshQC tests in coverage mode (`mcp__drmCopilotExtension__run_poshqc_test`) against the repository. Record the result in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-test.2026-04-25T00-00.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (must include numeric line-coverage headline value).

---

### Phase 1 — Implementation

- [x] [P1-T1] In `scripts/Install.Helpers.psm1`, add function `Test-TcpPortOpen` (lines before `Export-ModuleMember`):
  - Parameters: `[string]$IpAddress` (mandatory), `[int]$Port` (mandatory).
  - Uses `[System.Net.Sockets.TcpClient]::new()` with a 500 ms `ConnectAsync` / `Wait` call.
  - Returns `$true` when the port is reachable, `$false` on timeout or failure (swallows connection-refused only).
  - `CmdletBinding()` only (read-only, no ShouldProcess required).
  - Acceptance: function is present in the file, file remains ≤ 500 lines.

- [x] [P1-T2] In `scripts/Install.Helpers.psm1`, add wrapper function `Invoke-HostAdapterProcess` (lines after `Test-TcpPortOpen`, before `Export-ModuleMember`):
  - Parameters: `[System.Diagnostics.ProcessStartInfo]$ProcessStartInfo` (mandatory).
  - Body: `[System.Diagnostics.Process]::Start($ProcessStartInfo)`.
  - `CmdletBinding(SupportsShouldProcess = $true)`.
  - Acceptance: function is present in the file, file remains ≤ 500 lines.

- [x] [P1-T3] In `scripts/Install.Helpers.psm1`, add function `Invoke-HostAdapterStart` (lines after `Invoke-HostAdapterProcess`, before `Export-ModuleMember`):
  - Synopsis: Ensures OpenClaw.HostAdapter is running by launching it from the supplied exe path if the configured port is not yet listening.
  - Parameters: `[string]$HostAdapterExePath` (mandatory), `[string]$AspNetCoreUrls` (mandatory).
  - `CmdletBinding(SupportsShouldProcess = $true)`.
  - Logic (in order):
    1. If `$HostAdapterExePath` does not exist (via `Test-Path`): throw `"HostAdapter executable not found at '$HostAdapterExePath'. The bundle may be incomplete or the destination copy did not complete."`.
    2. Parse port from `$AspNetCoreUrls` using `[UriBuilder]::new($AspNetCoreUrls).Port`.
    3. Call `Test-TcpPortOpen -IpAddress '127.0.0.1' -Port $port`.
    4. If already listening: `Write-Information "[install:hostadapter-start] HostAdapter already running on port $port; skipping start." -InformationAction Continue` and `return`.
    5. Build a `[System.Diagnostics.ProcessStartInfo]` with `FileName = $HostAdapterExePath`, `UseShellExecute = $false`, `WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden`, and `EnvironmentVariables["ASPNETCORE_URLS"] = $AspNetCoreUrls` — set environment variable on the `psi` object, not on the current process.
    6. If `$PSCmdlet.ShouldProcess($HostAdapterExePath, 'Start-HostAdapter')`: call `Invoke-HostAdapterProcess -ProcessStartInfo $psi`.
    7. `Write-Information "[install:hostadapter-start] HostAdapter process launched from '$HostAdapterExePath'." -InformationAction Continue`.
  - Acceptance: function is present, all logic branches are present, file remains ≤ 500 lines.

- [x] [P1-T4] In `scripts/Install.Helpers.psm1`, update the `Export-ModuleMember` call (line ~453–466) to include `'Test-TcpPortOpen'`, `'Invoke-HostAdapterProcess'`, and `'Invoke-HostAdapterStart'` in the exported function list.
  - Acceptance: all three names appear in the `Export-ModuleMember` call; the backtick-continuation style matches the existing pattern.

- [x] [P1-T5] In `scripts/Install.ps1`, insert Stage 7a block immediately before the existing Stage 7 comment block (the comment `# Stage 7 preflight: HostAdapter readiness guard...`):
  ```powershell
      # Stage 7a: launch HostAdapter from bundle if not already running.
      if (-not $SkipDocker) {
          Write-Information '[install:hostadapter-start] Ensuring HostAdapter is running' -InformationAction Continue
          $HostAdapterExePath = Join-Path $DestinationPath 'executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
          $hostAdapterUri = Get-HostAdapterPreflightUri -EnvMap (Get-InstallEnvFileMap -EnvFilePath (Join-Path $DestDockerDir '.env'))
          Invoke-HostAdapterStart `
              -HostAdapterExePath $HostAdapterExePath `
              -AspNetCoreUrls "$($hostAdapterUri.Scheme)://$($hostAdapterUri.Host):$($hostAdapterUri.Port)"
      }
  ```
  - Acceptance: block is present immediately before `# Stage 7 preflight:` comment, is guarded by `-not $SkipDocker`, file remains ≤ 500 lines.

- [x] [P1-T6] In `scripts/Install.ps1`, add a mock entry for `Invoke-HostAdapterStart` to the `BeforeEach` mock block in `tests/scripts/Install.Tests.ps1`:
  - `Mock Invoke-HostAdapterStart { [void]$global:InstallTestCalls.Add('Invoke-HostAdapterStart') }`
  - Verify that the existing Stage 7 mock for `Assert-HostAdapterRuntimePreflight` is already present in the mock block; if it is absent, add it: `Mock Assert-HostAdapterRuntimePreflight { [void]$global:InstallTestCalls.Add('Assert-HostAdapterRuntimePreflight') }`.
  - Acceptance: both mocks are present in the `BeforeEach` block; `Install.Tests.ps1` remains ≤ 500 lines. (New `Invoke-HostAdapterStart` unit tests go in the separate file per P1-T7 — not in this file.)

- [x] [P1-T7] Create `tests/scripts/Install.HostAdapterStart.Tests.ps1` with a `Describe 'Install.Helpers.psm1 — Invoke-HostAdapterStart'` block. The file must contain exactly these three `It` tests (each in its own `Context`):

  **Context `'exe not found'`**:
  - Arrange: mock `Test-Path` to return `$false` for any path containing `OpenClaw.HostAdapter.exe`.
  - Act: `{ Invoke-HostAdapterStart -HostAdapterExePath 'C:\missing\OpenClaw.HostAdapter.exe' -AspNetCoreUrls 'http://127.0.0.1:4319' }`.
  - Assert: `Should -Throw -ExpectedMessage "*HostAdapter executable not found at*"`.

  **Context `'already running'`**:
  - Arrange: mock `Test-Path` to return `$true` for `OpenClaw.HostAdapter.exe`; mock `Test-TcpPortOpen` to return `$true`; mock `Invoke-HostAdapterProcess` as a tracking mock.
  - Act: `Invoke-HostAdapterStart -HostAdapterExePath 'C:\fake\OpenClaw.HostAdapter.exe' -AspNetCoreUrls 'http://127.0.0.1:4319'`.
  - Assert: `Invoke-HostAdapterProcess` was NOT called (Should -Invoke count 0).

  **Context `'not running — launches process'`**:
  - Arrange: mock `Test-Path` to return `$true`; mock `Test-TcpPortOpen` to return `$false`; mock `Invoke-HostAdapterProcess` as a tracking mock.
  - Act: `Invoke-HostAdapterStart -HostAdapterExePath 'C:\fake\OpenClaw.HostAdapter.exe' -AspNetCoreUrls 'http://127.0.0.1:4319'`.
  - Assert: `Invoke-HostAdapterProcess` was called exactly once (Should -Invoke -Exactly 1).

  `BeforeAll` must import `scripts/Install.Helpers.psm1` with `-Force`. File must remain ≤ 500 lines.

---

### Phase 2 — Final QC Loop

- [x] [P2-T1] Run PoshQC formatter (`mcp__drmCopilotExtension__run_poshqc_format`). Record the result in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/final-format.2026-04-25T00-00.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. If the formatter changes any file, restart the QC loop from P2-T1.

- [x] [P2-T2] Run PoshQC analyzer (`mcp__drmCopilotExtension__run_poshqc_analyze`). Record the result in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/final-analyze.2026-04-25T00-00.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. If the analyzer reports any error or warning that is not suppressed by an existing policy suppression, fix it and restart the loop from P2-T1.

- [x] [P2-T3] Run PoshQC tests in coverage mode (`mcp__drmCopilotExtension__run_poshqc_test`). Record the result in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/final-test.2026-04-25T00-00.md` with fields: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (must include numeric line-coverage headline and confirm: overall coverage ≥ 80%, new function coverage ≥ 90%). If any test fails or coverage thresholds are not met, fix the issue and restart the loop from P2-T1. The loop is complete only when P2-T1, P2-T2, and P2-T3 all pass in a single uninterrupted pass.

- [x] [P2-T4] Verify all eight acceptance criteria from `issue.md § Acceptance Criteria` against implementation and test evidence. Record pass/fail per criterion in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/ac-verification.2026-04-25T00-00.md`. Any criterion marked fail requires a fix and a full restart of the Phase 2 loop from P2-T1.
