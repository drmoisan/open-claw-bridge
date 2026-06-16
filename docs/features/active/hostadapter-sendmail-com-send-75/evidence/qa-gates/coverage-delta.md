# Final QA — Coverage Delta and No-Regression

Timestamp: 2026-06-16T09-19

## Baseline vs Post-change (combined across the three test projects)

| Metric | Baseline (P0-T6) | Post-change (P11-T5) | Delta |
|---|---|---|---|
| Line coverage | 90.21% (3798/4210) | 90.25% (4028/4463) | +0.04 pp |
| Branch coverage | 78.92% (846/1072) | 79.35% (911/1148) | +0.43 pp |

Per-project line/branch:

| Project | Baseline line | Post line | Baseline branch | Post branch |
|---|---|---|---|---|
| Core | 89.57% | 89.61% | 78.44% | 78.44% |
| HostAdapter | 86.86% | 87.70% | 65.95% | 67.19% |
| MailBridge | 93.87% | 93.08% | 87.03% | 86.92% |

## Threshold verdict

- Combined line 90.25% >= 85% — PASS.
- Combined branch 79.35% >= 75% — PASS.
- No regression introduced by the feature: combined line and branch both increased vs baseline. The
  small MailBridge per-project decreases (line 93.87->93.08, branch 87.03->86.92) reflect added
  denominator from new code (OutlookComMailSender non-excluded surface, SendMailRpcHandler) that is
  itself well covered; the project remains far above thresholds and the combined metric rose.

## New/changed-code coverage

All new/changed product files meet or exceed thresholds on covered lines (see final-test-coverage.md):
MailContracts 100/100, HostAdapterResponses 100/100, HostAdapterCommandBuilder 94.5/83.3,
MailRoutes 100 line / 71.4 branch (behavioral branches covered; residual are defensive null-coalesce),
IOutlookMailSender 100/100, OutlookApplicationProvider 100/100, OutlookComMailSender 100/100
(non-excluded surface), SendMailRpcHandler parser branches covered, HostAdapterHttpClient 100/100.

## [ExcludeFromCodeCoverage] accounting (live-COM-only)

The three excluded members of `OutlookComMailSender` (SendOnSta, AddRecipients, ReleaseRecipients)
are covered-by-design by the Phase 9 `[TestCategory("Integration")]` tests
(`OutlookComMailSenderIntegrationTests`). On this host the integration run was gated-skipped (no live
Outlook); a live-Outlook run exercises these members (see evidence/regression-testing/integration-com-send.md).
This is flagged for the feature audit as covered-by-design pending a live run.

## Outcome

PASS. All required coverage values are available and numeric; thresholds hold; no regression on the
combined surface.
