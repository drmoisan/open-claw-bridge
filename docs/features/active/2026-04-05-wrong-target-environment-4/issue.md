# wrong-target-environment (Issue #4)

- Date captured: 2026-04-05
- Author: drmoisan
- Status: Promoted -> docs/features/active/wrong-target-environment/ (Issue #4)

- Issue: #4
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/4
- Last Updated: 2026-04-06
- Work Mode: minor-audit

## Summary

The MailBridge solution targeted `net8.0-windows` even though the workspace and local machine were configured for .NET 10.
Opening the workspace triggered test discovery against a `net8.0-windows` testhost that could not start because `Microsoft.NETCore.App` 8.0.x was not installed.

## Environment

- OS/version: Windows 11 (user workspace on `C:\Users\DanMoisan\repos\open-claw-bridge`)
- Python version: N/A
- Command/flags used: Visual Studio / VS Test discovery on workspace open; repro also confirmed with `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal`
- Data source or fixture: Local MailBridge solution and `OpenClaw.MailBridge.Tests.dll`

## Steps to Reproduce

1. Open the `open-claw-bridge` workspace on a machine with .NET 10 SDK/runtime installed but without .NET 8 runtime.
2. Let the IDE perform test discovery for `tests\OpenClaw.MailBridge.Tests`.
3. Observe the testhost startup failure for `bin\Debug\net8.0-windows\testhost.exe`.

## Expected Behavior

The solution and test projects should target the repo's intended runtime so test discovery and `dotnet test` succeed on the standard development environment.

## Actual Behavior

Test discovery aborted before any tests were found.
Key error text:
`You must install or update .NET to run this application.`
`Framework: 'Microsoft.NETCore.App', version '8.0.0' (x64)`
`The following frameworks were found: 10.0.5`

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet:
  ```text
  Testhost process for source(s) 'C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\bin\Debug\net8.0-windows\OpenClaw.MailBridge.Tests.dll' exited with error: You must install or update .NET to run this application.
  App: C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\bin\Debug\net8.0-windows\testhost.exe
  Framework: 'Microsoft.NETCore.App', version '8.0.0' (x64)
  The following frameworks were found:
    10.0.5 at [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  ```

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

## Suspected Cause / Notes

Project target frameworks were still pinned to `net8.0-windows` while `global.json` and the machine environment were aligned to .NET 10.
Files implicated during investigation:
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`
- `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj`
- `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj`
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`

## Proposed Fix / Validation Ideas

- [x] Unit coverage areas
- [x] Integration scenario to retest
- [x] Manual verification notes

- Retarget all MailBridge projects from `net8.0-windows` to `net10.0-windows`.
- Re-run formatter, analyzer build, nullable build, and test execution after the TFM migration.
- Re-open the workspace and confirm automatic test discovery succeeds without requiring the .NET 8 runtime.
- Confirm `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` passes.

## Acceptance Criteria

- [x] All MailBridge projects (`OpenClaw.MailBridge`, `OpenClaw.MailBridge.Contracts`, `OpenClaw.MailBridge.Client`, `OpenClaw.MailBridge.Tests`) target `net10.0-windows`.
- [x] The test project (`OpenClaw.MailBridge.Tests`) uses MSTest packages and MSTest attributes (not NUnit).
- [x] All existing test scenarios pass on `net10.0-windows` (no test removed or weakened).
- [x] The `DOTNET_*` harness isolation behavior in `CodexWebSetupScriptTests.cs` is preserved.
- [x] `csharpier .` reports no formatting changes.
- [x] `dotnet msbuild` with `EnableNETAnalyzers=true` and `EnforceCodeStyleInBuild=true` passes clean.
- [x] `dotnet msbuild` with `Nullable=enable` and `TreatWarningsAsErrors=true` passes clean.
- [x] `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal` passes all tests.
- [x] `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` passes all tests.

## Next Step

- [ ] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch