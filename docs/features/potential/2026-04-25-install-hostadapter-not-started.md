# install-hostadapter-not-started (Potential Bug)

- Date captured: 2026-04-25
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-04-25-install-hostadapter-not-started-59/ (Issue #59)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

## Summary

`Install.ps1` never starts the HostAdapter process. The installer checks HostAdapter readiness at Stage 7 (before MSIX install, per the fix for issue #52), but provides no step to launch it. The bundle includes `executables/OpenClaw.HostAdapter/OpenClaw.HostAdapter.exe` as a self-contained executable, but Install.ps1 never invokes it. On any system where HostAdapter is not already running — including a fresh install — Stage 7 always fails with "HostAdapter preflight failed before starting Docker", making the full Docker install path non-functional.

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

Root cause: `Install.ps1` was designed to check HostAdapter readiness before MSIX install (Stage 7), following the fix for issue #52. However, no stage was added to start HostAdapter. The bundle includes `executables/OpenClaw.HostAdapter/OpenClaw.HostAdapter.exe` as a self-contained executable (published via `--self-contained true -r win-x64`), but the installer never launches it. After Stage 5 (bundle copy), the executable is available at `$DestinationPath\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe`. The installer should start this process with the appropriate `ASPNETCORE_URLS` before running the Stage 7 preflight.

Key files: `scripts/Install.ps1` (Stage 7 ordering), `scripts/Install.Helpers.psm1` (helper functions), `tests/scripts/Install.Tests.ps1`.

## Proposed Fix / Validation Ideas

- Add `Invoke-HostAdapterStart` to `Install.Helpers.psm1`: checks if something is already listening on the HostAdapter port; if not, launches `$DestinationPath\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe` with `ASPNETCORE_URLS` set to the host-side URI derived from the installed `.env`.
- Add a new Stage 7a in `Install.ps1` (inside the `if (-not $SkipDocker)` block) to call `Invoke-HostAdapterStart` before the existing Stage 7 preflight check.
- Update `Install.Tests.ps1` with tests for `Invoke-HostAdapterStart`: already-running skip path, not-running launch path, and exe-not-found error path.
- HostAdapter is skipped (not started) when `-SkipDocker` is passed, consistent with all other Docker-path guards.
