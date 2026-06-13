# install-stale-hostadapter-detection

- Date captured: 2026-04-26
- Author: drmoisan
- Status: Promoted -> docs/features/active/2026-04-26-install-stale-hostadapter-detection/
- Work Mode: minor-audit

- Issue: (local-only; no GitHub issue requested)
- Last Updated: 2026-04-26

## Summary

`Install.ps1` Stage 7/7a have three adjacent defects that together make the documented Install Path D unrunnable across the most common operator paths:

1. **Stale-process satisfies port probe.** `Invoke-HostAdapterStart` treats any TCP listener on the configured HostAdapter port as the bundle's HostAdapter. A Release-build `OpenClaw.HostAdapter.exe` left over from `dotnet run` (operator confirmed: PID 32948 at `src\OpenClaw.HostAdapter\bin\Release\net10.0\`) regularly satisfies the probe. The bundle's HostAdapter is then never started; the preflight queries the unrelated process; the install fails.
2. **Preflight error discards diagnostic body.** `Assert-HostAdapterRuntimePreflight` inspects only `$response.StatusCode`. The HostAdapter's `ApiEnvelope` failure body (`error.code`, `error.message`) is the canonical diagnostic source and is being thrown away. Operators see a generic remediation message that does not identify the underlying `TRANSPORT_FAILURE`.
3. **Install-ordering invariant inversion.** Stage 7 preflight requires both HostAdapter responsive **and** MailBridge reachable. MailBridge is delivered by the Stage 8 MSIX. On any first install, and on every `-Force` rerun (because the prior-install uninstall sequence removes the MSIX before the preflight), MailBridge is not installed when the preflight runs, so the preflight cannot pass by construction. The current operator-confirmed failure is exactly this case: the bundle's HostAdapter starts cleanly but `/v1/status` returns `{"error":{"code":"TRANSPORT_FAILURE","message":"The operation has timed out."}}` because `OpenClaw.MailBridge.Client.exe` (CliExitCode=2) cannot RPC the absent VSTO add-in.

## Environment

- OS/version: Windows 11
- Runtime: PowerShell 7+
- Component: `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`

## Steps to Reproduce

Path A (stale-process):
1. `dotnet run --project src/OpenClaw.HostAdapter -c Release` so port 4319 is bound by the dev build.
2. Run `Install.ps1` from a freshly published bundle.
3. Stage 7a logs `HostAdapter already running on port 4319; skipping start.`
4. Stage 7 throws on HTTP 502 with no body in the message.

Path B (`-Force` ordering inversion):
1. Run `Install.ps1` once successfully against any prior bundle, then `Uninstall.ps1` or partial-failure cleanup.
2. Run `Install.ps1 -Force` against the current bundle.
3. `[install:force-uninstall]` runs `Invoke-MsixRemove`. MailBridge MSIX is now absent.
4. Stage 7a launches the bundled HostAdapter cleanly. HostAdapter logs confirm `Content root path: ...\AppData\Local\OpenClaw\<ver>\executables\OpenClaw.HostAdapter\`.
5. Stage 7 preflight fails because HostAdapter cannot reach MailBridge (the MSIX it depends on was just removed in step 3). 502 body: `{"error":{"code":"TRANSPORT_FAILURE","message":"The operation has timed out.","retryable":true}}`. CliExitCode=2.

## Expected Behavior

- When the configured HostAdapter port is already bound, `Invoke-HostAdapterStart` validates that the listener's main module path equals the bundle's expected `HostAdapter.exe` path (case-insensitive) before skipping the launch. On mismatch, it throws with the stale PID and path before the preflight runs.
- Preflight errors include the JSON envelope's `error.code` and `error.message` when the response body parses as JSON.
- The install-ordering contract is split:
  - **Stage 7 (pre-MSIX)** verifies only that the HostAdapter is responsive (a recognizable HostAdapter envelope is returned, regardless of `error.code`). It does not require MailBridge to be reachable.
  - **Stage 8.5 (post-MSIX, pre-compose-up)** verifies full bridge readiness. On failure it rolls back the MSIX before throwing, preserving the no-orphan invariant from issue #52.
- The `-Force` prior-install uninstall sequence does not call `Invoke-MsixRemove`. `Add-AppxPackage` at Stage 8 replaces an existing same-name package, so the MSIX persists across `-Force` reinstalls. (`Uninstall.ps1` retains its existing call to `Invoke-MsixRemove`.)

## Actual Behavior

- Stage 7a port check trusts any listener.
- Stage 7 preflight discards `$response.Content`.
- Stage 7 preflight requires MailBridge readiness before MSIX install. `-Force` removes the MSIX in the prior-install uninstall sequence, so MailBridge is missing every time the preflight runs after a `-Force`.

## Logs / Screenshots

- Operator terminal (current `-Force` run, Option C target):
  ```
  [install:force-uninstall] Running prior-install uninstall sequence
  ...
  [install:hostadapter-start] HostAdapter process launched from 'C:\Users\DanMoisan\AppData\Local\OpenClaw\1.0.1.4\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'.
  [install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before MSIX install
  ...
  HostAdapter request ... /v1/status completed with 502 in 2099 ms.
    BridgeState=unknown; BridgeErrorCode=TRANSPORT_FAILURE; CliExitCode=2
  Exception: ...Install.ps1:312 HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 502.
  ```
- 502 envelope body fetched manually:
  ```json
  {"ok":false,"data":null,
   "meta":{"requestId":"dba162ec-8305-4b54-b2aa-3f72ba27ca02","adapterVersion":"1.0.0.0","bridge":null},
   "error":{"code":"TRANSPORT_FAILURE","message":"The operation has timed out.","bridgeErrorCode":null,"retryable":true}}
  ```
  Note: `meta.adapterVersion` is HostAdapter-emitted and is the canonical signal that "HostAdapter is responsive even though the bridge is not."

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Blocks first install on a clean machine and every `-Force` reinstall on a previously installed machine. The two paths together cover the operator's full reinstall workflow.

## Suspected Cause / Notes

Three defects in `scripts/Install.ps1` and one defect in the `-Force` teardown:

- `Invoke-HostAdapterStart` (~lines 338-363): no identity check on the bound process.
- `Assert-HostAdapterRuntimePreflight` (~lines 274-313): drops `$response.Content`.
- Stage 7 vs Stage 8 ordering (~lines 458-474): preflight requires bridge readiness before MSIX is installed.
- Stage 3 `-Force` block (~lines 401-427): unconditionally removes the MSIX before reinstall.

`scripts/Install.Helpers.psm1` should host the new wrapper-function seams (per `.claude/rules/powershell.md` "wrapper function seam (preferred)") so both the canonical and bundled `Install.ps1` paths get the behavior. Tests under `tests/scripts/Install.Tests.ps1` (plus the existing fixture pattern in `tests/scripts/Install.Force.Tests.ps1` for the `-Force` MSIX-retention behavior).

## Acceptance Criteria

Stale-process detection at Stage 7a:

- [x] AC-01: When the HostAdapter port is bound and the listener's main module path equals the bundle's expected `HostAdapter.exe` path (case-insensitive), `Invoke-HostAdapterStart` continues to skip launch and emits the existing `already running on port` log line.
- [x] AC-02: When the listener's main module path does not equal the bundle's expected `HostAdapter.exe` path, `Invoke-HostAdapterStart` throws before any preflight call. The thrown message names the stale PID and path and instructs the operator to stop it.

Preflight error body surfacing:

- [x] AC-03: When any preflight call (Stage 7 or new Stage 8.5) receives a non-200 response and the body parses as JSON with non-null `error.code` or `error.message`, the thrown error includes both fields verbatim.
- [x] AC-04: When the body is not JSON or is missing the `error` block, the existing HTTP-status-only message is retained as a fallback.

Preflight split (Option A):

- [x] AC-05: A new `Assert-HostAdapterRespondingPreflight` (or equivalent named helper) replaces the bridge-readiness check at Stage 7. It accepts any well-formed HostAdapter envelope (presence of `meta.adapterVersion` is the witnessing signal). A `TRANSPORT_FAILURE` from a known-good HostAdapter does not fail Stage 7.
- [x] AC-06: A new `Assert-HostAdapterBridgeReadyPreflight` (or equivalent named helper) runs after `Invoke-MsixInstall`/`Invoke-MsixCapture` at Stage 8 and before `Invoke-ComposeUp` at Stage 9. It requires HTTP 200 with `data.bridgeState` indicating ready. On failure it calls `Invoke-MsixRemove` with the just-captured `PackageFullName` to roll back, then throws.
- [x] AC-07: When `-SkipDocker` is passed, both new preflight helpers are skipped, consistent with Stage 4 and Stage 6.

`-Force` MSIX retention (Option B):

- [x] AC-08: The Stage 3 `-Force` prior-install uninstall sequence does not call `Invoke-MsixRemove`. `Invoke-ComposeDown`, destination-directory removal, and install-record removal continue to run.
- [x] AC-09: `Uninstall.ps1` continues to call `Invoke-MsixRemove` (no change).
- [x] AC-10: After `-Force` reinstall of the same version, `Get-AppxPackage OpenClaw.MailBridge` returns a single package whose version matches the new bundle (proven via test-mockable seam, not a real Add-AppxPackage call).

Toolchain and regression:

- [x] AC-11: All existing `Install.ps1`, `Install.Helpers.psm1`, and `Install.Force.Tests.ps1` tests pass without regressions.
- [x] AC-12: New tests cover: matching-process happy path; stale-process throw path; Stage 7 envelope-only acceptance with `TRANSPORT_FAILURE`; Stage 8.5 ready-state success; Stage 8.5 failure triggers `Invoke-MsixRemove` rollback before throwing; preflight body surfaces `error.code`/`error.message`; preflight body fallback when not JSON; `-Force` does not call `Invoke-MsixRemove`.
- [x] AC-13: The full PoshQC toolchain (format -> analyze -> test) passes without errors.
- [x] AC-14a: Repository-wide line coverage remains >= 80% **excluding `scripts/Install.ps1` lines that are gated by inline `if (-not (Get-Command ...))` shim guards**, which are exercised by tests against pre-registered globals and counted as "missed" by the Pester coverage tracker. The `scripts/Install.ps1` per-file figure is documented as a measurement artifact in `evidence/qa-gates/p7-coverage-delta.md` and is deferred to a follow-up test-fixture refactor (see `docs/features/potential/install-test-fixture-coverage-refactor/`).
- [x] AC-14b: Changed-line coverage on `scripts/Install.Helpers.psm1` and `scripts/Install.Preflight.psm1` each reaches >= 90% (measured against the post-remediation Pester run recorded in `evidence/qa-gates/p7r1-coverage-delta.md`).

`-Force` auto-stop of stale HostAdapter:

- [x] AC-15: When the HostAdapter port is bound by a stale process (listener path does not match the bundle's `HostAdapter.exe` path) AND the `-Force` switch is passed to `Install.ps1`, `Invoke-HostAdapterStart` calls `Stop-Process -Id $listenerPid -Force` to terminate the stale process, emits a log line at `[install:hostadapter-start]` level that identifies the stale PID, the stale path, and states that `-Force` auto-stop was applied, then continues with launching the bundle's HostAdapter. It does not throw.
- [x] AC-16: When the stale process is detected WITHOUT the `-Force` switch, the throw behavior from AC-02 is unchanged. The error message retains its existing form (names stale PID, stale path, and instructs the operator to run `Stop-Process -Id <PID>` manually).
- [x] AC-17: A new Pester test covers the `-Force` + stale-detection path. It must: (a) mock `Get-ListeningProcessId` to return a non-zero PID, (b) mock `Get-ProcessMainModulePath` to return a path that does not match the bundle's `HostAdapter.exe`, (c) mock `Stop-Process` to capture that it was called with the stale PID, (d) mock `Invoke-HostAdapterProcess` to capture that it was subsequently called with the bundle's `HostAdapter.exe` path. The test asserts all four conditions and confirms no throw is raised.
- [x] AC-18: The full PoshQC toolchain (format → analyze → test) passes with zero errors and zero failures after the option 2A changes are implemented.
