# Spec — Install MSIX First-Launch Activation (Issue #62)

- Work Mode: full-bug
- Issue: #62
- Created: 2026-04-30
- Status: Draft (pending atomic-planner refinement and option-selection)

## Background

Stage 8.5 of `Install.ps1` (`[install:hostadapter-bridge-check]`) is the post-MSIX
bridge-ready preflight introduced by issue #52 to enforce the no-orphan invariant. It
requires HostAdapter `/v1/status` to return HTTP 200 with `data.state` outside the
not-ready set (`starting`, `waiting_for_outlook`) before the install proceeds to Stage 9
(compose up).

The failure observed against bundle 1.0.1.9 is HTTP 502 with
`error.code=TRANSPORT_FAILURE` and `error.message=The operation has timed out.`. This is
distinct from the not-ready states defined in
`src/OpenClaw.HostAdapter/Program.cs:444` (`IsBridgeNotReady`). The HostAdapter is
responsive but its CLI invocation against MailBridge times out because MailBridge is not
running.

The MSIX manifest at `installer/Package.appxmanifest` declares MailBridge as a
`uap5:Extension Category="windows.startupTask"` only:

```xml
<uap5:Extension Category="windows.startupTask"
                Executable="bridge\OpenClaw.MailBridge.exe"
                EntryPoint="Windows.FullTrustApplication">
  <uap5:StartupTask TaskId="OpenClawMailBridge"
                    Enabled="true"
                    DisplayName="OpenClaw MailBridge" />
</uap5:Extension>
```

`windows.startupTask` activates only at user logon. `Add-AppxPackage` registers the
package and exits without launching MailBridge. There is no current step in
`Install.ps1` that activates MailBridge after the MSIX install, so Stage 8.5's preflight
runs against a non-running process and fails.

## Objectives

1. Resolve the install-time defect by ensuring MailBridge is reachable at Stage 8.5
   under a clean install on a Windows host where MailBridge was not previously running.
2. Preserve the steady-state behavior: at user logon, MailBridge auto-starts via the
   existing or replacement startup mechanism.
3. Preserve the no-orphan invariant from issue #52.
4. Maintain or improve test coverage for the changed code paths in PowerShell, C#, and
   manifest-assertion suites.

## Non-Objectives

- HostAdapter behavior changes beyond what is required to consume the new activation
  contract.
- Docker / compose stack readiness changes (Stage 9 is unaffected).
- Changes to the operator-supplied `.env` files or the gateway-token contract.

## Selected Activation Pattern

**Selected: Option 3a — `windows.protocol` activation, retain `windows.startupTask`.**

Selection recorded by atomic-planner in
`docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/plan.2026-04-30T00-00.md`
(section "Activation-Pattern Selection"). This spec section is updated to record the
chosen option and its rationale per the planning hand-off.

A `<uap:Extension Category="windows.protocol">` declaring `<uap:Protocol
Name="openclaw-mailbridge"/>` is added as a sibling of the existing
`<uap5:Extension Category="windows.startupTask">` element. The installer activates the
package with `Start-Process 'openclaw-mailbridge:firstrun'`; the URI fragment `firstrun`
is reserved for the install path.

Rationale:

- **Cross-session correctness.** Protocol activation is performed by the shell broker,
  which resolves the registered package identity and launches the executable in the
  operator's interactive desktop session. It works from elevated PowerShell because the
  broker (not the calling process) routes the session token. This avoids the cross-session
  pitfalls of `Start-Process shell:AppsFolder\...` and the `IApplicationActivationManager`
  COM seam.
- **Additive manifest change.** The `windows.startupTask` element is retained verbatim;
  steady-state startup at logon is unchanged. The protocol extension only fires on
  explicit `Start-Process` invocation by the installer.
- **Retained `windows.startupTask`.** The protocol extension is purely additive; no
  re-thinking of the logon-startup story is required.

### Options not selected

- **Option 3b — `windows.appExecutionAlias`.** Rejected. Adds an alias to PATH for all
  consoles (broader operator-visible side effect than required) with non-zero collision
  risk and known inconsistencies when invoked from elevated PowerShell.
- **Option 3c — replace `windows.startupTask` with `windows.fullTrustProcess` or
  equivalent.** Rejected. Largest manifest change; loses logon-startup behavior; provides
  no advantage over Option 3a for the install-time defect under repair.

## High-Level Design (post option-selection)

Regardless of which option is selected, the implementation includes:

### Manifest changes

