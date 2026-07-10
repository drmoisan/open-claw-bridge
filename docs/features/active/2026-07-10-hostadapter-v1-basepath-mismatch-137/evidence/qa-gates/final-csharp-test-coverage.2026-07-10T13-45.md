Timestamp: 2026-07-10T13-45

Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final

EXIT_CODE: 0

Output Summary:
- OpenClaw.HostAdapter.Tests: Passed 100, Failed 0, Skipped 0, Total 100 (unchanged from baseline).
- OpenClaw.Core.Tests: Passed 931, Failed 0, Skipped 0, Total 931 (up from baseline's 930 — includes the new `CoreHostAdapterBaseUrlFallbackTests.BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment`, which now passes).
- OpenClaw.MailBridge.Tests: Passed 347, Failed 0, Skipped 5, Total 352 (unchanged from baseline).
- Cobertura reports generated under `artifacts/csharp/final/*/coverage.cobertura.xml`.
- OpenClaw.Core.Tests run cobertura (`97e2d146-.../coverage.cobertura.xml`) package-level coverage: `OpenClaw.Core` line-rate = 99.29%, branch-rate = 92.28% — unchanged from the 99.29%/92.28% baseline (no regression).
- Class-level post-fix coverage for the changed file: `OpenClaw.Core\Program.cs` line-rate = 100%, branch-rate = 100% (unchanged from the 100%/100% baseline).
- Other project cobertura (informational): `OpenClaw.MailBridge` line-rate = 93.58%, branch-rate = 88.16%; `OpenClaw.HostAdapter` line-rate = 98.64%, branch-rate = 89.47% (both unchanged/consistent with baseline; neither project was touched by this plan).
