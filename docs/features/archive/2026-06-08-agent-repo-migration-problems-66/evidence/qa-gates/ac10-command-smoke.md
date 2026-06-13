# AC-10 — Corrected-Command Smoke (Issue #66)

Timestamp: 2026-06-08T10-23

## Build

Command: `dotnet build OpenClaw.MailBridge.sln`
EXIT_CODE: 0
Output Summary: Build succeeded. All nine solution projects (six production + three test) restored and
compiled. 0 Warning(s), 0 Error(s). The solution path `OpenClaw.MailBridge.sln` resolves and the
corrected build command is valid for this repository.

## Test

Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0
Output Summary: Test run succeeded. The solution and the `mailbridge.runsettings` settings path both
resolve and the corrected test command is valid for this repository. Results:
- OpenClaw.HostAdapter.Tests: 71 passed, 0 failed, 0 skipped.
- OpenClaw.Core.Tests: 51 passed, 0 failed, 0 skipped.
- OpenClaw.MailBridge.Tests: 176 passed, 0 failed, 3 skipped.
- Total: 298 passed, 0 failed, 3 skipped.
- Coverage attachments (cobertura) were produced under each test project's `TestResults/`.

AC-10 success criterion met: the named solution and runsettings paths resolve and the corrected
toolchain commands are valid for this repository. The green test run is a supporting signal. Note: the
`--collect:"XPlat Code Coverage"` data collector printed "Profiler was not initialized" for some
projects, but coverlet (configured by `mailbridge.runsettings`) still produced `coverage.cobertura.xml`
attachments; this does not affect AC-10, which is a command-validity check, not a coverage gate for
this documentation change.
