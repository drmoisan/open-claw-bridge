# Baseline — C# Test + Coverage (Issue #70)

Timestamp: 2026-06-09T12-32

Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

EXIT_CODE: 0

Output Summary: PASS. Test totals across the solution: 298 passed, 0 failed, 3 skipped.
- OpenClaw.HostAdapter.Tests: 71 passed, 0 failed.
- OpenClaw.Core.Tests: 51 passed, 0 failed.
- OpenClaw.MailBridge.Tests: 176 passed, 0 failed, 3 skipped (2 publish-output tests requiring published artifacts, 1 non-Windows COM guard).

Coverage headline (cobertura, `OpenClaw.Core.Tests` run, per-package `OpenClaw.Core`):
- OpenClaw.Core package line coverage: 99.46% (line-rate 0.9946).
- OpenClaw.Core package branch coverage: 89.28% (branch-rate 0.8928).

Baseline reference for the no-regression gate: the agent code added by this feature is folded into the `OpenClaw.Core` package; the post-change `OpenClaw.Core` coverage in Phase 7 (P7-T4/P7-T5) is compared against this baseline. The aggregate Core.Tests cobertura totals (line-rate 0.8065, branch-rate 0.5357) include the partially-covered `OpenClaw.MailBridge.Contracts` package pulled in transitively; the authoritative agent-application figure is the `OpenClaw.Core` package value above.
