# Fail-Before / Coverage Exception Dossier — Live-COM Send (R-3)

Timestamp: 2026-06-16T07-44
Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"`
EXIT_CODE: 0 (gated-skip; the two Integration tests report `Assert.Inconclusive` -> MSTest Skipped)

This dossier records, per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`, why the live-COM
send path (AC-06, AC-10a, AC-10b) cannot be produced as an executed pass-after / fail-before run in this
remediation environment, and why the affected criteria are recorded as **covered-by-design**, NOT as an
unconditional PASS. No attempt to run live COM was made. This complements the existing Phase 9 artifact
`evidence/regression-testing/integration-com-send.md`.

## (a) The two gated Integration tests

File: `tests/OpenClaw.MailBridge.Tests/OutlookComMailSenderIntegrationTests.cs`

1. `SendMail_with_one_recipient_should_create_a_sent_items_entry` (`[TestMethod] [TestCategory("Integration")]`)
   — On a live-Outlook host, sends a message with a unique subject to the current user's resolved SMTP
   address and asserts a matching entry appears in the Sent Items folder (`saveToSentItems: true`).
2. `SendMail_end_to_end_through_seam_should_complete_and_release_com` (`[TestMethod] [TestCategory("Integration")]`)
   — On a live-Outlook host, drives the full create -> populate -> Send -> release path through the
   `IOutlookMailSender` seam and asserts completion without throwing (COM release occurs in the sender's
   `finally`).

Both tests begin with `TryConnect()`, which returns null when the platform is non-Windows or when no
running Outlook instance is reachable via `ComActiveObject().TryGet("Outlook.Application")` on the STA
thread. When null, each test calls `Assert.Inconclusive(...)` and returns, which MSTest reports as Skipped.

## (b) The three `[ExcludeFromCodeCoverage]` live-COM members covered by these tests

File: `src/OpenClaw.MailBridge/OutlookComMailSender.cs`

1. `private void SendOnSta(object application, SendMailComRequest request)` (line ~60) —
   Justification: "Live Outlook COM send; covered by `[TestCategory("Integration")]`
   OutlookComMailSenderIntegrationTests on a live-Outlook host."
2. `private static void AddRecipients(object mailItem, IReadOnlyList<string> addresses, int type)` (line ~111) —
   Justification: "Live Outlook COM recipient resolution; covered by integration tests."
3. `private static void ReleaseRecipients(object? mailItem)` (line ~153) —
   Justification: "Live Outlook COM resolve-all on the Recipients collection; covered by integration tests."

These three members issue late-bound Outlook COM calls on the dedicated STA thread and cannot execute
without a live Outlook host. They are the members the two Integration tests above exercise by design.
This remediation added no new `[ExcludeFromCodeCoverage]` members; the three above pre-date this
remediation cycle.

## (c) Environment gating

- The Integration tests `Assert.Inconclusive` (MSTest Skipped) whenever Outlook is unavailable; they never
  fail on a host without Outlook.
- This remediation environment has no live Outlook host: `TryConnect()` returns null (no running, logged-on
  Outlook session reachable on the STA thread). The Phase 9 artifact `integration-com-send.md` records the
  gated-skip outcome (Skipped 2, Failed 0, EXIT_CODE 0).

## (d) WhyFailingRunImpossible

WhyFailingRunImpossible: The live-COM send path requires a running, logged-on Outlook host that is absent
in this remediation environment. Without it, `TryConnect()` returns null and the tests gate to
`Assert.Inconclusive` rather than executing the COM members, so the tests cannot be made to run
fail-then-pass here. These are additive pass-after tests of new behavior, not a regression of pre-existing
behavior, so there is also no prior failing state to capture.

## (e) Repo precedent for environment-gated COM / platform skips

Environment- and platform-gated test skips are an established pattern in this repository, consistent with
`.claude/rules/csharp.md` (no live Outlook in unit tests; COM confined to `OpenClaw.MailBridge`):
- `Com_active_object_create_and_logon_should_throw_on_non_windows` is gated and reports Skipped on this host
  (observed in the Phase 0 / P1-T4 test runs: 3 skipped in OpenClaw.MailBridge.Tests).
- The two `PublishOutput_*` tests are likewise environment-gated (publish-output presence) and report Skipped.
- Additional gated Integration/COM tests use `Assert.Inconclusive` / `OperatingSystem.IsWindows()` guards in
  `OutlookComMailSenderIntegrationTests.cs`, `MailBridgeRuntimeTests*.cs`, and `MsixPackageTests.cs`.

## (f) Live-run / fail-before search trail (negative-evidence)

SearchScope:
- `docs/features/active/hostadapter-sendmail-com-send-75/evidence/regression-testing/`
- `docs/features/active/hostadapter-sendmail-com-send-75/evidence/` (feature root, all kinds)
SearchPatterns: `fail-before-exception.*.md`, `*integration*`, `*live*`
SearchResult:
- `evidence/regression-testing/integration-com-send.md` (Phase 9 gated-skip record; EXIT_CODE 0, 2 Skipped).
- This dossier `evidence/regression-testing/fail-before-exception.2026-06-16T07-44.md`.
- No executed live-COM pass-after run exists (no live Outlook host available); none can be produced here.

## Criteria disposition

- AC-06 (COM send on STA; To/CC/BCC incl olBCC; release in finally; COM confined): covered-by-design via the
  two gated Integration tests and the three live-COM members above. NOT marked unconditional PASS.
- AC-10a (integration Sent Items entry verification): covered-by-design (gated-skip here). NOT unconditional PASS.
- AC-10b (COM send-path exercised end-to-end through the seam): covered-by-design (gated-skip here). NOT
  unconditional PASS.

To convert these to executed PASS, run on a live-Outlook host:
`dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"` and confirm the two tests
run (not Inconclusive) with EXIT_CODE 0.
