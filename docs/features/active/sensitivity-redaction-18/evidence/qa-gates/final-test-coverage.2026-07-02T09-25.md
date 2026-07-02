# Final QA Gate — dotnet test with XPlat Code Coverage (P5-T3)

Timestamp: 2026-07-02T09-25
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0
Output Summary:
- Tests: 652 total — 647 passed, 0 failed, 5 skipped (same Windows/COM- and publish-gated skips as baseline).
  - OpenClaw.MailBridge.Tests: 334 passed, 5 skipped (339 total; +51 tests versus the 288-test baseline)
  - OpenClaw.Core.Tests: 213 passed
  - OpenClaw.HostAdapter.Tests: 100 passed
- Post-change coverage (Cobertura, numeric):
  - OpenClaw.MailBridge package: line 93.58% (1533/1638), branch 87.31% (413/473)
  - OpenClaw.Core package: line 89.62% (1503/1677), branch 78.44% (342/436)
  - OpenClaw.HostAdapter package: line 87.70% (1113/1269), branch 67.19% (170/253)
  - Pooled (all three reports): line 90.51% (4149/4584), branch 79.60% (925/1162)
- Raw Cobertura reports staged (non-evidence intermediates) at `artifacts/csharp/post-change-final/coverage.{mailbridge,core,hostadapter}.cobertura.xml`.
- `git status --porcelain` (excluding transient TestResults) identical before and after the run — part of the final consecutive clean pass at 2026-07-02T09-25.
