# Remediation Baseline — dotnet test with coverage

Timestamp: 2026-07-02T09-58
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "artifacts/csharp/remediation-baseline-2026-07-02T09-58"`
EXIT_CODE: 0
Output Summary:

- Tests: 652 total — 647 passed, 0 failed, 5 skipped (environment-gated COM/publish skips).
  - OpenClaw.MailBridge.Tests: 334 passed, 5 skipped (339 total)
  - OpenClaw.Core.Tests: 213 passed
  - OpenClaw.HostAdapter.Tests: 100 passed
- Pooled coverage (3 Cobertura reports, summed root counters):
  - Line: 90.51% (4149/4584)
  - Branch: 79.60% (925/1162)
  - Matches the reviewer reference values (90.51% line / 79.60% branch) exactly.
- Per-file `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` (MailBridge Cobertura, per-line max across duplicate class entries):
  - Line: 100.00% (109/109)
  - Branch: 71.43% (10/14) — reproduces the Blocking finding (< 75% gate); remediation starting point confirmed.
  - Branch detail: line 25 = 4/4, line 63 = 3/6, line 165 = 2/2, line 170 = 1/2.
- Raw Cobertura reports staged under `artifacts/csharp/remediation-baseline-2026-07-02T09-58/<guid>/coverage.cobertura.xml`.
