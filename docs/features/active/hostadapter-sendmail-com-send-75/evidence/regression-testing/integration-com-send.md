# Phase 9 — COM Send Integration Tests (gated)

Timestamp: 2026-06-16T08-58
Command: dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"
EXIT_CODE: 0

## Output Summary

Gated-skip outcome (live Outlook unavailable on this host):
- Skipped: SendMail_with_one_recipient_should_create_a_sent_items_entry
- Skipped: SendMail_end_to_end_through_seam_should_complete_and_release_com
- Result: Skipped! Failed 0, Passed 0, Skipped 2, Total 2. The suite does not fail.

Gating reason: the tests call `Assert.Inconclusive(...)` when Outlook is not available. On this host
`TryConnect()` returns null because there is no running Outlook instance reachable via
`ComActiveObject().TryGet("Outlook.Application")` on the STA thread (CI/dev host without a logged-on
Outlook session). MSTest reports an inconclusive test as Skipped.

## Coverage-by-design note (live-COM members)

The `[ExcludeFromCodeCoverage]` live-COM-only members of `OutlookComMailSender`
(`SendOnSta`, `AddRecipients`, `ReleaseRecipients`) are designed to be exercised by these
integration tests on a live-Outlook host. Because the run was gated-skipped here, those members
remain covered-by-design pending a live run. This is flagged for the feature audit:
- On a live-Outlook host, run `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"`;
  the tests will (a) send to the current user's SMTP and verify a Sent Items entry with the unique
  subject, and (b) complete the end-to-end seam send including COM release.

## Negative-evidence audit trail (fail-before not applicable)

These are pass-after integration tests of new behavior, not a fail-before regression. No fail-before
artifact is required.
- SearchScope: docs/features/active/hostadapter-sendmail-com-send-75/evidence/regression-testing/
- SearchPatterns: fail-before-exception.*.md
- SearchResult: none (not applicable; additive feature, no pre-existing failing behavior to capture)
