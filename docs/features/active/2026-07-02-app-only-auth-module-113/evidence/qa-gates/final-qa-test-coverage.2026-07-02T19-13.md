# Final QA — Test and Coverage

Timestamp: 2026-07-02T19-13
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/postchange-113
EXIT_CODE: 0
Output Summary:
- All test projects passed in the same clean pass as format and build. OpenClaw.Core.Tests: 436 passed / 0 failed / 0 skipped (377 baseline + 59 new CloudAuth). OpenClaw.HostAdapter.Tests: 100 passed / 0 failed. OpenClaw.MailBridge.Tests: 347 passed / 5 skipped (same pre-existing COM/publish environment guards as baseline).
- Post-change coverage, Core.Tests run (Cobertura root): line 91.58% (1894/2068), branch 82.06% (453/552).
- Post-change pooled across all three runs: line 91.26% (4540/4975), branch 81.38% (1040/1278).
- Thresholds satisfied: line >= 85%, branch >= 75%.

Raw Cobertura files (under artifacts/csharp/):
- artifacts/csharp/postchange-113/d23b82bd-3144-4e2e-bb89-c0f7b0ea8743/coverage.cobertura.xml (OpenClaw.Core.Tests run — authoritative for OpenClaw.Core / CloudAuth)
- artifacts/csharp/postchange-113/ebf98c71-7b9b-44d6-963e-88f9985bd3ab/coverage.cobertura.xml (OpenClaw.HostAdapter.Tests run)
- artifacts/csharp/postchange-113/002c737a-7d7d-446e-b07f-f02fd55ff7c6/coverage.cobertura.xml (OpenClaw.MailBridge.Tests run)
