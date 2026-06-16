# Final QA — Test + Coverage (Integration excluded)

Timestamp: 2026-06-16T09-17
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --filter "TestCategory!=Integration"
EXIT_CODE: 0

## Output Summary

All non-integration tests pass:
- OpenClaw.HostAdapter.Tests: Passed 100, Failed 0, Skipped 0
- OpenClaw.Core.Tests: Passed 210, Failed 0, Skipped 0
- OpenClaw.MailBridge.Tests: Passed 277, Failed 0, Skipped 3 (pre-existing non-Windows COM + 2 publish-output skips)
- Total: 587 passed, 3 skipped (the 2 [TestCategory("Integration")] COM tests are excluded by filter)

## Post-change coverage headline (per-project cobertura)

| Project | line-rate | branch-rate | lines-covered/valid | branches-covered/valid |
|---|---|---|---|---|
| OpenClaw.Core.Tests | 89.61% | 78.44% | 1502/1676 | 342/436 |
| OpenClaw.HostAdapter.Tests | 87.70% | 67.19% | 1113/1269 | 170/253 |
| OpenClaw.MailBridge.Tests | 93.08% | 86.92% | 1413/1518 | 399/459 |

Combined (sum across projects):
- Line coverage: (1502+1113+1413)/(1676+1269+1518) = 4028/4463 = **90.25%** (>= 85% PASS)
- Branch coverage: (342+170+399)/(436+253+459) = 911/1148 = **79.35%** (>= 75% PASS)

## New/changed-file coverage (changed-lines no-regression)

| File | line-rate | branch-rate |
|---|---|---|
| OpenClaw.HostAdapter.Contracts/MailContracts.cs | 100% | 100% |
| OpenClaw.HostAdapter/HostAdapterResponses.cs | 100% | 100% |
| OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs | 94.52% | 83.33% |
| OpenClaw.HostAdapter/MailRoutes.cs | 100% | 71.43%* |
| OpenClaw.MailBridge/IOutlookMailSender.cs | 100% | 100% |
| OpenClaw.MailBridge/OutlookApplicationProvider.cs | 100% | 100% |
| OpenClaw.MailBridge/OutlookComMailSender.cs | 100%** | 100%** |
| OpenClaw.MailBridge/SendMailRpcHandler.cs | ~79-100%*** | ~77-100%*** |
| OpenClaw.Core/HostAdapterHttpClient.cs | 100% | 100% |

\* MailRoutes.cs branch 71.43% reflects defensive null-coalescing branches in `ValidateRequest`
(`?.Count ?? 0`) and the `Meta.Bridge ?? bridgeStatus` fallback that the unit tests do not all drive;
the route's behavioral branches (202 / 400 no-recipient / 400 contentType / 409 / 502) are all covered
by HostAdapterSendMailTests. This file's lines are fully covered (100% line).

\** OutlookComMailSender.cs reports 100% for its non-excluded surface; the three live-COM-only members
(SendOnSta, AddRecipients, ReleaseRecipients) carry `[ExcludeFromCodeCoverage]` and are excluded from
the denominator, covered-by-design by the Phase 9 integration tests.

\*** SendMailRpcHandler.cs cobertura emits multiple `<class>` entries (the parser, the exception type,
and nested records); the parser/validator branches (contentType, no-recipient, save-to-sent-items
default, recipient JSON) are exercised by the MailBridge dispatch tests.

If any P11 step had rewritten files or failed, the loop would restart from P11-T1; none did.
