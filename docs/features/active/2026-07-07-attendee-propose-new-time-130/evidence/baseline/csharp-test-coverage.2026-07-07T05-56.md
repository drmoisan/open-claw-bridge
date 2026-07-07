# Baseline — C# Test / Architecture-Boundary / Coverage

Timestamp: 2026-07-07T05-56
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline-2026-07-07T05-56`
EXIT_CODE: 0

Output Summary:
- Tests: all pass. Failed: 0. Passed: 1340 (OpenClaw.HostAdapter.Tests 100; OpenClaw.Core.Tests 893; OpenClaw.MailBridge.Tests 347). Skipped: 5 (COM/publish host-bound tests, expected). Total run: 1345.
- Architecture-boundary (NetArchTest) suites are part of OpenClaw.Core.Tests and passed within the 893.
- Coverage (Cobertura), OpenClaw.Core package (the T1 module this feature touches): line-rate 0.9927 (99.27%), branch-rate 0.9224 (92.24%).
- Coverage (Cobertura), OpenClaw.Core.Tests run overall (Core plus referenced assemblies in scope): line-rate 0.9513 (95.13%), branch-rate 0.8645 (86.45%).
- Baseline is well above the uniform thresholds (line >= 85%, branch >= 75%).

Raw intermediates: `artifacts/csharp/baseline-2026-07-07T05-56/` (TRX not emitted by default; three `coverage.cobertura.xml` files plus one binary `.coverage`). The OpenClaw.Core package figures come from `640df224-5a58-434b-86f3-4309587d9384/coverage.cobertura.xml`.
