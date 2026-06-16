# Feature Audit: hostadapter-sendmail-com-send (#75)

**Audit Date:** 2026-06-16
**Feature Folder:** `docs/features/active/hostadapter-sendmail-com-send-75`
**Base Branch:** `main`
**Head Branch:** `feature/hostadapter-sendmail-com-send-75`
**Work Mode:** `full-feature`
**Audit Type:** Post-remediation acceptance verification (remediation cycle 1)

---

## Scope and Baseline

- **Base branch:** `main` (commit `0cb7de6`)
- **Head branch/commit:** `feature/hostadapter-sendmail-com-send-75` (commit `4f8ecce`)
- **Merge base:** `0cb7de6`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/**` (including `qa-gates/remediation-*.md`, `remediation-baseline/*.md`, `regression-testing/fail-before-exception.2026-06-16T07-44.md`, `other/acceptance-criteria-map.md`)
  - Additional evidence: direct diff inspection (`git diff 0cb7de6..4f8ecce`), `wc -l` and `grep` over the changed `.cs` set
- **Feature folder used:** `docs/features/active/hostadapter-sendmail-com-send-75`
- **Requirements source:** `spec.md` (AC-01..AC-11) and `user-story.md` (per full-feature work mode)
- **Work mode resolution note:** `issue.md` is absent from the feature folder, so the work-mode marker could not be read from it. Per the fail-closed rule in `feature-review-workflow`, work mode defaults to `full-feature` (sources: `spec.md` and `user-story.md`). The caller also confirmed `full-feature`. The persisted `- Work Mode: full-feature` marker in `plan.2026-06-16T06-27.md` is consistent with this resolution.
- **Scope note:** This is a post-remediation re-audit over the full branch diff `0cb7de6..HEAD`, not the remediation subset. The remediation cycle 1 commit (`4f8ecce`) changed only test source (the `MailBridgeProgramTests` partial-class split) and documentation/evidence; the feature implementation is in commit `269d3bb`. `user-story.md` contains only placeholder template text (no concrete story or criteria authored); the authoritative criteria for evaluation are the AC-01..AC-11 prose items in `spec.md`.

---

## Acceptance Criteria Inventory

**Instructions note:** `spec.md` states AC-01..AC-11 as bold prose, not markdown checkboxes; they are transcribed faithfully below. No checkbox source exists for direct check-off (see Acceptance Criteria Check-off). `user-story.md` contains unfilled placeholder criteria (`- [ ] Criterion 1/2/3`) and no authored content, so it provides no evaluable acceptance criteria.

**Authoritative AC source files for this run:**
- `docs/features/active/hostadapter-sendmail-com-send-75/spec.md` — primary (AC-01..AC-11, prose)
- `docs/features/active/hostadapter-sendmail-com-send-75/user-story.md` — secondary; placeholder-only, no evaluable criteria

### Acceptance criteria

#### From spec.md

1. **AC-01:** `IHostAdapterClient` declares `SendMailAsync(SendMailRequest, string? requestId = null, CancellationToken = default)` returning `Task<ApiEnvelope<object?>>`; the `SendMail*` DTOs are Graph-aligned `sealed record`s in `OpenClaw.HostAdapter.Contracts`.
2. **AC-02:** The HostAdapter registers `POST /users/{assistantMailbox}/sendMail` in a new `MailRoutes.cs` via `app.MapMailRoutes()`; `Program.cs` remains under 500 lines; the `BearerTokenMiddleware` → `RequireReadyBridgeAsync` → validate → dispatch order is preserved.
3. **AC-03:** A successful send returns 202 Accepted with `ApiEnvelope<object?>` (`ok: true`, `data: null`) (D-A).
4. **AC-04:** `HostAdapterHttpClient.SendMailAsync` issues an HTTP POST to `users/{MailboxId}/sendMail` with the JSON body via a new `PostAsync` helper, obtaining the token through the existing `TokenReader` seam.
5. **AC-05:** `BridgeMethods.SendMail = "send_mail"` is added to `BridgeMethods.All`; the MailBridge client `Build` switch gains a `"send-mail"` arm; recipients are passed as JSON-serialized arrays per recipient type (D-C).
6. **AC-06:** COM send runs on the STA thread in `OutlookComMailSender : IOutlookMailSender`, obtaining the `Application` via `IOutlookApplicationProvider` set by `OutlookScanner` (D-E); it sets `Subject`, body (`HTMLBody` for `HTML`, else `Body`), To/CC/BCC recipients including BCC via `Recipients.Add(...).Type=olBCC` (D-I), `DeleteAfterSubmit = !saveToSentItems`, calls `Send()`, and releases all COM objects in `finally`. COM remains confined to `OpenClaw.MailBridge`.
7. **AC-07:** Validation: ≥1 recipient across To/CC/BCC combined is required (D-G); `contentType` must be `Text` or `HTML` case-insensitive; empty subject is permitted (D-F); `{assistantMailbox}` is not validated against the local profile (D-D); validation failures return 400 `INVALID_REQUEST`; COM send failure maps to `BridgeErrorCodes.InternalError` → HTTP 502 (D-H).
8. **AC-08:** `saveToSentItems` defaults to `true` when absent; `true` saves the message to Sent Items (`DeleteAfterSubmit = false`), `false` does not.
9. **AC-09:** Send-on-behalf is deferred to PI-1; the `IOutlookMailSender` seam accepts a future `fromEmailAddress` without breaking existing callers, and this deferral is documented.
10. **AC-10:** Tests cover: (a) an integration test [real COM] where a valid `SendMailRequest` produces a Sent Items entry; (b) an integration test validating the COM send path; (c) a unit test validating the endpoint with a mocked runner; plus Core client unit tests (POST shape/body/token-missing), MailBridge RPC-dispatch unit tests using a `FakeOutlookMailSender` (success + failure + invalid-request), and a contract-coverage test asserting `BridgeMethods.All` contains `send_mail`.
11. **AC-11:** The full seven-stage toolchain passes; line coverage ≥ 85% and branch coverage ≥ 75%; no regression on changed lines; no new analyzer/nullable suppressions except documented `[ExcludeFromCodeCoverage]` on live-COM-only members (each covered by the integration test); no file exceeds 500 lines; all contract changes are additive (no breaking changes).

#### From user-story.md

- No evaluable acceptance criteria. The file holds unfilled template placeholders (`- [ ] Criterion 1`, `Criterion 2`, `Criterion 3`) and no authored story content.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-01 | `SendMailAsync` on `IHostAdapterClient`; Graph-aligned `sealed record` DTOs | PASS | `src/OpenClaw.HostAdapter.Contracts/{IHostAdapterClient,MailContracts}.cs`; `MailContractsTests` (JSON round-trip, Graph-shape, BCC-only, default saveToSentItems) | `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory!=Integration"` | Five `public sealed record` DTOs; method signature matches spec. |
| AC-02 | `MailRoutes.cs` + `app.MapMailRoutes()`; `Program.cs` < 500; middleware order preserved | PASS | `MailRoutes.cs` (149 lines); `Program.cs` 436 lines (`wc -l`); `HostAdapterSendMailTests` assert 202/400/409/502 ordering | `wc -l src/OpenClaw.HostAdapter/Program.cs` | Order BearerToken → RequireReady → validate → dispatch preserved. |
| AC-03 | 202 + `{ ok:true, data:null }` | PASS | `AcceptedNoContent` factory in `HostAdapterResponses`; `SendMail_valid_request_should_return_202_with_empty_envelope`; Core `SendMailAsync_should_map_202_to_ok_true_data_null` | `dotnet test ... --filter "TestCategory!=Integration"` | Unit-verified. |
| AC-04 | `SendMailAsync` POST to `users/{MailboxId}/sendMail` via `PostAsync`, token via `TokenReader` | PASS | `HostAdapterHttpClientSendMailTests` (POST path; body serialization; missing-token CONFIGURATION_ERROR, no HTTP call) | `dotnet test ... --filter "TestCategory!=Integration"` | 100% line coverage on the new client path. |
| AC-05 | `BridgeMethods.SendMail` in `All`; client `send-mail` arm; recipients as JSON arrays | PASS | `BridgeContractsCoverageTests` (`send_mail` in `All`); `Build_WhenCommandIsSendMail_*` (2); `BuildSendMail_*` (2, JSON recipient arrays) | `dotnet test ... --filter "TestCategory!=Integration"` | Additive verb. |
| AC-06 | COM send on STA; app via provider; To/CC/BCC incl olBCC; release in finally; COM confined | PARTIAL | `OutlookComMailSenderGuardTests` (null-Application guard); `OutlookApplicationProviderTests` (set/clear/read); `remediation-final-architecture.md` (COM confinement); the live send path is in 3 `[ExcludeFromCodeCoverage]` members exercised only by the 2 gated integration tests | `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"` (on a live-Outlook host) | Non-COM surface and confinement verified; the live send/release path is covered-by-design only (gated-skip; no live Outlook). See `fail-before-exception.2026-06-16T07-44.md`. |
| AC-07 | Validation rules; 400 INVALID_REQUEST; COM failure → InternalError → 502 | PASS | MailBridge dispatch tests (no recipients / invalid contentType → InvalidRequest; empty subject accepted; sender throws → InternalError); HostAdapter endpoint tests (400 no-recipient/contentType; 502 runner failure) | `dotnet test ... --filter "TestCategory!=Integration"` | All validation and error-mapping branches unit-covered. |
| AC-08 | `saveToSentItems` defaults true; maps to `DeleteAfterSubmit = !saveToSentItems` | PASS | `Send_mail_save_to_sent_items_should_default_to_true_when_absent`; `MailContractsTests` default-true; `BuildSendMail` default true; `OutlookComMailSender` sets `DeleteAfterSubmit = !SaveToSentItems` | `dotnet test ... --filter "TestCategory!=Integration"` | Default and mapping unit-verified; the live write to Sent Items is part of the gated integration path (AC-06/AC-10). |
| AC-09 | Send-on-behalf deferred to PI-1; seam accepts future `fromEmailAddress`; documented | PASS | `SendMailComRequest.FromEmailAddress` optional param; `IHostAdapterClient.SendMailAsync` XML doc PI-1 note; README PI-1 note | diff inspection | Additive, non-breaking seam. |
| AC-10 | Test inventory (a) integration Sent Items (b) integration COM path (c) endpoint mocked-runner unit + Core/RPC/contract tests | PARTIAL | (c) and the Core/RPC/contract unit tests present and passing (`HostAdapterSendMailTests`, `HostAdapterHttpClientSendMailTests`, `MailBridgeRuntimeTests.SendMail`, `BridgeContractsCoverageTests`); (a),(b) exist as `[TestCategory("Integration")]` tests but gate-skip without live Outlook | `dotnet test ... --filter "TestCategory!=Integration"`; integration filter on a live host | The unit-test obligations of AC-10 are PASS; (a) and (b) are written but not executed (gated-skip). Covered-by-design. |
| AC-11 | Toolchain passes; line ≥ 85% / branch ≥ 75%; no regression; only documented `[ExcludeFromCodeCoverage]`; no file > 500 lines; additive | PASS | `remediation-final-{format,lint,typecheck,architecture,test-coverage}.md` (EXIT_CODE 0); 90.25% line / 79.36% branch; delta 0.00 pp; 3 `[ExcludeFromCodeCoverage]` only; all changed `.cs` ≤ 500 lines (max 465); additive contracts | `for f in $(git diff --name-only 0cb7de6..HEAD \| grep '\.cs$'); do wc -l "$f"; done \| sort -rn` | The file-size sub-criterion, previously FAIL, is now PASS after the R-1 split. The "covered by the integration test" qualifier on the `[ExcludeFromCodeCoverage]` members is satisfied by-design pending a live run (see AC-06). |

---

## Summary

**Overall Feature Readiness:** NEEDS REVISION (single non-blocking item: execute live-COM integration tests)

**Criteria summary:**
- **PASS:** 9 criteria (AC-01, AC-02, AC-03, AC-04, AC-05, AC-07, AC-08, AC-09, AC-11)
- **PARTIAL:** 2 criteria (AC-06, AC-10)
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Blocking-findings count: 0.** The prior cycle's sole Blocking finding (R-1, the 500-line file-size violation embedded in AC-11) is resolved. The two PARTIAL criteria are covered-by-design dispositions gated by the absence of a live Outlook host, not Blocking findings.

**Top gaps preventing PASS:**

1. AC-06 and AC-10(a,b): the live-COM send path (Sent Items entry creation and end-to-end COM send/release) is covered-by-design only; the two `[TestCategory("Integration")]` tests gate-skip without a live Outlook host. Documented in `evidence/regression-testing/fail-before-exception.2026-06-16T07-44.md`.

**Recommended follow-up verification steps:**

1. Run `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"` on a host with a running, logged-on Outlook session and confirm the two integration tests execute (not `Assert.Inconclusive`) with EXIT_CODE 0; capture the run to `evidence/regression-testing/`. This converts AC-06 and AC-10(a,b) to executed PASS.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- Criteria evaluated as **PASS** may be checked off in the authoritative source file(s) if they are represented as markdown checkboxes and are not already checked.
- Criteria evaluated as **PARTIAL**, **FAIL**, or **UNVERIFIED** must remain unchecked.
- If the source uses prose or numbered requirements instead of checkbox items, do not rewrite the source file; record status only in this audit.

The `spec.md` AC-01..AC-11 items are authored as bold prose (`- **AC-01:** ...`), not markdown checkboxes, so there is no checkbox to toggle in the source; status is recorded in this audit only and `spec.md` is left unmodified. `user-story.md` holds unfilled placeholder checkboxes with no authored criteria, so none are checked off there. No source-file checkbox change was made for either file, by design.

### AC Status Summary

- Source: `docs/features/active/hostadapter-sendmail-com-send-75/spec.md` (AC-01..AC-11, prose); `docs/features/active/hostadapter-sendmail-com-send-75/user-story.md` (placeholder-only)
- Total AC items: 11 (spec.md)
- Checked off (delivered): 9 PASS (recorded in this audit; spec is prose, no checkbox toggled)
- Remaining (unchecked): 2 PARTIAL (AC-06, AC-10)
- Items remaining: AC-06 (live-COM send path covered-by-design pending live run); AC-10 (integration sub-criteria (a),(b) gated-skip; unit-test obligations met)

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 11 | 9 | 2 | Prose-only (no checkboxes); status tracked in this audit, source unmodified |
| `user-story.md` | 0 evaluable | 0 | 0 | Placeholder template only; not an authoritative authored source for this feature |

No source-file checkbox change was made because `spec.md` uses prose AC items and `user-story.md` contains only unfilled placeholders.
