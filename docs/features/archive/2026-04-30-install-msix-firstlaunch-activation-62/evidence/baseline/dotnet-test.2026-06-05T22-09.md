# MSTest Baseline With Coverage (P0-T11)

Timestamp: 2026-06-05T22-09

Command: `dotnet test OpenClaw.MailBridge.sln --collect:"XPlat Code Coverage" --settings mailbridge.runsettings --results-directory artifacts/coverage-baseline`

TOOLING NOTE: Plan named `--settings tests/coverlet.runsettings`. That file is absent. The repository coverage runsettings is `mailbridge.runsettings` at repo root (the file actually wired for coverage in this tree). Used it.

EXIT_CODE: 0

Output Summary:
- OpenClaw.MailBridge.Tests: Passed 165, Skipped 3, Total 168. (Skipped: 2 publish-output env-gated tests + 1 non-Windows COM test.)
- OpenClaw.HostAdapter.Tests: Passed 71, Skipped 0.
- OpenClaw.Core.Tests: Passed 51, Skipped 0.
- All tests pass; 0 failures across the solution.
- Coverage (cobertura line-rate per coverage run):
  - MailBridge group (OpenClaw.MailBridge, .Client, .Contracts): 93.08%
  - HostAdapter group (OpenClaw.HostAdapter, .Contracts, MailBridge.Contracts): 84.99%
  - Core group (OpenClaw.Core, HostAdapter.Contracts, MailBridge.Contracts): 80.65%
- The Phase 2 C# change is to a test file (`MsixPackageTests.cs`) and the manifest (XML), neither of which is in the C# production coverage denominator; the MailBridge production coverage baseline (93.08%) is the no-regression reference for P8.
