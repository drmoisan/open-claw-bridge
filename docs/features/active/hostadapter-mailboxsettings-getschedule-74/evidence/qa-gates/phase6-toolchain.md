# Phase 6 — Toolchain Gate (HostAdapterSchedulingService delegation)

Timestamp: 2026-06-13T10-30

## Stage 1 — Format
Command: csharpier format . ; csharpier check .
EXIT_CODE: 0
Output Summary: "Checked 164 files" with 0 unformatted. Clean.

## Stage 2 — Lint / Analyzers
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Error(s).

## Stage 3 — Nullable Type-Check
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Error(s).

## Stage 4 — Architecture Verification
Command: grep -i ProjectReference on the in-scope csproj files
EXIT_CODE: 0
Output Summary: No new ProjectReference edges.

## Stage 5 — Test + Coverage
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: PASS. 496 passed, 0 failed, 3 skipped.
- OpenClaw.HostAdapter.Tests: 89 passed.
- OpenClaw.Core.Tests: 191 passed (was 189; the two NotSupportedException expectation tests were
  replaced by four delegation + failure-path tests, net +2).
- OpenClaw.MailBridge.Tests: 216 passed, 3 skipped.

Changed-code coverage:
- HostAdapterSchedulingService.cs: line 100.00%, branch 100.00%.
- Core project total: line 89.17%, branch 77.59%.

## Stub-removal confirmation
- `GetMailboxSettingsAsync` NotSupportedException stub removed; now delegates to
  `IHostAdapterClient.GetMailboxSettingsAsync` and returns documented defaults on non-Ok envelope.
- `GetFreeBusyAsync` NotSupportedException stub removed; now delegates to
  `IHostAdapterClient.GetFreeBusyAsync` and returns empty BusyIntervals on non-Ok envelope.
- `SendMailAsync` NotSupportedException stub UNCHANGED (deferred to #75); the SendMailAsync_Throws
  test remains and passes.
