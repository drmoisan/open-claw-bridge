# install-hostadapter-not-started (Issue #59)

- Date captured: 2026-04-25
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-04-25-install-hostadapter-not-started-59/ (Issue #59)
- Work Mode: minor-audit

- Issue: #59
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/59
- Last Updated: 2026-04-25

## Summary

`Install.ps1` never starts the HostAdapter process. The installer checks HostAdapter readiness at Stage 7 (before MSIX install, per the fix for issue #52), but provides no step to launch it. The bundle includes `executables/OpenClaw.HostAdapter/OpenClaw.HostAdapter.exe` as a self-contained executable, but `Install.ps1` never invokes it. On any system where HostAdapter is not already running — including a fresh install — Stage 7 always fails with "HostAdapter preflight failed before starting Docker", making the full Docker install path non-functional.

## Environment

- OS/version: Windows 11
- Runtime: PowerShell 7+
- Component: `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`

## Steps to Reproduce

1. Publish a bundle: `scripts/Publish.ps1 -Version 1.0.1.3 -SkipSign`
2. Ensure HostAdapter is not running on the machine.
3. Run `Install.ps1` with Docker enabled (no `-SkipDocker`).
4. Observe: installer reaches `[install:hostadapter-check]` at Stage 7.
5. Observe: Stage 7 throws `HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status was unreachable`.
6. Result: MSIX is never installed; Docker compose is never started. Installation cannot complete.

## Expected Behavior

`Install.ps1` should start HostAdapter from the bundle's executables directory before the Stage 7 preflight check if HostAdapter is not already running. After the start, the existing preflight check verifies HostAdapter is responsive. Installation proceeds normally.

## Actual Behavior

The installer never starts HostAdapter. Stage 7 preflight always fails when HostAdapter is not already running. The install path documented in the runbook (`Install Path D: Scripted Bundle Install`) is non-functional without a separate, undocumented manual step to start HostAdapter.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet:
  ```
  [install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before MSIX install
  Exception: ...Install.ps1:308
  | HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status was unreachable:
  | No connection could be made because the target machine actively refused it. (127.0.0.1:4319).
  | Start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage.
  ```

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

## Suspected Cause / Notes

Root cause: `Install.ps1` was designed to check HostAdapter readiness before MSIX install (Stage 7, per issue #52 fix). However, no stage was added to start HostAdapter. After Stage 5 (bundle copy), the executable is available at `$DestinationPath\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe`. The installer should start this process with the appropriate `ASPNETCORE_URLS` before running the Stage 7 preflight.

Key files: `scripts/Install.ps1` (Stage 7), `scripts/Install.Helpers.psm1` (helper functions), `tests/scripts/Install.Tests.ps1`.

## Acceptance Criteria

- [x] When HostAdapter is not running before the installer starts, `Install.ps1` launches the HostAdapter executable from `$DestinationPath\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe` before the Stage 7 preflight check.
- [x] When HostAdapter is already running (something is already responding on the configured port), the installer does NOT start a second instance — it proceeds directly to the preflight check.
- [x] The HostAdapter start step is skipped when `-SkipDocker` is passed, consistent with all other Docker-path guards.
- [x] If the HostAdapter executable is not found at the expected path, the installer throws a clear error before attempting the preflight.
- [x] The existing Stage 7 preflight check (`Assert-HostAdapterRuntimePreflight`) continues to verify HostAdapter readiness after the start step.
- [x] All existing `Install.ps1` and `Install.Helpers.psm1` tests pass without regressions.
- [x] New tests cover: already-running skip path, not-running launch path, exe-not-found error path.
- [x] The full PoshQC toolchain (format → analyze → test) passes without errors.
