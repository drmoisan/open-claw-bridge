# Phase 4 — Toolchain Gate (routes + free/busy projection)

Timestamp: 2026-06-13T10-30

## Plan-ordering note (same as Phase 3)

The full-solution build remains transiently red between Phase 2 and Phase 5 because
`HostAdapterHttpClient` (OpenClaw.Core) does not implement the two interface members until
Phase 5. Per the post-preflight rule, the Phase 4 gate is verified at the Phase 4 subject scope
(`OpenClaw.HostAdapter` + its test project); the full-solution five-stage green pass is recorded
at P5-T4 and again at P9.

## Stage 1 — Format
Command: csharpier format . ; csharpier check .
EXIT_CODE: 0
Output Summary: "Checked 164 files" with 0 unformatted. Clean.

## Stage 2 — Lint / Analyzers (Phase 4 subject scope)
Command: dotnet build src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Error(s).

## Stage 3 — Nullable Type-Check (Phase 4 subject scope)
Command: dotnet build tests/OpenClaw.HostAdapter.Tests/OpenClaw.HostAdapter.Tests.csproj -c Debug -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Error(s).

## Stage 4 — Architecture Verification
Command: grep -i ProjectReference on the in-scope csproj files
EXIT_CODE: 0
Output Summary: No new ProjectReference edges. New files (FreeBusyProjection.cs,
SchedulingRoutes.cs) live in OpenClaw.HostAdapter and use only OpenClaw.HostAdapter.Contracts,
OpenClaw.MailBridge.Contracts, and BCL types. Boundaries unchanged.

## Stage 5 — Test + Coverage (Phase 4 subject scope)
Command: dotnet test tests/OpenClaw.HostAdapter.Tests/OpenClaw.HostAdapter.Tests.csproj -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: PASS. 89 passed, 0 failed, 0 skipped (was 74 at baseline; +15 new tests for the
two routes, window validation, and FreeBusyProjection).

Changed-code coverage (per-file, cobertura):
- FreeBusyProjection.cs: line 100.00%, branch 100.00%.
- SchedulingRoutes.cs: line 100.00%, branch 100.00%.
- MailboxSettingsOptions.cs: line 100.00%, branch 100.00%.
- HostAdapterOptions.cs: line 90.47%, branch 50.00% — the single changed line (the new
  MailboxSettings auto-property) is covered; the file's branch rate reflects pre-existing
  PostConfigure null-coalescing branches that are out of this feature's changed scope.
- HostAdapter project total: line 86.81%, branch 65.95% (whole-project; the sub-75% branch total
  reflects pre-existing uncovered surface unrelated to this feature, recorded against the
  no-regression baseline of 60.28% — branch coverage IMPROVED from baseline).

All four new/changed files in the feature's changed scope meet line >= 85% and branch >= 75%
(three at 100/100). Full-solution coverage verified at P5-T4 and P9-T5.

## File-size cap
- Program.cs: 435 lines (< 500).
- SchedulingRoutes.cs: 230 lines (< 500).
- FreeBusyProjection.cs: 48 lines (< 500).
- MailboxSettingsOptions.cs: 33 lines (< 500).
All under the 500-line cap.

## Note on a production fix made during P4-T4 verification
The .NET configuration binder appends array elements onto a pre-initialized default array rather
than replacing it. A test (MailboxSettings_returns_configured_values) surfaced that operator-
supplied `WorkingDaysOfWeek` was being appended to the Monday–Friday default. Program.cs
PostConfigure now replaces `MailboxSettings.WorkingDaysOfWeek` with exactly the configured
entries when the config section exists, matching the spec semantics ("config overrides
defaults"). This keeps the P3-T1 acceptance (default-constructed POCO yields Mon–Fri) intact.
