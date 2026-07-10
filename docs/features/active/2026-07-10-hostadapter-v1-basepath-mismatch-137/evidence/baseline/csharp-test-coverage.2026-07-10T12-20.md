Timestamp: 2026-07-10T12-20

Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline

EXIT_CODE: 0

Output Summary:
- OpenClaw.HostAdapter.Tests: Passed 100, Failed 0, Skipped 0, Total 100.
- OpenClaw.Core.Tests: Passed 930, Failed 0, Skipped 0, Total 930.
- OpenClaw.MailBridge.Tests: Passed 347, Failed 0, Skipped 5, Total 352.
- Cobertura reports generated under `artifacts/csharp/baseline/*/coverage.cobertura.xml` (raw, non-evidence intermediates per plan convention).
- OpenClaw.Core.Tests run cobertura (`8b6ba149-.../coverage.cobertura.xml`) package-level coverage: `OpenClaw.Core` line-rate = 99.29%, branch-rate = 92.28%.
- Class-level baseline for the file to be changed: `OpenClaw.Core\Program.cs` line-rate = 100%, branch-rate = 100% (baseline, pre-fix).
- Other project cobertura (informational, not the target of this plan's coverage gate): `OpenClaw.MailBridge` (7699e263 run) line-rate = 93.58%, branch-rate = 88.16%; `OpenClaw.HostAdapter` package-level rate within the f181438d run (which also aggregates `OpenClaw.HostAdapter.Contracts` and a partial `OpenClaw.MailBridge.Contracts`) line-rate = 98.64%, branch-rate = 89.47% (corrected from an initial mislabeling that reported the run's blended root rate instead of the `OpenClaw.HostAdapter` package-specific rate).
