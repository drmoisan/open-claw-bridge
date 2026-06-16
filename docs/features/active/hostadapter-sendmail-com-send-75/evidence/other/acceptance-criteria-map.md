# Acceptance Criteria Map — Issue #75

Timestamp: 2026-06-16T09-22
AC Source: docs/features/active/hostadapter-sendmail-com-send-75/spec.md (AC-01..AC-11, prose form)
Overall verdict: PASS

Note: the spec states AC-01..AC-11 as bold prose, not markdown checkboxes; per the
acceptance-criteria-tracking skill, status is tracked here rather than by reformatting the spec.

| AC | Description | Satisfying tasks | Tests / evidence | Verdict |
|---|---|---|---|---|
| AC-01 | `SendMailAsync` declared on `IHostAdapterClient`; `SendMail*` Graph-aligned `sealed record`s in HostAdapter.Contracts | P2-T1, P2-T2, P2-T4 | `MailContractsTests` (4 tests, JSON round-trip + Graph-shape + BCC-only + default saveToSentItems); `IHostAdapterClient.SendMailAsync` compiles | PASS |
| AC-02 | `MailRoutes.cs` + `app.MapMailRoutes()`; Program.cs < 500; middleware->ready->validate->dispatch order | P8-T3, P8-T4, P8-T5 | `MailRoutes.cs` (149 lines); Program.cs = 436 lines; `HostAdapterSendMailTests` 202/400/409/502 assert ordering | PASS |
| AC-03 | Success -> 202 + `{ ok:true, data:null }` | P8-T1, P8-T3, P8-T5, P7-T2, P7-T3 | `AcceptedNoContent` factory; `SendMail_valid_request_should_return_202_with_empty_envelope`; Core `SendMailAsync_should_map_202_to_ok_true_data_null` | PASS |
| AC-04 | `HostAdapterHttpClient.SendMailAsync` POST to `users/{MailboxId}/sendMail` via `PostAsync`, token via `TokenReader` | P7-T1, P7-T2, P7-T3 | `HostAdapterHttpClientSendMailTests` (POST path; body serialization; missing-token CONFIGURATION_ERROR with no HTTP call) | PASS |
| AC-05 | `BridgeMethods.SendMail` in `All`; client `send-mail` arm; recipients as JSON arrays | P1-T1, P1-T2, P6-T1, P6-T2, P8-T2, P8-T6 | `BridgeContractsCoverageTests` (send_mail in All); `Build_WhenCommandIsSendMail_*` (2); `BuildSendMail_*` (2, JSON recipient arrays) | PASS |
| AC-06 | COM send on STA in `OutlookComMailSender`; app via `IOutlookApplicationProvider` set by `OutlookScanner`; To/CC/BCC incl olBCC; release in finally; COM confined | P3-T1..P3-T3, P4-T1, P4-T2, P5-T2, P9-T1 | `OutlookApplicationProviderTests` (3); `OutlookComMailSenderGuardTests` (2); `OutlookComMailSenderIntegrationTests` (2, gated); architecture final-architecture.md confirms COM confinement; DI registers shared singleton provider | PASS (integration covered-by-design pending live run) |
| AC-07 | Validation: >=1 recipient (D-G); contentType {Text,HTML}; empty subject allowed (D-F); `{id}` not validated (D-D); 400 INVALID_REQUEST; COM failure -> InternalError -> 502 (D-H) | P5-T1, P5-T4, P8-T3, P8-T5 | MailBridge dispatch tests (no recipients/invalid contentType -> InvalidRequest; empty subject accepted; sender throws -> InternalError); HostAdapter endpoint tests (400 no-recipient/contentType; 502 runner failure) | PASS |
| AC-08 | `saveToSentItems` defaults true; maps to `DeleteAfterSubmit = !saveToSentItems` | P4-T2, P5-T1, P5-T4, P8-T2 | `Send_mail_save_to_sent_items_should_default_to_true_when_absent`; `MailContractsTests` default-true; `BuildSendMail` default true; OutlookComMailSender sets `DeleteAfterSubmit = !SaveToSentItems` | PASS |
| AC-09 | Send-on-behalf deferred to PI-1; seam accepts future `fromEmailAddress` without breaking callers; documented | P2-T2, P4-T1, P10-T2 | `SendMailComRequest.FromEmailAddress` optional param; `IHostAdapterClient.SendMailAsync` XML doc PI-1 note; README PI-1 note | PASS |
| AC-10 | Tests cover integration Sent Items + COM path; endpoint unit test with mocked runner; Core client POST/body/token tests; RPC dispatch fake-sender tests; contract-coverage test for send_mail | P1-T2, P5-T3, P5-T4, P7-T3, P8-T5, P8-T6, P9-T1 | `FakeOutlookMailSender`; all test files above | PASS |
| AC-11 | Seven-stage toolchain passes; line>=85%, branch>=75%; no regression on changed lines; only documented `[ExcludeFromCodeCoverage]` (integration-covered); no file > 500 lines; additive contracts only | P11-T1..P11-T6; remediation R-1 split (2026-06-16) | final-format/lint/typecheck/architecture/test-coverage/coverage-delta artifacts; line 90.25% / branch 79.35%; after the R-1 remediation split every changed `.cs` file is <= 500 lines (largest changed test file `MailBridgeProgramTests.RunAsync.cs` 268; largest changed file overall `OutlookScanner.cs` 465 — see `evidence/qa-gates/remediation-file-sizes.md`); additive only | PASS |

## Suppression / file-size / additive-contract confirmation

- New suppressions: only `[ExcludeFromCodeCoverage]` on 3 live-COM-only members of `OutlookComMailSender`,
  each annotated as integration-test-covered. No new analyzer/nullable suppressions.
- No file exceeds 500 lines. The initial branch left `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs`
  at 573 lines; the R-1 remediation split (2026-06-16) divided it into three behavior-preserving partial-class
  files (`MailBridgeProgramTests.cs` 264, `MailBridgeProgramTests.RunAsync.cs` 268, `MailBridgeProgramTests.SendMail.cs` 71),
  so every changed `.cs` file is now <= 500 lines. Largest changed files after the split: OutlookScanner.cs 465;
  PipeRpcWorker.cs 438; Program.cs 436. Verified by `evidence/qa-gates/remediation-file-sizes.md`.
- All contract changes additive: BridgeMethods.SendMail added to All; new SendMail* DTOs; new
  IHostAdapterClient.SendMailAsync; new route; new RPC verb. No existing member/route/verb altered or
  removed. No major version bump.