- `installer/Package.appxmanifest` updated for the chosen activation pattern.
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` updated to assert the new
  contract, with existing `windows.startupTask` assertions retained or replaced
  according to the selected option.

### PowerShell changes

- `scripts/Install.Helpers.psm1`: new wrapper seam for the launch step. Final signature
  (Option 3a — protocol activation; planner-selected):

  ```powershell
  function Invoke-MsixAppActivate {
      [CmdletBinding(SupportsShouldProcess = $true)]
      [OutputType([void])]
      param(
          [Parameter(Mandatory = $true)][string]$ActivationUri
      )
      # Activates the registered protocol handler in the caller's interactive desktop
      # session via Start-Process. Mocked at this seam in Pester.
  }
  ```

  The single `ActivationUri` parameter replaces the earlier suggested
  `PackageFamilyName`/`AppId` parameters: protocol activation does not require a
  package-family or app-id lookup, so those parameters are removed. The wrapper is the
  seam for Pester mocking per `.claude/rules/powershell.md`.

- `scripts/Install.ps1`: insert a launch step in Stage 8 (after `Invoke-MsixInstall`
  and `Invoke-MsixCapture`) that invokes the wrapper. The launch failure path must
  trigger the same MSIX rollback used by Stage 8.5.

- `scripts/Install.Preflight.psm1`: extend `Assert-HostAdapterBridgeReadyPreflight` with
  bounded polling. Suggested parameters, subject to planner refinement:

  ```powershell
  function Assert-HostAdapterBridgeReadyPreflight {
      [CmdletBinding()]
      param(
          [Parameter(Mandatory = $true)][string]$DestDockerDir,
          [Parameter()][int]$TimeoutSec = 60,
          [Parameter()][int]$PollIntervalSec = 2,
          [Parameter()][scriptblock]$DelayProvider,
          [Parameter()][scriptblock]$NowProvider
      )
      # ...
  }
  ```

  Production defaults: `TimeoutSec=60`, `PollIntervalSec=2`. Tests inject
  `DelayProvider`/`NowProvider` to drive the loop without wall-clock sleeps.

  Retryable conditions:
  - HTTP 502 with `error.code=TRANSPORT_FAILURE` (MailBridge not yet reachable).
  - HTTP 200 with `data.state in {starting, waiting_for_outlook}` (matches
    `IsBridgeNotReady`).

  Terminal conditions:
  - HTTP 401 / non-envelope responses.
  - Timeout exhaustion.

  On terminal failure, the existing `Format-HostAdapterPreflightFailure` and
  `Invoke-Stage8Point5BridgeReadyOrRollback` paths continue to run.

### Test additions

- `tests/scripts/Install.Preflight.Tests.ps1`: new `It` blocks covering:
  - First-poll success.
  - Success after one or more 502 / not-ready responses.
  - Timeout exhaustion (terminal).
  - Terminal non-retryable response (e.g., HTTP 401).
  - Mock signature parity with `Invoke-HostAdapterStatusRequest` per
    `.claude/rules/powershell.md` mocking rules.

- `tests/scripts/Install.Tests.ps1`: cover the new launch step and its rollback path.
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`: cover the new manifest
  activation entry.

## Quality gates

- Black / Ruff / Pyright (Python): not applicable.
- PoshQC format -> PSScriptAnalyzer -> Pester (PowerShell).
- CSharpier -> .NET Analyzers -> Nullable Analysis -> MSTest (C#).
- Coverage thresholds per `.claude/rules/powershell.md` and `.claude/rules/csharp.md`.
- Operator smoke test on a representative Windows host. Smoke evidence under
  `evidence/regression-testing/`.

## Open Questions (resolved)

1. Does the chosen activation option require a manifest version bump or a publish
   pipeline change?
   **A1:** Yes for the manifest Identity Version: it is bumped from `1.0.0.0` to
   `1.0.1.0` so `Add-AppxPackage` re-registers the protocol handler deterministically on
   upgrade. The downstream bundle version produced by `Publish.ps1` (e.g., `1.0.1.9`) is
   independent of the manifest's own Identity Version and is unchanged. No publish
   pipeline change is required.
2. Is there an existing `PackageFamilyName` lookup helper in the repo, or does
   `Invoke-MsixAppActivate` need to derive it from `Invoke-MsixCapture`'s result?
   **A2:** Not needed. Protocol activation does not require a `PackageFamilyName` lookup;
   `Invoke-MsixAppActivate` takes only an `ActivationUri`. No derivation from
   `Invoke-MsixCapture` is performed.
3. Should the launch step be skipped under `-SkipDocker`, mirroring Stage 7a / Stage 7
   / Stage 8.5?
   **A3:** Yes — skip when `-SkipDocker` is supplied, mirroring Stage 7a, Stage 7, and
   Stage 8.5, to keep behavior consistent.

## References

- `artifacts/research/2026-04-29-install-stage8.5-bridge-ready-defect.md`
- `installer/Package.appxmanifest`
- `scripts/Install.ps1`
- `scripts/Install.Helpers.psm1`
- `scripts/Install.Preflight.psm1`
- `src/OpenClaw.HostAdapter/Program.cs:444`
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`
