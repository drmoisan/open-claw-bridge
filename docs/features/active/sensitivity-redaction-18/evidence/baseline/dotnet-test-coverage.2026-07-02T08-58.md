# Baseline — dotnet test with XPlat Code Coverage

Timestamp: 2026-07-02T08-58
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
EXIT_CODE: 0
Output Summary:
- Tests: 601 total — 596 passed, 0 failed, 5 skipped (Windows/COM- and publish-gated skips: `Com_active_object_create_and_logon_should_throw_on_non_windows`, `PublishOutput_*` x2, `SendMail_*` x2).
  - OpenClaw.MailBridge.Tests: 283 passed, 5 skipped (288 total)
  - OpenClaw.Core.Tests: 213 passed
  - OpenClaw.HostAdapter.Tests: 100 passed
- Baseline coverage (Cobertura, numeric):
  - OpenClaw.MailBridge package: line 93.08% (1413/1518), branch 86.92% (399/459)
  - OpenClaw.Core package: line 89.62% (1503/1677), branch 78.44% (342/436)
  - OpenClaw.HostAdapter package: line 87.70% (1113/1269), branch 67.19% (170/253)
  - Pooled (all three reports): line 90.26% (4029/4464), branch 79.36% (911/1148)
- Raw Cobertura reports staged (non-evidence intermediates) at `artifacts/csharp/baseline/coverage.{mailbridge,core,hostadapter}.cobertura.xml`.
