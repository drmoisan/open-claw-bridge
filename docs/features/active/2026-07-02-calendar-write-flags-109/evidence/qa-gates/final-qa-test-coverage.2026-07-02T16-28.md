# Final QA — Test and Coverage

Timestamp: 2026-07-02T16-28
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp
EXIT_CODE: 0

Raw Cobertura files (artifacts/csharp/):
- OpenClaw.Core.Tests → `artifacts/csharp/8c96ad60-49d1-47f1-9c9d-cbadd02f4d66/coverage.cobertura.xml`
- OpenClaw.HostAdapter.Tests → `artifacts/csharp/4a67323f-a51e-4665-b22e-2b9334fc7ef9/coverage.cobertura.xml`
- OpenClaw.MailBridge.Tests → `artifacts/csharp/343c8e25-c41b-419f-a581-06c80922ef6f/coverage.cobertura.xml`

Output Summary:
- Test results: Failed: 0, Passed: 824, Skipped: 5, Total: 829 (Core.Tests 377/377 passed — up 17 from the 360 baseline, matching the 17 new tests; HostAdapter.Tests 100/100; MailBridge.Tests 347 passed / 5 skipped platform-gated). Suite includes architecture-boundary (AgentArchitectureBoundaryTests) and contract (SchedulingDtoContractTests) tests.
- OpenClaw.Core coverage (module in scope): line 91.00% (1761/1935), branch 80.96% (421/520).
- OpenClaw.HostAdapter coverage: line 87.70% (1113/1269), branch 67.19% (170/253) — unchanged from baseline.
- OpenClaw.MailBridge coverage: line 93.58% (1533/1638), branch 88.16% (417/473) — unchanged from baseline.
- Aggregate (summed across the three Cobertura reports): line 91.02% (4407/4842), branch 80.90% (1008/1246).
- New file `CalendarWritePolicy.cs`: line-rate 1.0, branch-rate 1.0 (8/8 instrumented lines covered, 0 missed).
