# Feature Audit: mailbridge-messagedto-resolved-fields (#73) — Remediation Cycle 1 Re-Audit

**Audit Date:** 2026-06-15
**Feature Folder:** `docs/features/active/mailbridge-messagedto-resolved-fields-73`
**Base Branch:** `main`
**Head Branch:** `feature/mailbridge-messagedto-resolved-fields-73` (`07c4e202ffab4128cb1f077dcc564645ca0366ba`)
**Work Mode:** `full-feature` (fail-closed default; `issue.md` absent)
**Audit Type:** Post-remediation acceptance verification (Remediation Cycle 1)

---

## Scope and Baseline

- **Base branch:** `main` (commit `be2ddbf6559febc4ddfcf14a098025d96647f772`)
- **Head branch/commit:** `feature/mailbridge-messagedto-resolved-fields-73` (`07c4e202ffab4128cb1f077dcc564645ca0366ba`)
- **Merge base:** `be2ddbf6559febc4ddfcf14a098025d96647f772`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt` (refreshed 2026-06-15)
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/**`
  - Additional evidence: source file inspection, wc -l, git diff grep
- **Feature folder used:** `docs/features/active/mailbridge-messagedto-resolved-fields-73`
- **Requirements source:** `spec.md` (AC-01..AC-11, prose-formatted) and `user-story.md` (placeholder template only)
- **Work mode resolution note:** `issue.md` does not exist in the feature folder, so the persisted work-mode marker is absent. Per the fail-closed rule, work mode defaults to `full-feature`, making both `spec.md` and `user-story.md` authoritative AC sources.
- **Scope note:** The `pr_context.summary.txt` "Changed files overview" reports "Core logic changes: 0 files" and lists only docs/tooling, inconsistent with the actual diff (21 changed C# files). The authoritative scope is the full `be2ddbf..07c4e20` git diff per the scope invariant. Both feature commits (`9658ee7` and `07c4e20`) are in scope.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/mailbridge-messagedto-resolved-fields-73/spec.md` — AC-01..AC-11 (prose-formatted `- **AC-NN:**`)
- `docs/features/active/mailbridge-messagedto-resolved-fields-73/user-story.md` — placeholder template criteria

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
| AC-01 | MessageDto trailing optional fields; ToJson/CcJson positions retained | PASS | `BridgeContracts.cs` lines 92–95: four nullable trailing optionals with `= null` defaults; ToJson/CcJson at lines 87–88. Build clean. | `git diff be2ddbf HEAD -- BridgeContracts.cs` | Unchanged from initial audit. |
| AC-02 | SenderEmailResolved fail-soft SMTP resolution | PASS | `ComMessageSource.ResolveSenderSmtp`: PropertyAccessor → ExchangeUser → Address → raw fallback chain. Tests: `ExchangeDnSender_should_resolve_to_true_smtp_not_the_dn`, `OrdinaryMail_should_populate_resolved_fields`, `ResolveViaPropertyAccessor_should_return_null_when_get_property_throws`, `ResolveViaExchangeUser_should_return_null_when_get_exchange_user_throws`. | `dotnet test ...` (558 pass) | RF-1 remediation added direct catch-path coverage. |
| AC-03 | FromEmailAddress on-behalf-of, else resolved sender | PASS | `ComMessageSource.ResolveFromSmtp`/`ResolveOnBehalfOfSmtp`; tests: `DelegateSentMeeting_should_reflect_on_behalf_of_in_from_address`, `OrdinaryMail_should_populate_resolved_fields`. | `dotnet test ...` | Unchanged from initial audit. |
| AC-04 | ToJson/CcJson non-null JSON arrays; To=1, Cc=2 | PASS | `OutlookScanner.ReadMessageRecipients` + `SerializeAttendees`; tests: `OrdinaryMail_should_populate_resolved_fields_and_recipient_json`, `OrdinaryMail_with_no_recipients_should_yield_empty_json_arrays`. | `dotnet test ...` | Same serializer/options as #71. |
| AC-05 | ConversationId non-empty from source | PASS | `ComMessageSource.ConversationId` pass-through; test `Adapter_should_pass_conversation_id_through_unmodified`. | `dotnet test ...` | Unchanged from initial audit. |
| AC-06 | MeetingMessageType raw OlMeetingType for meeting, null for mail | PASS | `ComMessageSource.MeetingMessageType`; `ComMessageSourceTests` DataRows 0–4 and ordinary-mail null. | `dotnet test ...` | Unchanged from initial audit. |
| AC-07 | Combined acceptance signal (SMTP sender + non-null ToJson + non-empty ConversationId) | PASS | Test `MeetingRequest_should_satisfy_combined_acceptance_signal`. | `dotnet test ...` | Unchanged from initial audit. |
| AC-08 | Unit tests cover meeting + ordinary-mail paths | PASS | `OutlookScannerMessageFieldsTests`, `ComMessageSourceTests`, `ComMessageSourceResolutionTests` all pass. 558 total pass. | `dotnet test ...` | RF-1 remediation added 28 new test methods. |
| AC-09 | IMessageSource + COM adapter inside MailBridge; downstream depends on abstraction; COM confined | PASS | `IMessageSource.cs`/`ComMessageSource.cs` internal to MailBridge; `final-architecture.md` 0 boundary violations; no new ProjectReference edges. | `evidence/qa-gates/final-architecture.md` | Unchanged from initial audit. |
| AC-10 | Both caches persist/read all fields via idempotent migrations; mapper maps all four | PASS | `CacheRepository.Schema.cs`, `CoreCacheRepository.Schema.cs`: PRAGMA-guarded `MigrateMessagesSchemaAsync`. `CacheRepository.cs`, `CoreCacheRepository.Messages.cs`: parameter binding. `SchedulingDtoMapper.MapMessage`: sender/from/conversation/meetingType wired (no hardcoded values). Round-trip tests pass. Core.Tests 206/206 confirms read-back fidelity after RF-2 extraction. | `dotnet test ...` | RF-2 extraction preserved all SQL/binding/reader logic. |
| AC-11 | Seven-stage toolchain passes; coverage thresholds; no regression; no new suppressions; no file > 500 lines | PASS | Format: `final-format.md` exit 0. Lint: `final-build-lint.md` 0/0. Nullable: `final-nullable.md` 0/0. Architecture: 0 violations. Tests: 558 pass. Coverage: MailBridge 93.9%/87.0%, Core 89.6%/78.4%, ComMessageSource.cs 94.7%/93.5% — all above thresholds. `coverage-delta.md` PASS. Suppressions: none added. File size: all files <= 500 lines (`wc -l` verified). | Multiple commands — see `evidence/qa-gates/` | RF-1 + RF-2 together close all three sub-criteria that failed in the initial audit. |
| US-1 | user-story.md Criterion 1 (placeholder) | UNVERIFIED | Template placeholder text; no substantive criterion authored. | n/a | Not a real criterion. |
| US-2 | user-story.md Criterion 2 (placeholder) | UNVERIFIED | Template placeholder text. | n/a | Not a real criterion. |
| US-3 | user-story.md Criterion 3 (placeholder) | UNVERIFIED | Template placeholder text. | n/a | Not a real criterion. |

---

## Summary

**Overall Feature Readiness: PASS**

**Criteria summary:**
- **PASS:** 11 criteria (AC-01..AC-11)
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 3 criteria (user-story.md placeholder US-1..US-3)
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

None. All eleven spec acceptance criteria evaluate PASS. The three UNVERIFIED items are unfilled template placeholders with no substantive requirement text; they do not represent feature gaps.

**Recommended follow-up verification steps:**

1. Author real user-story criteria in `user-story.md` (optional authoring cleanup; not blocking).
2. Address `OpenClaw.HostAdapter.Tests` branch coverage (66.0%) in a separate work item (pre-existing, out of scope for this feature).

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- All 11 `spec.md` acceptance criteria evaluate **PASS**.
- The `spec.md` AC items are authored as bold prose (`- **AC-NN:** ...`), not markdown checkboxes. No `[ ]`/`[x]` toggle is possible without reformatting the source. Per the preserve-text rule, the source file is not modified; PASS status is recorded in this audit artifact only.
- `user-story.md` placeholder criteria US-1..US-3 are UNVERIFIED and remain unchecked.

### AC Status Summary

- Source: `spec.md` (AC-01..AC-11), `user-story.md` (placeholders US-1..US-3)
- Total AC items: 14 (11 spec + 3 user-story placeholders)
- Checked off (delivered): 11 (AC-01..AC-11 all PASS, though no checkbox exists in the source)
- Remaining (unchecked): 3 (US-1, US-2, US-3 — unfilled template placeholders)
- Items remaining: US-1, US-2, US-3 (not substantive requirements)

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 11 | 0 checkboxes (11 PASS) | 0 | Prose-only AC (`- **AC-NN:**`); no checkbox to toggle; all evaluated PASS in this audit. |
| `user-story.md` | 3 | 0 | 3 | Unfilled template placeholders; not substantive criteria. |

No source-file checkbox change was made. The `spec.md` ACs are prose-formatted and the preserve-text rule prohibits reformatting. AC pass/fail status is recorded in this audit only.
