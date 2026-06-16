# Feature Audit: hostadapter-sendmail-com-send (#75)

**Audit Date:** 2026-06-16
**Feature Folder:** `docs/features/active/hostadapter-sendmail-com-send-75`
**Base Branch:** `main`
**Head Branch:** `feature/hostadapter-sendmail-com-send-75`
**Work Mode:** `full-feature`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (merge-base commit `0cb7de6ed0fefe9a1e35d8dc7dbdc9bd4e7aa1b1`)
- **Head branch/commit:** `feature/hostadapter-sendmail-com-send-75` (commit `269d3bba2045b8430e23226af93ba4a33d8d585b`)
- **Merge base:** `0cb7de6`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/**` (baseline, qa-gates, regression-testing, other)
  - Additional evidence: direct branch-diff source inspection (`git diff 0cb7de6..HEAD`)
- **Feature folder used:** `docs/features/active/hostadapter-sendmail-com-send-75`
- **Requirements source:** `spec.md` (AC-01..AC-11) and `user-story.md` (per full-feature work mode)
- **Work mode resolution note:** `issue.md` is absent from the feature folder, so the work-mode marker could not be read. Per the fail-closed rule in `feature-review-workflow`, work mode defaults to `full-feature` (sources: `spec.md` and `user-story.md`). The caller also confirmed `full-feature`.
- **Scope note:** Audit covers the full branch diff `0cb7de6..HEAD`. C# is the only language with changed files. The two `[TestCategory("Integration")]` real-COM tests were gated-skipped on the review host (no live Outlook).

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/hostadapter-sendmail-com-send-75/spec.md` — primary source (AC-01..AC-11, bold prose, not markdown checkboxes)
- `docs/features/active/hostadapter-sendmail-com-send-75/user-story.md` — secondary source (placeholder template checkboxes only)

### From spec.md

1. **AC-01:** `IHostAdapterClient` declares `SendMailAsync(SendMailRequest, string? requestId = null, CancellationToken = default)` returning `Task<ApiEnvelope<object?>>`; the `SendMail*` DTOs are Graph-aligned `sealed record`s in `OpenClaw.HostAdapter.Contracts`.
2. **AC-02:** The HostAdapter registers `POST /users/{assistantMailbox}/sendMail` in a new `MailRoutes.cs` via `app.MapMailRoutes()`; `Program.cs` remains under 500 lines; the `BearerTokenMiddleware` → `RequireReadyBridgeAsync` → validate → dispatch order is preserved.
3. **AC-03:** A successful send returns 202 Accepted with `ApiEnvelope<object?>` (`ok: true`, `data: null`) (D-A).
4. **AC-04:** `HostAdapterHttpClient.SendMailAsync` issues an HTTP POST to `users/{MailboxId}/sendMail` with the JSON body via a new `PostAsync` helper, obtaining the token through the existing `TokenReader` seam.
5. **AC-05:** `BridgeMethods.SendMail = "send_mail"` is added to `BridgeMethods.All`; the MailBridge client `Build` switch gains a `"send-mail"` arm; recipients are passed as JSON-serialized arrays per recipient type (D-C).
6. **AC-06:** COM send runs on the STA thread in `OutlookComMailSender : IOutlookMailSender`, obtaining the `Application` via `IOutlookApplicationProvider` set by `OutlookScanner` (D-E); it sets `Subject`, body (`HTMLBody` for HTML, else `Body`), To/CC/BCC recipients including BCC via `Recipients.Add(...).Type=olBCC` (D-I), `DeleteAfterSubmit = !saveToSentItems`, calls `Send()`, and releases all COM objects in `finally`. COM remains confined to `OpenClaw.MailBridge`.
7. **AC-07:** Validation: ≥1 recipient across To/CC/BCC combined (D-G); contentType must be Text or HTML case-insensitive; empty subject permitted (D-F); `{assistantMailbox}` not validated against local profile (D-D); validation failures return 400 INVALID_REQUEST; COM send failure maps to `BridgeErrorCodes.InternalError` → HTTP 502 (D-H).
8. **AC-08:** `saveToSentItems` defaults to true when absent; true saves to Sent Items (`DeleteAfterSubmit = false`), false does not.
9. **AC-09:** Send-on-behalf deferred to PI-1; the `IOutlookMailSender` seam accepts a future `fromEmailAddress` without breaking existing callers, and this deferral is documented.
10. **AC-10:** Tests cover: (a) integration test [real COM] producing a Sent Items entry; (b) integration test validating the COM send path; (c) endpoint unit test with a mocked runner; plus Core client unit tests, MailBridge RPC-dispatch unit tests with a `FakeOutlookMailSender`, and a contract-coverage test asserting `BridgeMethods.All` contains `send_mail`.
11. **AC-11:** The full seven-stage toolchain passes; line coverage ≥ 85% and branch coverage ≥ 75%; no regression on changed lines; no new analyzer/nullable suppressions except documented `[ExcludeFromCodeCoverage]` on live-COM-only members (each covered by the integration test); no file exceeds 500 lines; all contract changes additive.

### From user-story.md

The `## Acceptance Criteria` section of `user-story.md` contains only unfilled template placeholders (`- [ ] Criterion 1`, `- [ ] Criterion 2`, `- [ ] Criterion 3`). These carry no substantive requirement and are not evaluable. They are recorded here for completeness; no check-off is applicable.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-01 | `SendMailAsync` on `IHostAdapterClient`; `SendMail*` sealed records | PASS | `IHostAdapterClient.cs` diff adds the method with exact signature; `MailContracts.cs` defines 5 `public sealed record` DTOs | `git diff 0cb7de6..HEAD -- src/OpenClaw.HostAdapter.Contracts/*.cs` | Additive interface change |
| AC-02 | New `MailRoutes.cs` + `app.MapMailRoutes()`; Program.cs < 500; order preserved | PASS | `MailRoutes.cs` (149 lines) maps the route; HostAdapter `Program.cs` = 436 lines; handler runs ready→validate→dispatch | `wc -l src/OpenClaw.HostAdapter/Program.cs` → 436 | `HostAdapterSendMailTests` assert ordering |
| AC-03 | 202 + `{ ok:true, data:null }` | PASS | `MailRoutes.HandleSendMailAsync` returns `HostAdapterResponses.AcceptedNoContent(...)`; Core test maps 202→ok:true/data:null | diff inspection; `evidence/qa-gates/final-test-coverage.md` | |
| AC-04 | `SendMailAsync` POST via `PostAsync`, token via `TokenReader` | PASS | `HostAdapterHttpClient` diff: `PostAsync<SendMailRequest,object?>("users/{id}/sendMail", ...)`, `TokenReader(options.HostAdapter.TokenFile, ...)` | `git diff 0cb7de6..HEAD -- src/OpenClaw.Core/HostAdapterHttpClient.cs` | |
| AC-05 | `BridgeMethods.SendMail` in `All`; client `send-mail` arm; recipients as JSON arrays | PASS | `BridgeContracts.cs` diff adds `SendMail` const + `All` entry; `BridgeContractsCoverageTests` asserts containment; `BuildSendMail` passes JSON recipient arrays | `git diff 0cb7de6..HEAD -- src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | |
| AC-06 | COM send on STA in `OutlookComMailSender`; app via provider; To/CC/BCC incl olBCC; release in finally; COM confined | PARTIAL | Source confirms STA dispatch, provider source, `OlBcc=3` recipient typing, `DeleteAfterSubmit = !SaveToSentItems`, release in `finally`, and COM confinement (architecture tests pass). The live-COM send members carry `[ExcludeFromCodeCoverage]` and were exercised only by gated integration tests that were skipped on the review host. | `git diff` of `OutlookComMailSender.cs`; `evidence/qa-gates/final-architecture.md`; `evidence/regression-testing/integration-com-send.md` | Implementation verified by inspection; live behavior not verified on this host |
| AC-07 | Validation rules; 400 INVALID_REQUEST; COM failure → 502 (D-H) | PASS | `MailRoutes.ValidateRequest` + `SendMailRpcHandler.Parse` enforce ≥1 recipient and contentType; empty subject allowed; `{id}` unvalidated; `PipeRpcWorker` maps `SendMailValidationException`→InvalidRequest and other exceptions→InternalError | diff inspection; `HostAdapterSendMailTests` (400/502), `MailBridgeRuntimeTests.SendMail` | |
| AC-08 | `saveToSentItems` default true; maps to `DeleteAfterSubmit` | PASS | `SendMailRpcHandler.ParseSaveToSentItems` returns true when absent; `OutlookComMailSender` sets `DeleteAfterSubmit = !SaveToSentItems`; `SendMailRequest` default `= true` | diff inspection; `Send_mail_save_to_sent_items_should_default_to_true_when_absent` | |
| AC-09 | Send-on-behalf deferred to PI-1; seam accepts future `fromEmailAddress`; documented | PASS | `IHostAdapterClient.SendMailAsync` XML doc documents PI-1 deferral; `SendMailComRequest` seam designed for future param; README modified | `git diff 0cb7de6..HEAD -- README.md src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` | |
| AC-10 | Test coverage (integration + endpoint + client + RPC + contract) | PARTIAL | (c) endpoint, Core client, RPC-dispatch, and contract-coverage tests present and passing. (a)(b) the two real-COM integration tests exist but were `Assert.Inconclusive`-skipped on the review host (no live Outlook). | `evidence/qa-gates/final-test-coverage.md`; `evidence/regression-testing/integration-com-send.md` | Unit/contract sub-criteria PASS; live-COM sub-criteria not verified here |
| AC-11 | Toolchain passes; coverage thresholds; no regression; only documented `[ExcludeFromCodeCoverage]`; no file > 500 lines; additive contracts | FAIL | Toolchain gates pass; coverage 90.25% line / 79.35% branch with no regression; exactly 3 documented `[ExcludeFromCodeCoverage]` on live-COM-only members; contracts additive. However a modified test file, `MailBridgeProgramTests.cs`, is 573 lines, violating the "no file exceeds 500 lines" requirement. | `for f in $(git diff --name-only 0cb7de6..HEAD \| grep '\.cs$'); do wc -l "$f"; done` → 573 | The 500-line breach fails this criterion as written |

---

## Summary

**Overall Feature Readiness:** NEEDS REVISION

**Criteria summary:**
- **PASS:** 8 criteria (AC-01, AC-02, AC-03, AC-04, AC-05, AC-07, AC-08, AC-09)
- **PARTIAL:** 2 criteria (AC-06, AC-10)
- **UNVERIFIED:** 0 criteria
- **FAIL:** 1 criterion (AC-11)

**Top gaps preventing PASS:**

1. AC-11 fails: `tests/OpenClaw.MailBridge.Tests/MailBridgeProgramTests.cs` is 573 lines, exceeding the 500-line limit (baseline 518, +55 this branch). The AC map's "no file exceeds 500 lines" claim is inaccurate.
2. AC-06 and AC-10 are PARTIAL: the live-COM Sent Items entry and COM send path were gated-skipped on the review host; covered-by-design pending a live-Outlook integration run.

**Recommended follow-up verification steps:**

1. Split `MailBridgeProgramTests.cs` into multiple files each under 500 lines, then rerun the C# toolchain; correct the file-size statement in `evidence/other/acceptance-criteria-map.md`.
2. Run `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"` on a live-Outlook host to convert AC-06/AC-10(a,b) from covered-by-design to verified.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- `spec.md` states AC-01..AC-11 as bold prose, not markdown checkboxes; no checkbox check-off is possible in the source. Status is recorded in this audit only; the spec is not rewritten.
- `user-story.md` contains only unfilled template placeholders; no substantive criteria to check off. Leaving them unchecked.
- No source-file checkbox was modified because neither authoritative file expresses the substantive criteria as checkable, unchecked, evaluable items.

### AC Status Summary

- Source: `docs/features/active/hostadapter-sendmail-com-send-75/spec.md` (prose AC-01..AC-11), `docs/features/active/hostadapter-sendmail-com-send-75/user-story.md` (placeholder only)
- Total AC items: 11 (substantive, from spec.md)
- Checked off (delivered): 0 (prose source; not checkbox-backed)
- Remaining (unchecked): 11 (3 not fully PASS: AC-06, AC-10 PARTIAL; AC-11 FAIL)
- Items remaining: AC-06 (PARTIAL — live-COM unverified), AC-10 (PARTIAL — integration unverified), AC-11 (FAIL — file > 500 lines). The 8 PASS criteria are delivered but cannot be checkbox-marked because the source uses prose.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 11 | 0 | 11 | Prose-only; not checkbox-backed (8 evaluate PASS, 2 PARTIAL, 1 FAIL) |
| `user-story.md` | 3 | 0 | 3 | Placeholder template criteria; not authoritative content |
