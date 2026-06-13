- Work Mode: full-bug
- Issue: #62
- Promotion Type: bug
- Short Name: install-msix-firstlaunch-activation
- Created: 2026-04-30

# Install Stage 8.5 fails: MailBridge not launched after MSIX install (manifest activation defect)

## Summary

Operator install of bundle 1.0.1.9 fails at Stage 8.5 (`[install:hostadapter-bridge-check]`)
immediately after MSIX install. HostAdapter `GET /v1/status` returns HTTP 502 with
`error.code=TRANSPORT_FAILURE; error.message=The operation has timed out.`. Root cause is
that `OpenClaw.MailBridge.exe` is never launched between `Add-AppxPackage` and the
bridge-readiness preflight: the MSIX manifest declares MailBridge as a
`windows.startupTask`, which only fires at user logon.

## Repro

```powershell
& (Join-Path $bundle 'Install.ps1') -Force `
  -DockerEnvFilePath (Join-Path $operatorConfig '.env') `
  -AnthropicEnvFilePath (Join-Path $operatorConfig 'secrets\.env.anthropic')
```

Observed: `[install:hostadapter-bridge-check]` throws via
`Format-HostAdapterPreflightFailure`. MSIX rollback runs (no-orphan invariant from #52
preserved), and `Install.ps1` exits before reaching Stage 9.

## Resolution scope (Option 3 — manifest activation change)

Replace or supplement the `windows.startupTask`-only activation with a first-launch-friendly
activation pattern so that `Add-AppxPackage` followed by an explicit launch step in
`Install.ps1` reliably starts MailBridge in the operator session, and Stage 8.5 polling
reaches HTTP 200 with `data.state` not in `{starting, waiting_for_outlook}` within a
bounded deadline.

In scope:

- `installer/Package.appxmanifest` — activation pattern change (candidates:
  `windows.protocol`, `windows.appExecutionAlias`, retain `windows.startupTask` plus add
  explicit activation entry, or equivalent).
- `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` — manifest assertion updates.
- `scripts/Install.ps1` and/or `scripts/Install.Helpers.psm1` — explicit launch step
  after `Invoke-MsixInstall`.
- `scripts/Install.Preflight.psm1` — bounded polling loop in
  `Assert-HostAdapterBridgeReadyPreflight` with deterministic seam for tests.
- `tests/scripts/Install.Preflight.Tests.ps1` and any related Pester suites — coverage
  for polling, retry, and timeout paths.

Out of scope:

- HostAdapter behavior changes beyond what is required to surface or consume the new
  activation contract.
- Docker/compose stage changes.

## Constraints / invariants to preserve

- No-orphan invariant from #52: terminal Stage 8.5 failure must call `Invoke-MsixRemove`
  with the captured `PackageFullName` before re-throw.
- Stage 7 preflight (`Assert-HostAdapterRespondingPreflight`) continues to accept any
  well-formed envelope including 502 with `TRANSPORT_FAILURE`.
- Stage 8.5 readiness contract: HTTP 200 with `data.state` not in
  `{starting, waiting_for_outlook}` (matches `IsBridgeNotReady` in
  `src/OpenClaw.HostAdapter/Program.cs:444`).
- Direct-mode budgets do not apply; this is a cross-language change.

## Acceptance Criteria

- AC-01: With MailBridge not previously running, `Install.ps1 -Force` completes through
  Stage 9 (compose up) on a clean Windows host without manual intervention.
- AC-02: `Assert-HostAdapterBridgeReadyPreflight` polls with a bounded deadline; treats
  HTTP 502 + `TRANSPORT_FAILURE` and HTTP 200 + `state in {starting, waiting_for_outlook}`
  as retryable; treats HTTP 401 / non-envelope responses as terminal.
- AC-03: Polling timing is injectable via a deterministic seam (clock or delay parameter
  with safe production default); Pester tests do not call `Start-Sleep` against the wall
  clock.
- AC-04: On terminal Stage 8.5 failure, `Invoke-MsixRemove` is called for the captured
  `PackageFullName`; the no-orphan invariant from #52 is preserved.
- AC-05: `Package.appxmanifest` declares an activation pattern that allows
  installer-driven first launch; the chosen pattern is documented in `spec.md` with
  rationale.
- AC-06: `tests/OpenClaw.MailBridge.Tests/MsixPackageTests.cs` updated to assert the new
  activation contract; existing manifest assertions either updated or preserved with
  explanation.
- AC-07: New launch wrapper seam (e.g., `Invoke-MsixAppActivate`) introduced in
  `Install.Helpers.psm1` per the wrapper-seam mocking rule; mocked in tests, not invoked
  against a real MSIX in unit tests.
- AC-08: All changed PowerShell files reach >=90% line coverage; repository PowerShell
  coverage stays >=80%.
- AC-09: All changed C# files reach >=90% line coverage; repository C# coverage does not
  regress.
- AC-10: Operator smoke test executed on a representative Windows host: `Install.ps1
  -Force` succeeds, MailBridge process visible in Task Manager under operator session,
  HostAdapter `/v1/status` returns 200 with `data.state` not in not-ready set. Smoke
  results recorded under `evidence/regression-testing/`.
- AC-11: PSScriptAnalyzer clean, Pester all green, .NET analyzers clean, MSTest all
  green, all in a single toolchain pass.

## References

- `artifacts/research/2026-04-29-install-stage8.5-bridge-ready-defect.md` — root-cause
  investigation.
- `docs/features/active/2026-04-26-install-stale-hostadapter-detection/` — adjacent
  prior feature (complete; unmerged at time of writing).
- `src/OpenClaw.HostAdapter/Program.cs:444` — `IsBridgeNotReady` contract.
- Issue #52 — no-orphan invariant.
- Issue #62 — this issue.
