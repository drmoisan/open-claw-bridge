# install-stale-hostadapter-detection

- Date captured: 2026-04-26
- Author: drmoisan
- Status: Potential
- Work Mode: minor-audit

## Summary

`Install.ps1` Stage 7a treats any TCP listener on the configured HostAdapter port as the bundle's HostAdapter and skips launching a fresh instance. On developer machines a Release-build `OpenClaw.HostAdapter.exe` from `src\OpenClaw.HostAdapter\bin\Release\net10.0\` (or any prior unrelated HostAdapter process) regularly satisfies the TCP probe. The bundle's HostAdapter is then never started, and the Stage 7 preflight at `Assert-HostAdapterRuntimePreflight` calls `/v1/status` against the stale process, which returns `HTTP 502` because it cannot resolve `OpenClaw.MailBridge.Client.exe` from its own `AppContext.BaseDirectory`. The installer surfaces only the HTTP status code and discards the JSON envelope body, so operators see a generic remediation message that does not identify the underlying `TRANSPORT_FAILURE`.

## Environment

- OS/version: Windows 11
- Runtime: PowerShell 7+
- Component: `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`

## Steps to Reproduce

1. Build and run `OpenClaw.HostAdapter` from the repo (`dotnet run --project src/OpenClaw.HostAdapter -c Release`) so port 4319 is bound by `src\OpenClaw.HostAdapter\bin\Release\net10.0\OpenClaw.HostAdapter.exe`.
2. Publish a bundle: `scripts/Publish.ps1 -Version <ver> -SkipSign`.
3. Run `Install.ps1` with Docker enabled (no `-SkipDocker`).
4. Observe Stage 7a print `[install:hostadapter-start] HostAdapter already running on port 4319; skipping start.`
5. Observe Stage 7 throw `HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 502.`
6. Confirm the bound process via `Get-NetTCPConnection -LocalPort 4319 -State Listen | Select-Object OwningProcess` and `Get-Process` — `Path` is the dev-build, not the bundle's HostAdapter.

## Expected Behavior

- When `Test-TcpPortOpen` reports the configured HostAdapter port is bound, the installer must verify the bound process's main module path matches the bundle's HostAdapter executable. When the bound process is unrelated, the installer must throw a precise error naming the stale process path and PID before any preflight call.
- When `Assert-HostAdapterRuntimePreflight` receives a non-200 response from `/v1/status`, the resulting error must include the `error.code` and `error.message` fields from the JSON envelope body so the underlying cause (`TRANSPORT_FAILURE`, `BRIDGE_NOT_READY`, etc.) is visible without reading HostAdapter source.

## Actual Behavior

- The Stage 7a port check trusts any listener on the configured port and skips the bundle launch.
- The Stage 7 preflight discards the response body and surfaces only the HTTP status code, which masks the JSON-encoded reason returned by HostAdapter.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet:
  ```
  [install:hostadapter-start] HostAdapter already running on port 4319; skipping start.
  [install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before MSIX install
  Exception: ...Install.ps1:312
  | HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 502.
  ```

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Blocks the documented Install Path D on any machine that has a developer build of HostAdapter still listening (a routine state during repo work).

## Suspected Cause / Notes

Two adjacent defects in the Stage 7/7a path of `scripts/Install.ps1`:

1. `Invoke-HostAdapterStart` in `scripts/Install.ps1` (lines ~338-363) treats `Test-TcpPortOpen` as a sufficient identity check for "the HostAdapter is already running". `Get-NetTCPConnection -LocalPort <port> -State Listen | Select-Object OwningProcess` plus `Get-Process` provide the missing identity comparison against `$HostAdapterExePath`.
2. `Assert-HostAdapterRuntimePreflight` in `scripts/Install.ps1` (lines ~274-313) only inspects `$response.StatusCode`. It does not parse `$response.Content`. The HostAdapter's `HostAdapterResponses.Failure` envelope (`error.code`, `error.message`) is the canonical diagnostic source and is being discarded.

Helpers should land in `scripts/Install.Helpers.psm1` so both the canonical and bundled `Install.ps1` paths get the behavior. Tests in `tests/scripts/Install.Tests.ps1` (and the existing fixture pattern there).

## Acceptance Criteria

- [ ] When `Test-TcpPortOpen` reports the HostAdapter port is bound and the bound process's main module path equals the bundle's expected `HostAdapter.exe` path (case-insensitive comparison), the installer continues to skip launch and emits the existing `already running on port` log.
- [ ] When the bound process's main module path does not equal the bundle's expected `HostAdapter.exe` path, the installer throws before calling the preflight, with a message that names the stale process path and PID and instructs the operator to stop it.
- [ ] When `Assert-HostAdapterRuntimePreflight` receives a non-200 response, the thrown error includes the JSON envelope's `error.code` and `error.message` when present. When the body is not JSON or is missing those fields, the existing HTTP-status-only message is retained.
- [ ] When the response body is JSON without an `error` block (for example a 200 with unexpected shape), the existing remediation text continues to apply.
- [ ] All existing `Install.ps1` and `Install.Helpers.psm1` tests pass without regressions.
- [ ] New tests cover: matching-process happy path, stale-process throw path, 502-with-error-envelope surfacing, 502-without-JSON-body fallback.
- [ ] The full PoshQC toolchain (format → analyze → test) passes without errors.
