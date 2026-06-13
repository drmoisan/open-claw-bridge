# Feature Audit — install-hostadapter-not-started-59

- Artifact type: feature-audit
- Timestamp: 2026-04-26T02-16
- Branch: bug/install-hostadapter-not-started-59
- Merge base: d2d13b3853538f697d0daadb75b67260019f0abe
- Base branch: development
- Work mode: minor-audit
- AC source: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/issue.md` § Acceptance Criteria

---

## Acceptance Criteria Evaluation

AC source is `issue.md § Acceptance Criteria` (8 items, minor-audit mode).

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| AC1 | When HostAdapter is not running before the installer starts, `Install.ps1` launches `$DestinationPath\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe` before the Stage 7 preflight check. | PASS | Stage 7a block in `Install.ps1` (lines 451–457) calls `Invoke-HostAdapterStart` with `$HostAdapterExePath = Join-Path $DestinationPath 'executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'`. The block is positioned immediately before the Stage 7 preflight (`Assert-HostAdapterRuntimePreflight`). Stage ordering test in `Install.Tests.ps1` (stage-ordering context, lines 169–188) verifies `Invoke-HostAdapterStart` appears before `Invoke-WebRequest` in the call log. |
| AC2 | When HostAdapter IS already running (something is already responding on the configured port), the installer does NOT start a second instance — it proceeds directly to the preflight check. | PASS | `Invoke-HostAdapterStart` calls `Test-TcpPortOpen -IpAddress '127.0.0.1' -Port $port`; if `$true`, logs and returns immediately without calling `Invoke-HostAdapterProcess`. Test "does NOT call Invoke-HostAdapterProcess when port is already listening" in `Install.HostAdapterStart.Tests.ps1` asserts `Should -Invoke -CommandName Invoke-HostAdapterProcess -Times 0 -Exactly`. |
| AC3 | The HostAdapter start step is skipped when `-SkipDocker` is passed, consistent with all other Docker-path guards. | PASS | Stage 7a block is wrapped in `if (-not $SkipDocker)` (line 452). Test `-SkipDocker path` context in `Install.Tests.ps1` (line 259–270) confirms installer completes with MSIX installed when `-SkipDocker` is supplied and `Invoke-HostAdapterStart` is not invoked (it is absent from the global call log for those tests). |
| AC4 | If the HostAdapter executable is not found at the expected path, the installer throws a clear error before attempting the preflight. | PASS | `Invoke-HostAdapterStart` throws `"HostAdapter executable not found at '$HostAdapterExePath'. The bundle may be incomplete or the destination copy did not complete."` when `Test-Path` returns `$false`. Test "throws with message containing HostAdapter executable not found at" in `Install.HostAdapterStart.Tests.ps1` asserts `Should -Throw -ExpectedMessage '*HostAdapter executable not found at*'`. |
| AC5 | The existing Stage 7 preflight check (`Assert-HostAdapterRuntimePreflight`) continues to verify HostAdapter readiness after the start step. | PASS | Stage 7 block (lines 458–465) is unchanged and follows Stage 7a in `Install.ps1`. `Assert-HostAdapterRuntimePreflight` is still called when `-SkipDocker` is not set. Stage ordering test verifies `Invoke-HostAdapterStart` appears before `Invoke-WebRequest` (called inside `Assert-HostAdapterRuntimePreflight`). |
| AC6 | All existing `Install.ps1` and `Install.Helpers.psm1` tests pass without regressions. | PASS | Final QC run: 189 tests total, 0 failed. Baseline was 159 tests. No pre-existing test was removed or weakened. Evidence: `qa-gates/final-test.2026-04-25T00-00.md`. |
| AC7 | New tests cover: already-running skip path, not-running launch path, exe-not-found error path. | PASS | `tests/scripts/Install.HostAdapterStart.Tests.ps1` contains exactly these three `It` blocks under `Context 'already running'`, `Context 'not running — launches process'`, and `Context 'exe not found'`. All three pass per final QC run. |
| AC8 | The full PoshQC toolchain (format → analyze → test) passes without errors. | PASS | Format: all 4 touched files CLEAN (exit 0). Analyze: zero PSScriptAnalyzer findings (exit 0). Test: 189 passed, 0 failed, coverage 92.6% overall, above 80% threshold and 90% new-code threshold. Evidence: `qa-gates/final-format`, `qa-gates/final-analyze`, `qa-gates/final-test`. |

---

## Independent Reviewer Observations

The following observations are supplementary to the AC verdicts and reflect independent reviewer analysis.

**AC7 test scope note:** `Install.HostAdapterStart.Tests.ps1` tests `Invoke-HostAdapterStart` as defined in `Install.ps1` (dot-sourced in `BeforeAll`). This is functional but note that the ac-verification artifact in evidence states the function is in `Install.Helpers.psm1`, which is not accurate — the function is in `Install.ps1`. The AC criterion itself ("new tests cover: already-running skip path...") is met regardless of which file hosts the function, so the verdict remains PASS.

**AC3 verification depth:** The `Install.Tests.ps1` mocks `Invoke-HostAdapterStart` as a global function override; its absence from `InstallTestCalls` in `-SkipDocker` tests is confirmed at line 109 (`Remove-Item -Path 'Function:\global:Invoke-HostAdapterStart'`) and by the global function only adding to the call log when invoked. The stage-ordering test at lines 169–188 includes `Invoke-HostAdapterStart` in the expected sequence, confirming it is called on the non-SkipDocker path.

---

## AC Check-Off Actions

All 8 AC items are already marked `[x]` in `issue.md` by the executor (confirmed by reading the file). All 8 items were evaluated PASS in this review. No items were downgraded; no additional check-offs are required.

---

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/issue.md` § Acceptance Criteria
- Total AC items: 8
- Checked off (delivered): 8
- Remaining (unchecked): 0
- Items remaining: none

---

## Overall Feature Audit Verdict

**PASS** — all 8 acceptance criteria are met by the implementation and test evidence.

Two issues identified during review affect code quality and policy compliance but do not invalidate AC delivery:

1. `tests/scripts/Install.Tests.ps1` is 506 lines (6 over the 500-line limit) — policy violation, requires remediation.
2. New functions are placed in `Install.ps1` rather than `Install.Helpers.psm1` — plan deviation, medium severity.

These are captured in `code-review.2026-04-26T02-16.md` and `remediation-inputs.2026-04-26T02-16.md`.
