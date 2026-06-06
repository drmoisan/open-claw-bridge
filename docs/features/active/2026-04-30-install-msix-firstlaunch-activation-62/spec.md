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

## Activation-Pattern Options (atomic-planner to evaluate and select)

The atomic-planner must evaluate the activation-pattern candidates against elevation /
session correctness, signing/publish implications, and operator UX, then select one.

### Option 3a — `windows.protocol` activation, retain `windows.startupTask`

Add a `<uap:Extension Category="windows.protocol">` with a custom scheme (for example,
`openclaw-mailbridge:`) so the installer can activate the package by launching
`Start-Process 'openclaw-mailbridge:firstrun'`. The protocol activation runs in the
caller's interactive session.

- Pros: Activation runs in the operator's session, not SYSTEM. Reusable for support /
  diagnostics. Retains startup-at-logon via the existing startupTask.
- Cons: New URI scheme is operator-visible and must be reserved across builds. Requires
  manifest schema review.

### Option 3b — `windows.appExecutionAlias` plus startup task

Add a `<uap5:Extension Category="windows.appExecutionAlias">` so MailBridge can be
launched by alias name from any console (e.g., `openclaw-mailbridge.exe`). Installer
launches via the alias.

- Pros: Discoverable for operator support. Aliases activate within the operator session.
- Cons: Alias collisions possible. Manifest contract slightly more complex.

### Option 3c — Replace `windows.startupTask` with `windows.fullTrustProcess` (or equivalent)

Move the activation entry point so first-launch activation through the MSIX shell
activation contract is well-defined, and reach steady-state startup through a
companion mechanism.

- Pros: Single activation contract.
- Cons: Largest manifest change. Requires re-thinking the logon-startup story.

The atomic-planner must record the chosen option and rationale in the plan and
update this spec accordingly.

## High-Level Design (post option-selection)

Regardless of which option is selected, the implementation includes:

### Manifest changes

- `installer/Package.appxmanifest` updated for the chosen activation pattern.
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` updated to assert the new
  contract, with existing `windows.startupTask` assertions retained or replaced
  according to the selected option.

### PowerShell changes

- `scripts/Install.Helpers.psm1`: new wrapper seam for the launch step. Suggested
  signature, subject to planner refinement:

  ```powershell
  function Invoke-MsixAppActivate {
      [CmdletBinding()]
      param(
          [Parameter(Mandatory = $true)][string]$PackageFamilyName,
          [Parameter(Mandatory = $true)][string]$AppId
      )
      # Implementation depends on selected option (protocol launch, alias, or shell
      # activation). Returns the launched process or activation receipt.
  }
  ```

  The wrapper is the seam for Pester mocking per `.claude/rules/powershell.md`.

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

## Open Questions

1. Does the chosen activation option require a manifest version bump or a publish
   pipeline change? The atomic-planner must determine and document.
2. Is there an existing `PackageFamilyName` lookup helper in the repo, or does
   `Invoke-MsixAppActivate` need to derive it from `Invoke-MsixCapture`'s result?
3. Should the launch step be skipped under `-SkipDocker`, mirroring Stage 7a / Stage 7
   / Stage 8.5? Working assumption: yes — keep behavior consistent.

## References

- `artifacts/research/2026-04-29-install-stage8.5-bridge-ready-defect.md`
- `installer/Package.appxmanifest`
- `scripts/Install.ps1`
- `scripts/Install.Helpers.psm1`
- `scripts/Install.Preflight.psm1`
- `src/OpenClaw.HostAdapter/Program.cs:444`
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`
