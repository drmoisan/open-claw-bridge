# Baseline — Test Run with Coverage

Timestamp: 2026-07-02T11-09
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --no-build
EXIT_CODE: 0
Output Summary:
- Test results: 587 passed, 0 failed, 5 skipped, 592 total.
  - OpenClaw.HostAdapter.Tests: 100 passed / 100 total
  - OpenClaw.Core.Tests: 210 passed / 210 total
  - OpenClaw.MailBridge.Tests: 277 passed, 5 skipped / 282 total
- Pooled coverage across the three coverage.cobertura.xml reports: line 90.25% (4028/4463), branch 79.36% (911/1148).
  (Correction note 2026-07-02T11-30: an initial computation summed only the MailBridge report
  (1413/1518 line, 399/459 branch) due to a script argument-binding error; the pooled values above
  were recomputed from all three preserved raw reports. Per-report totals: Core.Tests 1502/1676
  line, 342/436 branch; MailBridge.Tests 1413/1518 line, 399/459 branch; HostAdapter.Tests
  1113/1269 line, 170/253 branch.)
- OpenClaw.Core package (targeted module, from OpenClaw.Core.Tests cobertura): line-rate 0.986 (98.60%), branch-rate 0.9168 (91.68%).
- Cobertura source paths:
  - tests/OpenClaw.Core.Tests/TestResults/34b5c3e6-181b-4dbc-bd92-d3c76a43ee4a/coverage.cobertura.xml
  - tests/OpenClaw.MailBridge.Tests/TestResults/7482ea58-6a9c-4cae-ab4a-c2f95ca1fb8a/coverage.cobertura.xml
  - tests/OpenClaw.HostAdapter.Tests/TestResults/c0fd6f6a-4b4e-428a-bece-29c35d592cde/coverage.cobertura.xml
- Raw copies preserved at artifacts/csharp/baseline-2026-07-01T22-16/ (coverage.core-tests.cobertura.xml, coverage.mailbridge-tests.cobertura.xml, coverage.hostadapter-tests.cobertura.xml).
