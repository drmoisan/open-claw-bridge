# Feature Audit: mailbridge-messagedto-resolved-fields (#73)

**Audit Date:** 2026-06-13
**Feature Folder:** `docs/features/active/mailbridge-messagedto-resolved-fields-73`
**Base Branch:** `main`
**Head Branch:** `feature/mailbridge-messagedto-resolved-fields-73`
**Work Mode:** `full-feature` (fail-closed default; `issue.md` is absent so no work-mode marker exists)
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `be2ddbf6559febc4ddfcf14a098025d96647f772`)
- **Head branch/commit:** `feature/mailbridge-messagedto-resolved-fields-73` (commit `9658ee76b2a997287aa36a6d22f4a353f8bb9890`)
- **Merge base:** `be2ddbf6559febc4ddfcf14a098025d96647f772`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/**`
  - Additional evidence: independent `dotnet build` / `dotnet test --collect:"XPlat Code Coverage"` run and `git diff be2ddbf..9658ee7`
- **Feature folder used:** `docs/features/active/mailbridge-messagedto-resolved-fields-73`
- **Requirements source:** `spec.md` (`## Acceptance Criteria`, AC-01..AC-11) and `user-story.md` (placeholder criteria)
- **Work mode resolution note:** `issue.md` does not exist in the feature folder, so the persisted work-mode marker is missing. Per the fail-closed rule, work mode defaults to `full-feature`, making both `spec.md` and `user-story.md` authoritative AC sources. The caller also specified `full-feature` with `spec.md` as the AC source.
- **Scope note:** The audit was conducted against the full `main..HEAD` git diff. The `pr_context.summary.txt` "Changed files overview" reported only docs/tooling and `Core logic changes: 0 files`, which is inconsistent with the actual diff (18 changed `.cs` files, 3 new source files); the authoritative scope is the resolved base branch and the actual git diff, per the scope invariant.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/mailbridge-messagedto-resolved-fields-73/spec.md` — primary source (AC-01..AC-11)
- `docs/features/active/mailbridge-messagedto-resolved-fields-73/user-story.md` — secondary source (template placeholders only)

### From spec.md

1. **AC-01:** `MessageDto` declares `SenderEmailResolved` (`string?`), `FromEmailAddress` (`string?`), `ConversationId` (`string?`), `MeetingMessageType` (`int?`) as trailing optional parameters with defaults; `ToJson`/`CcJson` retain their positions.
2. **AC-02:** Normalization resolves `SenderEmailResolved` to a valid SMTP address for an Exchange internal sender (fail-soft per D-C), falling back gracefully when SMTP is unavailable.
3. **AC-03:** `FromEmailAddress` reflects the on-behalf-of identity when present, else the resolved sender (D-A).
4. **AC-04:** `ToJson` and `CcJson` are non-null JSON arrays of `{"name","email"}` for a message with recipients, using the same shape/serializer as the #71 attendee JSON; To=type 1, Cc=type 2.
5. **AC-05:** `ConversationId` is populated (non-empty) from the source `ConversationID`.
6. **AC-06:** `MeetingMessageType` carries the raw `OlMeetingType` int for a meeting-request item and is null for ordinary mail (D-B).
7. **AC-07:** A meeting-request `MessageDto` simultaneously has `SenderEmailResolved` as valid SMTP, `ToJson` as a non-null JSON array, and `ConversationId` non-empty (issue acceptance signal).
8. **AC-08:** Unit tests cover both the meeting-message path and the ordinary-mail path.
9. **AC-09:** A unifying message-source interface and a COM data-type adapter exist within `OpenClaw.MailBridge`; core normalization, `SchedulingDtoMapper`, and both cache repositories depend on the abstraction, not on concrete COM types (D-D). COM remains confined to `OpenClaw.MailBridge`.
10. **AC-10:** Both SQLite caches persist and read back all six fields via idempotent schema migrations; `SchedulingDtoMapper` maps all four scheduling-relevant fields (no hardcoded ConversationId or meeting type).
11. **AC-11:** Full seven-stage toolchain passes; line coverage >= 85%, branch coverage >= 75%; no regression on changed lines; no new analyzer/nullable suppressions; no file exceeds 500 lines.

### From user-story.md

- Criterion 1 (unfilled template placeholder)
- Criterion 2 (unfilled template placeholder)
- Criterion 3 (unfilled template placeholder)

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-01 | MessageDto trailing optional fields; ToJson/CcJson positions retained | PASS | `BridgeContracts.cs` lines 88-95 add four nullable trailing optionals with `= null` defaults; ToJson/CcJson keep prior positions. | `git diff ... BridgeContracts.cs` | Build clean confirms compilation of positional + named callers. |
| AC-02 | SenderEmailResolved fail-soft SMTP resolution | PASS | `ComMessageSource.ResolveSenderSmtp` + chain; test `ExchangeDnSender_should_resolve_to_true_smtp_not_the_dn` and `OrdinaryMail_should_populate_resolved_fields`. | `dotnet test ...` (530 pass) | Live-COM PropertyAccessor/GetExchangeUser branches untested (see AC-11 coverage). |
| AC-03 | FromEmailAddress on-behalf-of, else resolved sender | PASS | `ComMessageSource.ResolveFromSmtp`/`ResolveOnBehalfOfSmtp`; test `DelegateSentMeeting_should_reflect_on_behalf_of_in_from_address`, fallback via `OrdinaryMail_should_populate_resolved_fields`. | `dotnet test ...` | |
| AC-04 | ToJson/CcJson non-null `{"name","email"}` arrays; To=1, Cc=2 | PASS | `OutlookScanner.ReadMessageRecipients` (type 1→To, 2→Cc, 3 ignored) + reused `SerializeAttendees`; tests `OrdinaryMail_should_populate_resolved_fields_and_recipient_json`, `..._no_recipients_should_yield_empty_json_arrays` ("[]"). | `dotnet test ...` | Same serializer/options as #71. |
| AC-05 | ConversationId non-empty from source | PASS | `ComMessageSource.ConversationId` pass-through; test `Adapter_should_pass_conversation_id_through_unmodified` + scanner field tests. | `dotnet test ...` | |
| AC-06 | MeetingMessageType raw OlMeetingType for meeting, null for mail | PASS | `ComMessageSource.MeetingMessageType` (`_isMeeting ? MeetingType : null`); `ComMessageSourceTests` DataRows 0-4 and ordinary-mail null. | `dotnet test ...` | |
| AC-07 | Combined acceptance signal (SMTP sender + non-null ToJson + non-empty ConversationId) | PASS | Test `MeetingRequest_should_satisfy_combined_acceptance_signal`. | `dotnet test ...` | |
| AC-08 | Unit tests cover meeting + ordinary-mail paths | PASS | `OutlookScannerMessageFieldsTests` covers both paths; corroborated by 530-pass re-run. | `dotnet test ...` | |
| AC-09 | IMessageSource + COM adapter inside MailBridge; downstream depends on abstraction; COM confined | PASS | `IMessageSource.cs`/`ComMessageSource.cs` are internal to OpenClaw.MailBridge; `final-architecture.md` confirms no COM types outside MailBridge and no csproj changes. | `git diff`; `evidence/qa-gates/final-architecture.md` | |
| AC-10 | Both caches persist/read all fields via idempotent migrations; mapper maps all four | PASS | `CacheRepository*.cs` and `CoreCacheRepository*.cs` add columns, PRAGMA-guarded `MigrateMessagesSchemaAsync`, binding, readers; `SchedulingDtoMapper.MapMessage` wires sender/from/conversation/meeting type (no hardcoded values). Round-trip tests in both cache test files. | `dotnet test ...` | |
| AC-11 | Toolchain green; line >= 85% / branch >= 75%; no changed-line regression; no new suppressions; no file > 500 lines | FAIL | Toolchain: build 0/0 and 530 tests pass (re-run); no new suppressions (`final-lint.md`). BUT: new file `ComMessageSource.cs` 80.1% line / 60.9% branch (below uniform thresholds), and `CoreCacheRepository.cs` is 699 lines (> 500). | `dotnet build ...`; `dotnet test ... --collect:"XPlat Code Coverage"`; `evidence/other/file-size-check.md` | Per-project coverage passes (90.90%/80.39%, 98.60%/91.68%); the per-new-file coverage and file-size sub-criteria fail. |
| US-1 | user-story.md Criterion 1 (placeholder) | UNVERIFIED | Template placeholder text; no substantive criterion authored. | n/a | Not a real criterion; documented gap in user-story.md authoring. |
| US-2 | user-story.md Criterion 2 (placeholder) | UNVERIFIED | Template placeholder text. | n/a | As above. |
| US-3 | user-story.md Criterion 3 (placeholder) | UNVERIFIED | Template placeholder text. | n/a | As above. |

---

## Summary

**Overall Feature Readiness:** NEEDS REVISION

**Criteria summary:**
- **PASS:** 10 criteria (AC-01..AC-10)
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 3 criteria (user-story.md placeholders US-1..US-3)
- **FAIL:** 1 criterion (AC-11)

**Top gaps preventing PASS:**

1. AC-11: New file `ComMessageSource.cs` is below the uniform new-code coverage thresholds (80.1% line / 60.9% branch vs 85% / 75%).
2. AC-11: `CoreCacheRepository.cs` exceeds the 500-line cap (699 lines; pre-existing, grown +12 by this feature).
3. user-story.md contains only unfilled template placeholder criteria; the secondary AC source was not authored.

**Recommended follow-up verification steps:**

1. Add tests exercising the reachable SMTP fallback/catch branches in `ComMessageSource.cs` via the reflection doubles, then re-measure per-file coverage; or record an operator-approved coverage-exclusion disposition for the genuinely live-COM-only paths.
2. Resolve the `CoreCacheRepository.cs` file-size finding (extract a partial) or obtain a formal accepted-exception, then re-run `evidence/other/file-size-check.md`.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- AC-01..AC-10 are evaluated PASS. The `spec.md` `## Acceptance Criteria` items are authored as bold prose (`- **AC-01:** ...`), not markdown checkboxes, so there is no `[ ]`/`[x]` box to toggle. Status is recorded here only; the source `spec.md` is not rewritten (no-phantom-criteria / preserve-text rules).
- AC-11 is FAIL and remains unchecked/unmodified.
- user-story.md placeholders US-1..US-3 are UNVERIFIED and remain unchecked.

The `spec.md` `## Definition of Done` checklist (a separate checkbox section, not the AC source) was inspected but not modified: the executor had already marked the toolchain/tests/edge-case items `[x]` and left docs/telemetry `[ ]`. Per acceptance-criteria-tracking, reviewers check off AC source items only; Definition-of-Done items are out of scope for reviewer check-off and were left as authored.

### AC Status Summary

- Source: `spec.md` (AC-01..AC-11), `user-story.md` (placeholders)
- Total AC items: 14 (11 spec + 3 user-story placeholders)
- Checked off (delivered): 0 (spec ACs are prose, not checkboxes; no box to toggle)
- Remaining (unchecked): 14
- Items remaining: AC-11 (FAIL); US-1, US-2, US-3 (UNVERIFIED placeholders); AC-01..AC-10 are PASS but have no checkbox to toggle in the source.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 11 | 0 | 11 | Prose-only AC (`- **AC-NN:**`), not checkbox-backed; 10 PASS / 1 FAIL recorded in this audit only. |
| `user-story.md` | 3 | 0 | 3 | Unfilled template placeholders; not substantive criteria. |

No source-file checkbox change was made: the `spec.md` ACs are prose-formatted (not markdown checkboxes), so there is no checkbox to toggle, and the preserve-text rule prohibits reformatting them. AC pass/fail status is recorded in this audit.
