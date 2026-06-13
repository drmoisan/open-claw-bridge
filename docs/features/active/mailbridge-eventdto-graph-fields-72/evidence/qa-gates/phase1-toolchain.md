# Phase 1 — Toolchain (Contract Changes)

Timestamp: 2026-06-13T03-08

Phase boundary: build-green and test-green confirmed in a single clean pass.

## Stage results

1. Format — `csharpier check .` → EXIT 0. Checked 150 files, 0 needing format.
2. Lint/analyzers — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` → EXIT 0, 0 warnings, 0 errors.
3. Type-check (nullable) — same build with `-p:TreatWarningsAsErrors=true` → EXIT 0, 0 warnings, 0 errors. Confirms AC1 source-compatibility: all existing positional `new EventDto(...)` call sites compile unmodified.
4. Architecture — no new ProjectReference edges; `EventSensitivityLabel` + EventDto changes are in leaf `OpenClaw.MailBridge.Contracts`; no COM added. Boundaries intact.
5. Test — `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0

Output Summary:
- Tests PASS. Failed: 0, Passed: 431 (HostAdapter 71, Core 178, MailBridge 182), Skipped: 3, Total: 434. MailBridge.Tests grew from 176 to 182 (six new EventSensitivityLabel rows/methods).
- Coverage (cobertura): MailBridge.Tests line 93.34% (897/961), branch 83.51% (233/279). Core.Tests line 88.93% (1382/1554), branch 76.23% (308/404).
- Threshold check (line >= 85%, branch >= 75%): PASS for both modules.
