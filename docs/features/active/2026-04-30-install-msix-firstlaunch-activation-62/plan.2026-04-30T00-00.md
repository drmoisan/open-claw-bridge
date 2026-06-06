# Atomic Plan — Install MSIX First-Launch Activation (Issue #62)

- Work Mode: full-bug
- Issue: #62
- Plan File: docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/plan.2026-04-30T00-00.md
- Created: 2026-04-30T00-00
- Status: Draft for preflight validation
- Feature Folder: `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/`
- Evidence Root: `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/`

## Activation-Pattern Selection

**Selected option: Option 3a — `windows.protocol` activation, retain `windows.startupTask`.**

Rationale (addresses spec.md decision items):

- **Cross-session activation correctness from elevated PowerShell.** A `windows.protocol` extension binds a custom URI scheme to the packaged Application identity. Activation through `Start-Process 'openclaw-mailbridge:firstrun'` is performed by the shell broker, which resolves the registered package identity and starts the executable in the operator's interactive desktop session. This is the same activation mechanism browsers and Office use to invoke registered protocol handlers, and it works from elevated PowerShell because the broker is responsible for the session-token routing rather than the calling process. This avoids the cross-session pitfalls of `Start-Process shell:AppsFolder\…` and the `IApplicationActivationManager` COM seam called out in the research document.
- **MSIX rebuild and version bump.** The manifest change requires a rebuild of the MSIX. To make `Add-AppxPackage` deterministic across re-installs while the manifest contract is changing, the `Identity/@Version` is bumped (planner direction: `1.0.0.0` → `1.0.1.0` for the manifest under `installer/Package.appxmanifest`; downstream bundle versioning, e.g., `1.0.1.9`, is unchanged because that is set by `Publish.ps1` and is independent of the manifest's own Identity version). The bump also forces protocol-handler registration on upgrade.
- **Existing `windows.startupTask`.** Retained verbatim. The protocol extension is purely additive. Steady-state startup at logon continues to use `windows.startupTask`. The new scheme only fires on explicit `Start-Process` invocation by the installer and (optionally) operator support workflows.
- **Operator-visible side effects.** A new URI scheme is registered (`openclaw-mailbridge:`). It is namespaced and unlikely to collide. There is no Start Menu entry change, no PATH alias, no Add/Remove Programs change beyond the existing MSIX entry. Default-handler prompts do not appear because there is exactly one registered handler.
- **Test surface in `MsixPackageTests.cs`.** Two new MSTest cases assert (a) the manifest contains a `<uap:Extension Category="windows.protocol">` with `<uap:Protocol Name="openclaw-mailbridge"/>`, and (b) the existing `windows.startupTask` element remains present and unchanged. The existing manifest assertions are preserved.

Options not selected:

- **Option 3b — `windows.appExecutionAlias`.** Rejected. The alias would be added to PATH for all consoles, creating an operator-visible side effect that is broader than required, with non-zero collision risk. Aliases also exhibit known inconsistencies when invoked from elevated PowerShell.
- **Option 3c — replace `windows.startupTask` with `windows.fullTrustProcess` or equivalent.** Rejected. Largest manifest change; loses logon-startup behavior; requires re-thinking the steady-state autostart story; provides no advantage over Option 3a for the install-time defect under repair.

The spec.md update task in Phase 1 records this selection inside `spec.md`.

## Branch and Commit Strategy

- **Base branch:** `main`. The current working tree on `bug/install-stale-hostadapter-detection` carries 3 untracked audit artifacts from the prior `2026-04-26-install-stale-hostadapter-detection` feature plus uncommitted feature folder content for #62. The executor MUST branch off `main` (not the current working branch) for issue #62 to keep history clean and to avoid coupling #62 to in-flight work on #59.
- **Working branch name:** `bug/install-msix-firstlaunch-activation-62`.
- **Commit boundaries (planner-recommended):**
  1. Phase 0 evidence captures (no code change).
  2. Manifest change + MSIX MSTest updates (single C# commit).
  3. PowerShell wrapper seam additions to `Install.Helpers.psm1` (`Invoke-MsixAppActivate`).
  4. `Install.Preflight.psm1` bounded polling refactor + clock/delay seams.
  5. `Install.ps1` Stage 8 launch-step insertion.
  6. Pester test additions in `tests/scripts/Install.Preflight.Tests.ps1` and `tests/scripts/Install.Tests.ps1`.
  7. Final-QA evidence + spec.md update + AC mapping.
- Commit messages follow Conventional Commits with the issue suffix `(#62)`.

## Rollback Strategy

Trigger: AC-10 operator smoke test fails on the representative Windows host with MailBridge not visible in Task Manager after `Install.ps1 -Force`, or `/v1/status` does not reach `data.state` outside `{starting, waiting_for_outlook}` within the bounded deadline.

Abort path (no commit to `main`):

1. Capture failure evidence under `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/smoke-failure.md` including the exact `Install.ps1` invocation, observed Stage 8.5 output, `Get-AppxPackage -Name 'OpenClaw.MailBridge'` snapshot, `Get-Process OpenClaw.MailBridge` snapshot, and the `/v1/status` body.
2. Run `Install.ps1` rollback path (Stage 8.5 already calls `Invoke-MsixRemove`); manually verify with `Get-AppxPackage -Name 'OpenClaw.MailBridge'` returning empty.
3. If installation persisted, run `scripts/Uninstall.ps1` to restore steady state.
4. Re-open issue #62 with the failure evidence linked; do not merge `bug/install-msix-firstlaunch-activation-62` into `main`.
5. Re-plan: the most likely re-plan path is to add a fallback launch via `Invoke-CommandInDesktopPackage` after a failed protocol-activation poll cycle, or to switch to Option 3b. Re-planning is a re-entry into atomic-planner.

Cleanup-on-abort checklist: working branch retained for diagnostic purposes; evidence files retained; rollback executed; no `main` push.

## Wrapper / Seam Strategy

### `Invoke-MsixAppActivate` (new, in `Install.Helpers.psm1`)

Wrapper-seam rule from `.claude/rules/powershell.md` requires the executable boundary be in a wrapper function with a non-`Args` parameter name and array splat semantics. Final signature:

```powershell
function Invoke-MsixAppActivate {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([void])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ActivationUri
    )
    # Production: invokes Start-Process to activate the registered URI handler in
    # the caller's interactive desktop session. Mocked in Pester at this seam.
    if ($PSCmdlet.ShouldProcess($ActivationUri, 'Start-Process protocol activation')) {
        Start-Process -FilePath $ActivationUri | Out-Null
    }
}
```

Notes:

- The parameter name is `ActivationUri` (string), not `Args`, avoiding the `$Args` automatic-variable collision rule.
- Tests register a `Mock Invoke-MsixAppActivate -ParameterFilter { $ActivationUri -eq 'openclaw-mailbridge:firstrun' }`. Mock signature parity is enforced.
- Production call site in `Install.ps1` Stage 8 uses the URI literal `'openclaw-mailbridge:firstrun'`; the URI fragment `firstrun` is reserved for the install path so future operator-support flows can use a different fragment without ambiguity.
- The wrapper is intentionally narrow — no `PackageFamilyName` or `AppId` parameters because protocol activation does not need them. The optional helper signature suggested in spec.md (`-PackageFamilyName`/`-AppId`) is rejected for Option 3a; it would force callers to derive identity at every call site for no benefit.

### Polling clock/delay seams in `Assert-HostAdapterBridgeReadyPreflight`

Both seams are `[scriptblock]` parameters with safe production defaults so Pester can drive the polling loop without `Start-Sleep` against the wall clock:

```powershell
function Assert-HostAdapterBridgeReadyPreflight {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$DestDockerDir,
        [Parameter()][int]$TimeoutSec = 60,
        [Parameter()][int]$PollIntervalSec = 2,
        [Parameter()][scriptblock]$NowProvider = { [datetime]::UtcNow },
        [Parameter()][scriptblock]$DelayProvider = { param([int]$Seconds) Start-Sleep -Seconds $Seconds }
    )
    # ...
}
```

Production defaults: `TimeoutSec = 60`, `PollIntervalSec = 2`, `NowProvider = { [datetime]::UtcNow }`, `DelayProvider` invokes real `Start-Sleep`. Tests substitute deterministic `NowProvider`/`DelayProvider` script blocks that advance a counter and never block.

### Stage 8 `Install.ps1` launch step

Insert immediately after `$PackageFullName = Invoke-MsixCapture` and before Stage 8.5 polling. The launch step is gated by the same `-not $SkipDocker` predicate as Stage 7/Stage 8.5 (working-assumption answer to spec.md Open Question #3: "yes, skip when -SkipDocker"). Its failure path triggers the same rollback (`Invoke-MsixRemove -PackageFullName $PackageFullName`) used by the Stage 8.5 wrapper.

```
[install:msix-activate] Activating MailBridge via protocol handler
Invoke-MsixAppActivate -ActivationUri 'openclaw-mailbridge:firstrun'
```

The protocol-activation step is fire-and-forget; bounded polling in Stage 8.5 is the readiness gate. If `Invoke-MsixAppActivate` itself throws (no registered handler, or `Start-Process` failure), the catch block in `Install.ps1` calls `Invoke-MsixRemove` and re-throws, preserving the no-orphan invariant.

## Test Plan

### Pester (PowerShell)

`tests/scripts/Install.Preflight.Tests.ps1` — new `It` blocks under a new `Context 'Assert-HostAdapterBridgeReadyPreflight bounded polling'`:

- `It 'returns on the first poll when status is 200 with data.state="ready"'`
- `It 'retries on HTTP 502 with error.code=TRANSPORT_FAILURE and succeeds when MailBridge becomes ready'`
- `It 'retries on HTTP 200 with data.state="starting" and succeeds when state transitions to "ready"'`
- `It 'retries on HTTP 200 with data.state="waiting_for_outlook" and succeeds when state transitions to "ready"'`
- `It 'throws via Format-HostAdapterPreflightFailure on HTTP 401 immediately (terminal, no retry)'`
- `It 'throws via Format-HostAdapterPreflightFailure on a non-envelope body immediately (terminal)'`
- `It 'throws after timeout exhaustion when status remains TRANSPORT_FAILURE for the entire window'`
- `It 'invokes DelayProvider with PollIntervalSec between retries and never calls Start-Sleep'`
- `It 'uses NowProvider for all clock reads and never calls [datetime]::UtcNow directly'`

`tests/scripts/Install.Tests.ps1` — new `Context 'Stage 8 protocol-activation launch'`:

- `It 'invokes Invoke-MsixAppActivate with the openclaw-mailbridge:firstrun URI after Invoke-MsixCapture'`
- `It 'skips the activation step when -SkipDocker is supplied'`
- `It 'calls Invoke-MsixRemove with the captured PackageFullName when Invoke-MsixAppActivate throws'`
- `It 'does not call Invoke-MsixAppActivate before Invoke-MsixCapture'` (ordering assertion)

`tests/scripts/Install.Helpers.Tests.ps1` — new `Context 'Invoke-MsixAppActivate'`:

- `It 'invokes Start-Process with the supplied ActivationUri under ShouldProcess'`
- `It 'is a no-op when ShouldProcess returns false (e.g., -WhatIf)'`

Mocking rules (consistent with `.claude/rules/powershell.md`):

- Mock `Invoke-MsixAppActivate`, not `Start-Process`. Mock `Invoke-HostAdapterStatusRequest`, not `Invoke-WebRequest`. Mock `Invoke-MsixCapture`, `Invoke-MsixInstall`, `Invoke-MsixRemove` at the wrapper seam. Mock signatures match production parameter names exactly (`ActivationUri`, `StatusUri`, `Token`, `PackageFullName`).
- Polling tests inject `NowProvider`/`DelayProvider` script blocks; no real `Start-Sleep`. The `DelayProvider` mock records call order to assert spacing.

### MSTest (C#)

`tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` — new `[TestMethod]` cases:

- `Manifest_ContainsProtocolExtension_WithOpenClawMailBridgeScheme`
  - Asserts a `uap:Extension` with `Category="windows.protocol"` exists, with a child `uap:Protocol` whose `Name` attribute equals `openclaw-mailbridge`.
- `Manifest_ProtocolExtension_DoesNotConflictWithStartupTask`
  - Asserts both the existing `windows.startupTask` extension (preserved) and the new `windows.protocol` extension exist on the same `Application` element.
- `Manifest_IdentityVersion_HasBumpedMajorMinorOrBuild`
  - Asserts the `Identity/@Version` is greater than `1.0.0.0` (specifically, asserts the parsed `System.Version` compares greater than `new Version(1, 0, 0, 0)`).

The existing `Manifest_ContainsStartupTaskExtension_WithCorrectExecutable` test is preserved unchanged (Option 3a is additive).

## Phase Outline

- Phase 0 — Baseline capture (policy reads + repo baselines for PowerShell and C#).
- Phase 1 — Spec.md selection record (no code change).
- Phase 2 — MSIX manifest change and MSTest updates.
- Phase 3 — `Install.Helpers.psm1` wrapper seam `Invoke-MsixAppActivate`.
- Phase 4 — `Install.Preflight.psm1` bounded polling with clock/delay seams.
- Phase 5 — `Install.ps1` Stage 8 protocol-activation launch step.
- Phase 6 — Pester test surface for activation, polling, and rollback paths.
- Phase 7 — Operator smoke test (AC-10).
- Phase 8 — Final QA loop (PowerShell + C#) with coverage evidence and AC closure.

## Phase 0 — Baseline Capture

Evidence directory for this phase: `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/`.

- [x] [P0-T1] Read policy file `.claude/rules/general-code-change.md` and record an entry in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/phase0-instructions-read.md` with `Timestamp:`, `Policy Order:`, and the file path.
- [x] [P0-T2] Append a read of `.claude/rules/general-unit-test.md` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T3] Append a read of `.claude/rules/powershell.md` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T4] Append a read of `.claude/rules/csharp.md` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T5] Append a read of `.claude/rules/tonality.md` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T6] Run PowerShell formatter baseline via MCP `mcp__drmCopilotExtension__run_poshqc_format` (read-only or check mode); persist artifact `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/posh-format.<timestamp>.md` containing `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with the count of files needing reformatting (must be 0 to be considered clean baseline). [TOOLING SUBSTITUTION: `Invoke-Formatter` used; PoshQC MCP absent. Baseline drift=4 pre-existing files, none caused by this feature.]
- [x] [P0-T7] Run PowerShell analyzer baseline via MCP `mcp__drmCopilotExtension__run_poshqc_analyze`; persist artifact `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/posh-analyze.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (count of diagnostics by severity). [TOOLING SUBSTITUTION: `Invoke-ScriptAnalyzer`; 0 diagnostics.]
- [x] [P0-T8] Run Pester baseline with coverage via MCP `mcp__drmCopilotExtension__run_poshqc_test` against `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`; persist artifact `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/pester.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing pass/fail counts and the numeric repository-wide PowerShell line-coverage percentage (baseline value; must be present, not `UNVERIFIED`). [TOOLING SUBSTITUTION: `Invoke-Pester` + New-PesterConfiguration coverage; 216 passed, 90.42% over the three Install files.]
- [x] [P0-T9] Run CSharpier check baseline `dotnet tool run csharpier --check .`; persist artifact `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/csharpier.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (file count needing reformatting; must be 0). [TOOLING SUBSTITUTION: global `csharpier check .` (no tool manifest); 94 files clean.]
- [x] [P0-T10] Run .NET analyzers baseline `dotnet build OpenClaw.MailBridge.sln /p:Configuration=Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true /p:TreatWarningsAsErrors=true`; persist artifact `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/dotnet-build.<timestamp>.md` with required schema fields and warning/error counts in `Output Summary:`.
- [x] [P0-T11] Run MSTest baseline with coverage `dotnet test OpenClaw.MailBridge.sln --collect:"XPlat Code Coverage" --settings tests/coverlet.runsettings`; persist artifact `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/dotnet-test.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing pass/fail counts and the numeric C# line-coverage percentage (must be present). [TOOLING SUBSTITUTION: `--settings mailbridge.runsettings` (tests/coverlet.runsettings absent); MailBridge group 93.08%, all tests pass.]
- [x] [P0-T12] Capture a baseline of `installer/Package.appxmanifest` SHA-256 and the existing two `MsixPackageTests.cs` test results into `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/baseline/manifest-baseline.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (manifest hash + pre-change test pass-count of 2 startupTask + 1 no-service + identity-version assertions).

## Phase 1 — Spec Selection Record

- [x] [P1-T1] Edit `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/spec.md`: replace the "Activation-Pattern Options (atomic-planner to evaluate and select)" section with a "Selected Activation Pattern" section that records Option 3a, the rationale (cross-session correctness, additive manifest change, retained `windows.startupTask`), and links back to this plan file. No code change. Acceptance: `git diff -- docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/spec.md` shows the section replaced and references `Option 3a`.
- [x] [P1-T2] Edit `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/spec.md`: under "High-Level Design", replace the suggested `Invoke-MsixAppActivate` signature with the final signature `Invoke-MsixAppActivate -ActivationUri <string>`. Acceptance: file contains the updated signature and removes references to `PackageFamilyName`/`AppId`.
- [x] [P1-T3] Edit `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/spec.md`: in "Open Questions", record planner answers — Q1 = "Yes, manifest Identity Version bumped from 1.0.0.0 to 1.0.1.0; downstream bundle Publish.ps1 versioning is independent and unchanged"; Q2 = "Not needed — protocol activation does not require PackageFamilyName lookup"; Q3 = "Yes, skip when -SkipDocker, mirroring Stage 7a/Stage 7/Stage 8.5". Acceptance: spec.md contains explicit answers to Q1, Q2, Q3.

## Phase 2 — Manifest Change and MSTest Updates

Files in scope: `installer/Package.appxmanifest`, `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`.

- [x] [P2-T1] Edit `installer/Package.appxmanifest`: change `Identity/@Version` from `1.0.0.0` to `1.0.1.0`. Acceptance: `(Select-Xml -Path installer/Package.appxmanifest -XPath '/*[local-name()="Package"]/*[local-name()="Identity"]/@Version').Node.Value` returns `1.0.1.0`. [VERIFIED: returns 1.0.1.0.]
- [x] [P2-T2] Edit `installer/Package.appxmanifest`: add a `<uap:Extension Category="windows.protocol">` block as a sibling of the existing `<uap5:Extension Category="windows.startupTask">` element inside `Application/Extensions`. The new block declares `<uap:Protocol Name="openclaw-mailbridge"/>`. Acceptance: XPath query for `//*[local-name()="Extension" and @Category="windows.protocol"]/*[local-name()="Protocol" and @Name="openclaw-mailbridge"]` returns one element; the existing `windows.startupTask` element is unmodified. [VERIFIED: 1 protocol match, startupTask preserved, XML well-formed.]
- [x] [P2-T3] Add MSTest `Manifest_ContainsProtocolExtension_WithOpenClawMailBridgeScheme` to `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`. Acceptance: test builds and passes; uses the existing `XNamespace ManifestNs` plus a new `XNamespace UapNs = "http://schemas.microsoft.com/appx/manifest/uap/windows10";` declaration; asserts a single matching `Extension` element and a single child `Protocol` element with `Name == "openclaw-mailbridge"`.
- [x] [P2-T4] Add MSTest `Manifest_ProtocolExtension_DoesNotConflictWithStartupTask` to `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`. Acceptance: test asserts both the protocol extension and the startupTask extension exist as descendants of the same `Application` element; passes.
- [x] [P2-T5] Add MSTest `Manifest_IdentityVersion_IsAtLeast_1_0_1_0` to `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`. Acceptance: test parses `Identity/@Version` into `System.Version` and asserts `actual >= new System.Version(1, 0, 1, 0)`; passes.
- [x] [P2-T6] Run CSharpier on the test file: `dotnet tool run csharpier tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs`. Acceptance: no formatting changes remain; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/csharpier-p2.<timestamp>.md`. [TOOLING SUBSTITUTION: global `csharpier check`; 0 changes.]
- [x] [P2-T7] Run `dotnet test --filter "FullyQualifiedName~MsixPackageTests"` and confirm all five MSTest cases pass (3 existing + 2 new + 1 version assertion = 6 total in the class touched here; legacy tests preserved). Acceptance: `EXIT_CODE: 0` and `Output Summary:` shows all matching tests passing; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/dotnet-test-p2.<timestamp>.md`. [VERIFIED: 9 passed, 2 skipped (env-gated), 0 failed.]
- [x] [P2-T8] Verify file-size cap: `installer/Package.appxmanifest` and `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` each remain under 500 lines after the changes. Acceptance: `(Get-Content <path> | Measure-Object -Line).Lines -lt 500` for both; record in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/file-size-p2.<timestamp>.md`. [VERIFIED: 45 and 291 lines.]

## Phase 3 — Install.Helpers.psm1 Wrapper Seam

File in scope: `scripts/Install.Helpers.psm1`.

- [x] [P3-T1] Add `Invoke-MsixAppActivate` function to `scripts/Install.Helpers.psm1` with the exact signature recorded in this plan (single mandatory parameter `ActivationUri` typed `[string]`, `[CmdletBinding(SupportsShouldProcess = $true)]`, `[OutputType([void])]`). Body invokes `Start-Process -FilePath $ActivationUri | Out-Null` under `ShouldProcess`. Acceptance: `Get-Command -Module Install.Helpers -Name Invoke-MsixAppActivate` returns the function with parameter `ActivationUri`. [VERIFIED.]
- [x] [P3-T2] Update the `Export-ModuleMember -Function` list in `scripts/Install.Helpers.psm1` to include `'Invoke-MsixAppActivate'`. Acceptance: `(Import-Module ./scripts/Install.Helpers.psm1 -Force; Get-Command -Module Install.Helpers).Name -contains 'Invoke-MsixAppActivate'` is true. [VERIFIED. Also updated the exact export-surface assertion in `tests/scripts/Install.Helpers.Tests.ps1` (mechanically necessary so the existing test stays green); 35/35 pass.]
- [x] [P3-T3] Verify `scripts/Install.Helpers.psm1` remains under 500 lines. Acceptance: line count check ≤ 499; record in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/file-size-p3.<timestamp>.md`. [464 lines.]
- [x] [P3-T4] Run PoshQC format on `scripts/Install.Helpers.psm1`: `mcp__drmCopilotExtension__run_poshqc_format` scoped to that file. Acceptance: no formatter changes remain after run; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-format-p3.<timestamp>.md`. [TOOLING SUBSTITUTION: `Invoke-Formatter`; net new drift = 0 (only the pre-existing baseline line remains).]
- [x] [P3-T5] Run PSScriptAnalyzer on `scripts/Install.Helpers.psm1`: `mcp__drmCopilotExtension__run_poshqc_analyze` scoped to that file. Acceptance: `EXIT_CODE: 0` and zero new diagnostics relative to baseline; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-analyze-p3.<timestamp>.md`. [0 diagnostics.]

## Phase 4 — Install.Preflight.psm1 Bounded Polling

File in scope: `scripts/Install.Preflight.psm1`.

- [x] [P4-T1] Modify `Assert-HostAdapterBridgeReadyPreflight` signature in `scripts/Install.Preflight.psm1` to add four new parameters: `[int]$TimeoutSec = 60`, `[int]$PollIntervalSec = 2`, `[scriptblock]$NowProvider = { [datetime]::UtcNow }`, `[scriptblock]$DelayProvider = { param([int]$Seconds) Start-Sleep -Seconds $Seconds }`. Acceptance: `(Get-Command Assert-HostAdapterBridgeReadyPreflight).Parameters.Keys` contains `TimeoutSec`, `PollIntervalSec`, `NowProvider`, `DelayProvider`. [VERIFIED.]
- [x] [P4-T2] Refactor the body of `Assert-HostAdapterBridgeReadyPreflight` to compute a deadline via `& $NowProvider`, then loop: invoke `Invoke-HostAdapterStatusRequest`, classify the response, and `return` on ready / `throw` on terminal / `& $DelayProvider $PollIntervalSec` and continue on retryable. Acceptance: function never invokes `Start-Sleep` directly and never invokes `[datetime]::UtcNow` directly outside the default `NowProvider` script block. [VERIFIED: only occurrences are inside the default scriptblocks.]
- [x] [P4-T3] Define classification logic inside `Assert-HostAdapterBridgeReadyPreflight`: a response is **retryable** when (HTTP status is 502 AND parsed JSON `error.code` equals `TRANSPORT_FAILURE`) OR (HTTP status is 200 AND parsed JSON `data.state` is in `{starting, waiting_for_outlook}`). It is **terminal-fail** for any other non-200 (including 401), or any 200 with a body that fails to parse as JSON or lacks `data.state`. It is **success** for HTTP 200 with `data.state` non-empty and not in `{starting, waiting_for_outlook}`. Acceptance: code review confirms three distinct branches in the body. [Extracted to `Get-HostAdapterBridgeReadyClassification` returning success/retryable/terminal; three branches confirmed.]
- [x] [P4-T4] On timeout exhaustion, call `Format-HostAdapterPreflightFailure -StatusUri $statusUri -StatusCode $lastStatusCode -Body $lastBody` with the **last observed** status/body, and `throw` the formatted message. Acceptance: dedicated branch exists when the loop exits without success. [VERIFIED: post-loop throw uses last status/body.]
- [x] [P4-T5] Verify `scripts/Install.Preflight.psm1` remains under 500 lines after the refactor. Acceptance: line count check ≤ 499; record in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/file-size-p4.<timestamp>.md`. [336 lines.]
- [x] [P4-T6] Run PoshQC format on `scripts/Install.Preflight.psm1`. Acceptance: no formatter changes remain; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-format-p4.<timestamp>.md`. [TOOLING SUBSTITUTION: `Invoke-Formatter`; drift NONE.]
- [x] [P4-T7] Run PSScriptAnalyzer on `scripts/Install.Preflight.psm1`. Acceptance: zero new diagnostics; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-analyze-p4.<timestamp>.md`. [0 diagnostics.]

## Phase 5 — Install.ps1 Stage 8 Launch Step

File in scope: `scripts/Install.ps1`.

- [ ] [P5-T1] In `scripts/Install.ps1`, after `$PackageFullName = Invoke-MsixCapture` and before the `# Stage 8.5` comment, insert a Stage 8b block that, when `-not $SkipDocker`, writes `Write-Information '[install:msix-activate] Activating MailBridge via protocol handler' -InformationAction Continue` and calls `Invoke-MsixAppActivate -ActivationUri 'openclaw-mailbridge:firstrun'` inside a `try`/`catch`. The `catch` clause MUST call `Invoke-MsixRemove -PackageFullName $PackageFullName` (tolerated failure pattern matching Stage 8.5) and re-throw the original message. Acceptance: literal string `[install:msix-activate]` appears in `Install.ps1`; rollback path matches the Stage 8.5 pattern verbatim.
- [ ] [P5-T2] Verify `scripts/Install.ps1` remains under 500 lines after the insertion. Acceptance: line count check ≤ 499; record in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/file-size-p5.<timestamp>.md`.
- [ ] [P5-T3] Run PoshQC format on `scripts/Install.ps1`. Acceptance: no formatter changes remain; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-format-p5.<timestamp>.md`.
- [ ] [P5-T4] Run PSScriptAnalyzer on `scripts/Install.ps1`. Acceptance: zero new diagnostics; persist evidence to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-analyze-p5.<timestamp>.md`.

## Phase 6 — Pester Test Surface

Files in scope: `tests/scripts/Install.Preflight.Tests.ps1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`.

- [ ] [P6-T1] Add `It 'returns on the first poll when status is 200 with data.state="ready"'` to `tests/scripts/Install.Preflight.Tests.ps1` under a new `Context 'Assert-HostAdapterBridgeReadyPreflight bounded polling'`. Mock `Invoke-HostAdapterStatusRequest -ParameterFilter { $StatusUri -ne $null -and $Token -ne $null }` to return `[pscustomobject]@{ StatusCode = 200; Content = '{"data":{"state":"ready"},"meta":{"adapterVersion":"x"}}' }`. Inject deterministic `NowProvider`/`DelayProvider`. Assert function returns without throwing and `DelayProvider` was called zero times. Acceptance: test passes when run via `mcp__drmCopilotExtension__run_poshqc_test`.
- [ ] [P6-T2] Add `It 'retries on HTTP 502 with TRANSPORT_FAILURE then succeeds on subsequent ready response'` to `tests/scripts/Install.Preflight.Tests.ps1`. Mock returns 502/`TRANSPORT_FAILURE` body twice, then 200/`ready`. Assert function returns successfully, `DelayProvider` was called exactly twice with `2` (or the supplied `PollIntervalSec`). Acceptance: test passes.
- [ ] [P6-T3] Add `It 'retries on HTTP 200 with data.state=starting then succeeds on ready'` to `tests/scripts/Install.Preflight.Tests.ps1`. Acceptance: same shape as P6-T2 with `state=starting` body; passes.
- [ ] [P6-T4] Add `It 'retries on HTTP 200 with data.state=waiting_for_outlook then succeeds on ready'` to `tests/scripts/Install.Preflight.Tests.ps1`. Acceptance: same shape with `state=waiting_for_outlook` body; passes.
- [ ] [P6-T5] Add `It 'throws via Format-HostAdapterPreflightFailure on HTTP 401 immediately (terminal)'` to `tests/scripts/Install.Preflight.Tests.ps1`. Mock returns 401 with `'{"error":{"code":"UNAUTHORIZED","message":"x"}}'`. Assert thrown message contains `error.code=UNAUTHORIZED` and `DelayProvider` was called zero times. Acceptance: test passes.
- [ ] [P6-T6] Add `It 'throws on a non-envelope body immediately (terminal)'` to `tests/scripts/Install.Preflight.Tests.ps1`. Mock returns 200 with body `'<html>'`. Assert function throws and `DelayProvider` was called zero times. Acceptance: test passes.
- [ ] [P6-T7] Add `It 'throws after timeout exhaustion when status remains TRANSPORT_FAILURE for the entire window'` to `tests/scripts/Install.Preflight.Tests.ps1`. Use `NowProvider` script block that returns increasing timestamps so the loop exits after the configured `TimeoutSec`. Assert thrown message contains `error.code=TRANSPORT_FAILURE`. Acceptance: test passes; runs in under 100 ms wall clock (confirms no real `Start-Sleep`).
- [ ] [P6-T8] Add `It 'invokes DelayProvider with PollIntervalSec between retries and never calls Start-Sleep directly'` to `tests/scripts/Install.Preflight.Tests.ps1`. Spy on `DelayProvider` calls; mock `Start-Sleep` and assert it is called zero times. Acceptance: test passes.
- [ ] [P6-T9] Add `It 'invokes Invoke-MsixAppActivate with openclaw-mailbridge:firstrun after Invoke-MsixCapture'` to `tests/scripts/Install.Tests.ps1` under a new `Context 'Stage 8 protocol-activation launch'`. Mock `Invoke-MsixInstall`, `Invoke-MsixCapture` (return a fake `PackageFullName`), `Invoke-MsixAppActivate -ParameterFilter { $ActivationUri -eq 'openclaw-mailbridge:firstrun' }`, `Assert-HostAdapterBridgeReadyPreflight` (return). Assert `Invoke-MsixAppActivate` was called once with the expected URI and that the call ordering relative to `Invoke-MsixCapture` and `Assert-HostAdapterBridgeReadyPreflight` is preserved. Acceptance: test passes.
- [ ] [P6-T10] Add `It 'skips Invoke-MsixAppActivate when -SkipDocker is supplied'` to `tests/scripts/Install.Tests.ps1`. Acceptance: `Invoke-MsixAppActivate` was called zero times.
- [ ] [P6-T11] Add `It 'calls Invoke-MsixRemove with the captured PackageFullName when Invoke-MsixAppActivate throws'` to `tests/scripts/Install.Tests.ps1`. Mock `Invoke-MsixAppActivate` to `throw 'boom'`. Assert the test catches a re-thrown exception and `Invoke-MsixRemove -ParameterFilter { $PackageFullName -eq <captured> }` was called exactly once. Acceptance: test passes; the no-orphan invariant from issue #52 is preserved.
- [ ] [P6-T12] Add `It 'does not call Invoke-MsixAppActivate before Invoke-MsixCapture'` to `tests/scripts/Install.Tests.ps1`. Acceptance: ordering assertion holds via Pester `-Verifiable` plus an order-tracking variable; test passes.
- [ ] [P6-T13] Add `It 'invokes Start-Process with the supplied ActivationUri under ShouldProcess'` to `tests/scripts/Install.Helpers.Tests.ps1` under a new `Context 'Invoke-MsixAppActivate'`. Mock `Start-Process -ParameterFilter { $FilePath -eq 'openclaw-mailbridge:firstrun' }`. Acceptance: mock asserts called once and test passes.
- [ ] [P6-T14] Add `It 'is a no-op when ShouldProcess returns false'` to `tests/scripts/Install.Helpers.Tests.ps1`. Invoke `Invoke-MsixAppActivate -ActivationUri 'openclaw-mailbridge:firstrun' -WhatIf`. Acceptance: `Start-Process` mock asserted called zero times.
- [ ] [P6-T15] Run `mcp__drmCopilotExtension__run_poshqc_test` for the entire repository. Acceptance: `EXIT_CODE: 0`, all Pester tests pass, persist artifact to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/pester-p6.<timestamp>.md` with `Output Summary:` containing total pass/fail counts.
- [ ] [P6-T16] Verify each touched test file remains under 500 lines after additions. Acceptance: line count check ≤ 499 per file (`Install.Tests.ps1`, `Install.Preflight.Tests.ps1`, `Install.Helpers.Tests.ps1`); record in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/file-size-p6.<timestamp>.md`.

## Phase 7 — Operator Smoke Test (AC-10)

This phase is performed manually on a representative Windows host that satisfies the AC-10 prerequisites: Windows 10 (>=10.0.17763) or Windows 11; Outlook installed and configured for the operator; Docker Desktop running; PowerShell 7+. The host MUST not have a prior install of `OpenClaw.MailBridge` registered. The smoke test follows the bundle-build → install → observe flow.

- [ ] [P7-T1] Build a fresh bundle on the working branch by running `scripts/Publish.ps1` against the rebuilt MSIX (which incorporates the manifest change from Phase 2). Acceptance: bundle directory under `artifacts/publish/<version>` exists with the new MSIX containing the protocol extension. Persist build log to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/publish.log`.
- [ ] [P7-T2] Confirm the MSIX manifest inside the bundle declares the `windows.protocol` extension by running `Expand-Archive` on the MSIX into a temp dir and reading the embedded `AppxManifest.xml`. Acceptance: the protocol extension and `Identity/@Version >= 1.0.1.0` are both observed; record in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/msix-manifest-check.md` with the XML snippet captured.
- [ ] [P7-T3] On the operator host, with no prior install registered, run `& (Join-Path $bundle 'Install.ps1') -Force -DockerEnvFilePath <env> -AnthropicEnvFilePath <env-anthropic>`. Capture full transcript via `Start-Transcript`/`Stop-Transcript` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/install-transcript.log`. Acceptance: install reaches Stage 9 (`[install:docker] Starting compose stack`) and Stage 10 (`[install:record] Writing install record`) without throwing.
- [ ] [P7-T4] During or immediately after Stage 8.5, capture `Get-Process OpenClaw.MailBridge | Select-Object Id, ProcessName, SessionId, MainModule` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/mailbridge-process.txt`. Acceptance: a single `OpenClaw.MailBridge` process exists with `SessionId` matching the operator session (not session 0).
- [ ] [P7-T5] Capture `/v1/status` body from the operator host: `Invoke-WebRequest -Uri 'http://127.0.0.1:4319/v1/status' -Headers @{Authorization="Bearer $token"} -SkipHttpErrorCheck` to `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/v1-status-body.json`. Acceptance: HTTP status is 200; `data.state` is non-empty and not in `{starting, waiting_for_outlook}`.
- [ ] [P7-T6] Capture `Get-AppxPackage -Name 'OpenClaw.MailBridge'` snapshot showing `PackageFullName` and version `1.0.1.0`. Acceptance: matches the bundle's manifest version.
- [ ] [P7-T7] Write smoke summary `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/<timestamp>/smoke-summary.md` with `Timestamp:`, `Command:` (the `Install.ps1 -Force` invocation), `EXIT_CODE: 0`, `Output Summary:` linking to T3..T6 evidence and citing AC-10 closure. Acceptance: file exists and references the four prior artifacts.

If any of P7-T3..P7-T6 fails, halt and follow the **Rollback Strategy** section above instead of proceeding to Phase 8.

## Phase 8 — Final QA Loop

Full toolchain in order: format → lint → type-check (C# only) → test, restarted from step 1 if any step changes files or fails. Coverage-bearing test commands are mandatory.

- [ ] [P8-T1] Run PoshQC format across the repo: `mcp__drmCopilotExtension__run_poshqc_format`. Acceptance: `EXIT_CODE: 0`, zero file changes; persist `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-format-final.<timestamp>.md`.
- [ ] [P8-T2] Run PSScriptAnalyzer across the repo: `mcp__drmCopilotExtension__run_poshqc_analyze`. Acceptance: `EXIT_CODE: 0` and zero new diagnostics relative to the Phase 0 baseline; persist `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-analyze-final.<timestamp>.md`.
- [ ] [P8-T3] Run Pester with coverage across the repo: `mcp__drmCopilotExtension__run_poshqc_test` against `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`. Acceptance: `EXIT_CODE: 0`, all tests pass, repository PowerShell line coverage `>= 80%`. Persist `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/pester-final.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` containing the numeric repo coverage percentage and pass count.
- [ ] [P8-T4] Compute per-file line coverage for the four changed PowerShell production files (`scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Install.Preflight.psm1`) and assert each is `>= 90%` for changed/new lines. Acceptance: numeric per-file coverage values recorded in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/posh-coverage-deltas-final.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing baseline %, post-change %, and changed-line % per file. AC-08 closure depends on this.
- [ ] [P8-T5] Run CSharpier check: `dotnet tool run csharpier --check .`. Acceptance: `EXIT_CODE: 0`; persist `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/csharpier-final.<timestamp>.md`.
- [ ] [P8-T6] Run .NET analyzers + nullable analysis: `dotnet build OpenClaw.MailBridge.sln /p:Configuration=Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true /p:Nullable=enable /p:TreatWarningsAsErrors=true`. Acceptance: `EXIT_CODE: 0`, zero new warnings; persist `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/dotnet-build-final.<timestamp>.md`.
- [ ] [P8-T7] Run MSTest with coverage: `dotnet test OpenClaw.MailBridge.sln --collect:"XPlat Code Coverage" --settings tests/coverlet.runsettings`. Acceptance: `EXIT_CODE: 0`, all tests pass, repository C# line coverage does not regress relative to the Phase 0 baseline. Persist `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/dotnet-test-final.<timestamp>.md` with numeric coverage values.
- [ ] [P8-T8] Compute per-file C# coverage for `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` (and any other file touched in Phase 2) and assert `>= 90%` for changed/new lines. Acceptance: numeric per-file values recorded in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/dotnet-coverage-deltas-final.<timestamp>.md`. AC-09 closure depends on this.
- [ ] [P8-T9] If any of P8-T1..P8-T8 changed files or failed, restart the loop from P8-T1. Acceptance: a single uninterrupted pass completes (`Output Summary:` of the final pass cites "no file changes" and `EXIT_CODE: 0` for every step).
- [ ] [P8-T10] Map every acceptance criterion AC-01..AC-11 to evidence artifacts in `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/qa-gates/ac-mapping.<timestamp>.md`. Acceptance: each AC has at least one cited evidence path under `docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/`. AC-10 maps to Phase 7 outputs; AC-08 to P8-T4; AC-09 to P8-T8; AC-11 to the absence of failures across P8-T1..P8-T8; AC-04 to P6-T11; AC-02/AC-03 to P6-T1..P6-T8; AC-05/AC-06 to P1 + P2; AC-07 to P3 + P6-T13/P6-T14; AC-01 to P7-T3.

## Risks and Unknowns

- **Protocol activation under elevated PowerShell.** Verified by Microsoft documentation that protocol activation is performed by the shell broker and routed to the operator session; verification on the actual target host happens in Phase 7. If the host returns `0x80073D54`/`0x80070005` or activates under SYSTEM, **rollback** triggers and re-planning explores a fallback to `Invoke-CommandInDesktopPackage`.
- **Manifest schema namespace.** Adding a `<uap:Extension Category="windows.protocol">` requires the existing `xmlns:uap` declaration to remain on the root `Package` element (it does — line 4 of the current manifest). No additional `IgnorableNamespaces` change is needed.
- **MSIX rebuild gating.** Any agent change to `installer/Package.appxmanifest` requires MSIX rebuild before Phase 7 can execute. The rebuild is performed in P7-T1 via `scripts/Publish.ps1`; if `Publish.ps1` does not pick up the manifest change automatically, a re-plan task is required to address the publish pipeline.
- **`Add-AppxPackage` upgrade behavior.** The Identity Version bump from `1.0.0.0` to `1.0.1.0` is required for protocol-handler re-registration on existing installs. Smoke test must run on a clean host (no prior install) per AC-10 to satisfy the stated criterion.
- **Test coverage on `scripts/Install.ps1`.** The Stage 8b launch step is small; the existing Pester test infrastructure in `tests/scripts/Install.Tests.ps1` dot-sources `Install.ps1` for testing. Adding ordering assertions across mocked seams may require extending an existing helper rather than introducing a new one. If extending exceeds the per-batch 3-file production / 3-file test cap, split P6 into two batches.
- **Bundle versioning vs. manifest Identity Version.** The bundle reported in the issue is `1.0.1.9`; the manifest Identity Version is independent and currently `1.0.0.0`. Plan bumps the manifest to `1.0.1.0`. If the operator-facing bundle version constraint requires a higher manifest version, the bump target is adjusted at P2-T1 without changing the rest of the plan.
- **Polling defaults.** `TimeoutSec = 60` and `PollIntervalSec = 2` are heuristic. If smoke-test observation shows MailBridge needs more than 60 s to become ready (e.g., on a cold-Outlook host), the defaults may need to be raised. This is a numeric tuning change inside `Assert-HostAdapterBridgeReadyPreflight` and does not require re-planning.

## Acceptance Criteria Mapping (final summary)

| AC | Where addressed |
|----|------------------|
| AC-01 | P7-T3 (operator install reaches Stage 9 + Stage 10 unattended) |
| AC-02 | P4-T2..T4, P6-T2..T7 (bounded polling, retryable/terminal classification) |
| AC-03 | P4-T1, P6-T1, P6-T8 (clock/delay seams, no Start-Sleep in tests) |
| AC-04 | P5-T1, P6-T11 (rollback path on Stage 8b failure preserves no-orphan invariant) |
| AC-05 | P1-T1, P2-T2 (manifest declares activation pattern; rationale in spec.md) |
| AC-06 | P2-T3..T5 (MsixPackageTests.cs new tests added; existing assertions preserved) |
| AC-07 | P3-T1..T2, P6-T13..T14 (Invoke-MsixAppActivate seam + tests) |
| AC-08 | P8-T4 (per-file PowerShell coverage >= 90% for changed files; repo >= 80%) |
| AC-09 | P8-T8 (per-file C# coverage >= 90% on changes; no repo-wide regression) |
| AC-10 | P7-T1..T7 (operator smoke recorded under evidence/regression-testing) |
| AC-11 | P8-T1..T9 (toolchain clean in single pass) |

## Preflight Signal

DIRECTIVE: PREFLIGHT VALIDATION ONLY

PREFLIGHT: ALL CLEAR
