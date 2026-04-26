# install-hostadapter-preflight-ordering (Issue #52)

- Date captured: 2026-04-25
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-04-25-install-hostadapter-preflight-ordering-52/ (Issue #52)

- Issue: #52
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/52
- Last Updated: 2026-04-25
- Work Mode: minor-audit

## Summary

`Install.ps1` calls `Assert-HostAdapterRuntimePreflight` at Stage 8, after the MSIX has already been installed at Stage 7. When the preflight check fails (HostAdapter not running), the MSIX remains installed but no `install-record.json` is written, leaving the system in a partial state that requires manual cleanup and cannot be cleanly uninstalled via `Uninstall.ps1`.

## Environment

- OS/version: Windows 11
- Runtime: PowerShell 7+
- Component: `scripts/Install.ps1`

## Steps to Reproduce

1. Run `Install.ps1` with Docker enabled (no `-SkipDocker`) when `OpenClaw.HostAdapter` and `OpenClaw.MailBridge` are not running.
2. Observe: MSIX installs successfully at Stage 7.
3. Observe: Stage 8 preflight throws `HostAdapter preflight failed before starting Docker`.
4. Check system: MSIX is installed (`Get-AppxPackage OpenClaw.MailBridge` returns a result).
5. Check system: `%LOCALAPPDATA%\OpenClaw\install-record.json` does NOT exist.
6. Result: `Uninstall.ps1` reports nothing to uninstall; operator must manually remove the MSIX package.

## Expected Behavior

The HostAdapter readiness preflight must run before any state-changing operations (MSIX install). If the preflight fails, no MSIX is installed and the system is left in a clean, unmodified state.

## Actual Behavior

The HostAdapter preflight runs after MSIX install, leaving an orphaned MSIX package installed on the host with no install record to support automated cleanup.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet:
  ```
  [install:msix] Installing MSIX ...
  [install:msix] PackageFullName OpenClaw.MailBridge_1.0.1.2_x64__124xeds558nzw
  [install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before compose up
  Exception: ... HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status was unreachable ...
  ```

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

## Suspected Cause / Notes

Root cause: `Assert-HostAdapterRuntimePreflight` is positioned inside the Stage 8 compose-up block (line ~416 in `scripts/Install.ps1`), after `Invoke-MsixInstall` and `Invoke-MsixCapture` at Stage 7. The pattern of all other pre-state-change guards (Docker readiness at Stage 4, gateway token guard at Stage 6) placing checks before side effects is not followed for the HostAdapter preflight.

Key files: `scripts/Install.ps1` (Stage 7-8 ordering), `tests/scripts/Install.Tests.ps1` (existing test assertion documents broken behavior with `Should -BeTrue` on `Invoke-MsixInstall` after failed preflight).

## Acceptance Criteria

- [x] When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-MsixInstall` must NOT have been called (MSIX left clean).
- [x] When `Assert-HostAdapterRuntimePreflight` fails, `Invoke-ComposeUp` must NOT be called.
- [x] When `Assert-HostAdapterRuntimePreflight` fails, `Wait-ComposeHealthy` must NOT be called.
- [x] The happy-path stage ordering test passes with `Assert-HostAdapterRuntimePreflight` (or its underlying `Invoke-WebRequest`) confirmed to execute before `Invoke-MsixInstall`.
- [x] All existing Install.ps1 tests pass (no regressions).
- [x] The full PoshQC toolchain (format → analyze → test) passes without errors.
