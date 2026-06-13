# Phase 7 — Toolchain Gate (contract round-trip + cross-surface regression)

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
Output Summary: PASS. 498 passed, 0 failed, 3 skipped.
- OpenClaw.HostAdapter.Tests: 89 passed.
- OpenClaw.Core.Tests: 193 passed (was 191; +2 ApiEnvelope<MailboxSettingsDto> and
  ApiEnvelope<FreeBusyScheduleDto> round-trip tests).
- OpenClaw.MailBridge.Tests: 216 passed, 3 skipped.

Coverage (cobertura):
- Core: line 89.17%, branch 77.59%.
- HostAdapter: line 86.81%, branch 65.95% (whole-project; improved from 60.28% baseline).

The two new envelope round-trip tests (SchedulingDtoContractTests) assert the outer
ApiEnvelope (Ok, Data, Meta, Error) plus the inner DTO fields/intervals are preserved.
