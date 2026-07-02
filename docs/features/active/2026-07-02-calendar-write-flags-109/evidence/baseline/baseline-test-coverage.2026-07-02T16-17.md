# Baseline — Test and Coverage

Timestamp: 2026-07-02T16-17
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp
EXIT_CODE: 0

Raw Cobertura files (artifacts/csharp/):
- OpenClaw.Core.Tests → `artifacts/csharp/d7cde8c7-5b88-484b-8831-5ce1586dd13e/coverage.cobertura.xml`
- OpenClaw.HostAdapter.Tests → `artifacts/csharp/c081d4a4-7a3c-49a7-845c-819da868146e/coverage.cobertura.xml`
- OpenClaw.MailBridge.Tests → `artifacts/csharp/9a77f964-ff54-448b-bf6f-3698a5e72054/coverage.cobertura.xml`

Output Summary:
- Test results: Failed: 0, Passed: 807, Skipped: 5, Total: 812 (Core.Tests 360/360 passed; HostAdapter.Tests 100/100 passed; MailBridge.Tests 347 passed / 5 skipped platform-gated).
- OpenClaw.Core coverage (module in scope): line 90.97% (1753/1927), branch 80.81% (417/516).
- OpenClaw.HostAdapter coverage: line 87.70% (1113/1269), branch 67.19% (170/253) — pre-existing baseline value.
- OpenClaw.MailBridge coverage: line 93.58% (1533/1638), branch 88.16% (417/473).
- Aggregate (summed across the three Cobertura reports): line 91.00% (4399/4834), branch 80.84% (1004/1242).
