# Final QA Gate — Tests and Coverage

Timestamp: 2026-07-02T12-16
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final-101` (repo root)
EXIT_CODE: 0

Output Summary:
- Test results (all pass, 0 failed): OpenClaw.HostAdapter.Tests 100 passed; OpenClaw.Core.Tests 254 passed (baseline 224 + 30 new results: 11 SentActionKey unit results (2 named + 3 parameterized x 3 rows), 3 SentActionKey property, 11 CoreCacheRepositorySentActions results (7 named + 1 malformed-key negative test x 4 rows), 5 SchedulingWorkerDedupe); OpenClaw.MailBridge.Tests 347 passed / 5 skipped (Windows-COM/publish integration skips, unchanged from baseline). Totals: 701 passed, 0 failed, 5 skipped.
- The run includes the architecture-boundary tests (`AgentArchitectureBoundaryTests`), unit tests, and CsCheck property tests.
- Coverage (Cobertura reports under `artifacts/csharp/final-101/`):
  - Per-report: Core.Tests run (34a5ac0b): line 89.97% (1561/1735), branch 79.29% (360/454); MailBridge.Tests run (8622e903): line 93.58% (1533/1638), branch 88.16% (417/473); HostAdapter.Tests run (a3f48c58): line 87.70% (1113/1269), branch 67.19% (170/253)
  - Pooled (summed across the three reports): line coverage 90.63% (4207/4642), branch coverage 80.25% (947/1180)
  - `OpenClaw.Core` package (Core.Tests report): line 98.66%, branch 92.07%
- Verdict: pooled line 90.63% >= 85% and pooled branch 80.25% >= 75% — thresholds satisfied.

Loop note: the first Phase 5 iteration surfaced uncovered lines (the malformed-key throw path in `CoreCacheRepository.SentActions.cs`); a negative-flow test (`RecordAsync_malformed_key_should_throw_ArgumentException`, 4 data rows) was added and the loop restarted from the formatting gate. This artifact records the clean single pass.

Raw artifacts: `artifacts/csharp/final-101/34a5ac0b-.../coverage.cobertura.xml`, `.../8622e903-...`, `.../a3f48c58-...`
