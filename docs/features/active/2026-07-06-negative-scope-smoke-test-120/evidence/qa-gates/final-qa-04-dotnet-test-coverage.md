# Final QA 04 — dotnet test with Coverage (Issue #120)

Timestamp: 2026-07-06T23-32
Command: `dotnet test --collect:"XPlat Code Coverage"` (repository root)
EXIT_CODE: 0

Output Summary:

Test results (all assemblies):
- OpenClaw.Core.Tests: Failed 0, Passed 781, Skipped 0, Total 781.
- OpenClaw.HostAdapter.Tests: Failed 0, Passed 100, Skipped 0, Total 100.
- OpenClaw.MailBridge.Tests: Failed 0, Passed 347, Skipped 5, Total 352.

Coverage — `OpenClaw.Core` package (tier-T1 module for this feature), read from the
Cobertura report
`tests/OpenClaw.Core.Tests/TestResults/2e054d31-c5ae-48b4-923f-50961d1ab4a5/coverage.cobertura.xml`:
- Line coverage: 92.82% (line-rate 0.9282).
- Branch coverage: 81.40% (branch-rate 0.8140).

Whole-report aggregate for reference: line-rate 0.9064 (90.64%), branch-rate 0.7843
(78.43%), lines-covered 5670/6255, branches-covered 1353/1725 (the aggregate also includes
OpenClaw.MailBridge.Contracts at low executable coverage, which is unrelated to this
feature).

Both `OpenClaw.Core` post-change values exceed the T1 thresholds (line >= 85%,
branch >= 75%). Test + coverage gate: PASS.
