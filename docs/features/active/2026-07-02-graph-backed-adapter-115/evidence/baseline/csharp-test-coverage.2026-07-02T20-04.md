# Baseline — C# Tests and Coverage

Timestamp: 2026-07-02T20-04
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary:
- All tests passed. Totals: 883 passed, 0 failed, 5 skipped (888 total).
  - OpenClaw.HostAdapter.Tests: 100 passed / 0 failed / 0 skipped
  - OpenClaw.Core.Tests: 436 passed / 0 failed / 0 skipped
  - OpenClaw.MailBridge.Tests: 347 passed / 0 failed / 5 skipped (Windows/COM/publish-output tests)
- Pooled baseline coverage (sum across the three Cobertura reports):
  - Line coverage: 4540/4975 = 91.26%
  - Branch coverage: 1040/1278 = 81.38%
- Per-package baseline coverage:
  - OpenClaw.Core (Core.Tests report): line 1894/2068 = 91.59%; branch 453/552 = 82.07%
  - OpenClaw.HostAdapter: line 1113/1269 = 87.71%; branch 170/253 = 67.19%
  - OpenClaw.MailBridge: line 1533/1638 = 93.59%; branch 417/473 = 88.16%
- Raw intermediates copied to `artifacts/csharp/baseline-2026-07-02T20-04/` (coverage.core / coverage.hostadapter / coverage.mailbridge cobertura XMLs).
