---
Timestamp: 2026-04-25T22:10:00Z
Source: docs/features/active/2026-04-25-install-hostadapter-not-started-59/issue.md § Acceptance Criteria
---

## Acceptance Criteria Verification

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| AC1 | When HostAdapter is not running, `Install.ps1` launches `$DestinationPath\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe` before Stage 7 preflight | PASS | Stage 7a block in `Install.ps1` (lines ~452–458) calls `Invoke-HostAdapterStart` with the exe path resolved via `Join-Path $DestinationPath 'executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'`. `Invoke-HostAdapterStart` checks `Test-TcpPortOpen` and only calls `Invoke-HostAdapterProcess` when port is not listening. Test: "not running — launches process" in `Install.HostAdapterStart.Tests.ps1`. |
| AC2 | When HostAdapter IS already running, installer does NOT start a second instance | PASS | `Invoke-HostAdapterStart` calls `Test-TcpPortOpen -IpAddress '127.0.0.1' -Port $port`; if `$true`, logs "already running" and returns immediately without calling `Invoke-HostAdapterProcess`. Test: "already running" in `Install.HostAdapterStart.Tests.ps1` — `Should -Invoke Invoke-HostAdapterProcess -Times 0`. |
| AC3 | HostAdapter start step is skipped when `-SkipDocker` is passed | PASS | Stage 7a block is wrapped in `if (-not $SkipDocker)` (consistent with Stage 4, Stage 7, Stage 9 guards). Tests: `-SkipDocker path` context in `Install.Tests.ps1` all pass; `Invoke-HostAdapterStart` not called when `-SkipDocker`. |
| AC4 | If HostAdapter exe is not found at expected path, installer throws clear error before preflight | PASS | `Invoke-HostAdapterStart` checks `Test-Path -LiteralPath $HostAdapterExePath`; throws "HostAdapter executable not found at '$HostAdapterExePath'. The bundle may be incomplete or the destination copy did not complete." Test: "exe not found" in `Install.HostAdapterStart.Tests.ps1`. |
| AC5 | Existing Stage 7 preflight check (`Assert-HostAdapterRuntimePreflight`) continues to run after the start step | PASS | Stage 7 block is unchanged and follows Stage 7a in `Install.ps1`. Stage ordering test in `Install.Tests.ps1` verifies `Invoke-HostAdapterStart` appears before `Invoke-WebRequest` (called inside `Assert-HostAdapterRuntimePreflight`). |
| AC6 | All existing `Install.ps1` and `Install.Helpers.psm1` tests pass without regressions | PASS | 159 pre-existing tests all pass. Test run: 189 total passed (159 pre-existing + 30 new), 0 failed. |
| AC7 | New tests cover: already-running skip path, not-running launch path, exe-not-found error path | PASS | `tests/scripts/Install.HostAdapterStart.Tests.ps1` contains exactly these three tests, all passing. |
| AC8 | Full PoshQC toolchain (format → analyze → test) passes without errors | PASS | Format: all 4 files CLEAN. Analyze: 0 findings. Test: 189 passed, 0 failed, 92.6% coverage. |

All 8 acceptance criteria: PASS.
