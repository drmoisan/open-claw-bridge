# Feature Audit: sensitivity-redaction (#18, co-delivers #20)

**Audit Date:** 2026-07-02
**Auditor:** feature-review agent

## Scope and Baseline

- **Feature branch:** `feature/sensitivity-redaction-18` @ head `d267c663b0ea966609a97dc9e98e9e5ccbdc8cff`
- **Resolved base branch:** `main` (`origin/main`) @ merge-base `8c969f1a6e96120dd95f835a289c8b185abee202`
- **Diff range:** `8c969f1a6e96120dd95f835a289c8b185abee202..d267c663b0ea966609a97dc9e98e9e5ccbdc8cff` (40 files, +2600/-59)
- **Work mode:** `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` **and** `user-story.md` per `acceptance-criteria-tracking`. `issue.md` mirrors the spec AC verbatim.
- **Primary evidence:** `artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt` (refreshed for this head), executor evidence under `docs/features/active/sensitivity-redaction-18/evidence/**`, and the reviewer's independent toolchain/coverage re-run (`evidence/qa-gates/coverage-review.2026-07-02T09-45.md`).
- **Verification model:** every criterion below is mapped to named tests executed in the reviewer's own run (647 passed / 0 failed / 5 env-gated skips at head `d267c66`) and, where applicable, to fail-before regression evidence and reviewer-parsed coverage data.

## Acceptance Criteria Inventory

Source 1 — `spec.md` `## Acceptance Criteria` (19 checkbox items, all currently `[x]`, checked off by the executor):

- Group A — Normalization-time sensitivity redaction (#18): A1-A7
- Group B — Safe-mode shaping suppression (#20): B1-B6
- Group C — Composition invariants (#18 x #20): C1-C5
- Toolchain and coverage: T1 (single item)

Source 2 — `user-story.md` `## Acceptance Criteria` (6 checkbox items, all currently `[x]`, checked off by the executor): US1-US6.

## Acceptance Criteria Evaluation

### Spec Group A — Normalization-time sensitivity redaction (#18)

| # | Criterion (abbreviated) | Verdict | Evidence |
|---|---|---|---|
| A1 | `NormalizeMessage` with Sensitivity 2/3: subject "Private message"; sender/recipient/preview fields null; `IsRedacted = true`; `ProtectedFieldsAvailable = false` | PASS | `Sensitive_message_should_be_fully_redacted` (DataRows 2, 3) asserts all 10 field dispositions through the real scan path; fail-before EXIT 1 (`redaction-normalization-fail-before.2026-07-02T09-08.md`) |
| A2 | `NormalizeMessage` retains all 11 mechanical fields unchanged on redacted messages | PASS (with noted gap) | `Sensitive_message_should_retain_mechanical_fields` asserts 10 of 11 through the scanner (non-meeting item: `MeetingMessageType` is null by construction); `RedactMessage_should_retain_every_mechanical_field_unchanged` asserts all 11 including `MeetingMessageType` at the pure-transform level. Gap: no scanner-level test of a redacted **meeting** message (`ItemKind = "meeting"`, non-null `MeetingMessageType`) — source of the Blocking coverage finding; retention itself is proven at transform level, so the criterion is met as written. |
| A3 | `NormalizeEvent` with Sensitivity 2/3: subject "Private appointment"; location/organizer/attendee/body fields null; `Categories` empty; flags set | PASS | `Sensitive_event_should_be_fully_redacted` (DataRows 2, 3) asserts all 12 dispositions through the real calendar scan path |
| A4 | `NormalizeEvent` retains all 16 mechanical fields unchanged on redacted events | PASS | `Sensitive_event_should_retain_mechanical_fields` (DataRows 2/"private", 3/"confidential") asserts all 16 through the scanner, including the `SensitivityLabel` mapping; `RedactEvent_should_retain_every_mechanical_field_unchanged` at transform level |
| A5 | Sensitive normalization never invokes `ShapePreview` or reads/resolves protected COM content | PASS | `Sensitive_message_normalization_should_never_access_protected_members` and `Sensitive_event_normalization_should_never_access_protected_members` (access-recording doubles record zero protected accesses; failure message enumerates offenders). Code-level: `Sensitivity` hoisted to first read in both `NormalizeMessage` (OutlookScanner.cs:363) and `BuildEventDto` (GraphFields.cs:42); sensitive builders contain no `ShapePreview`, body, recipient, or sender-resolution calls (reviewer code inspection). |
| A6 | Redaction applied at cache-write time; upsert/read-back round-trip returns redacted values without `ResponseShaper` | PASS | `Scanned_sensitivity2_message_should_round_trip_redacted_through_cache` and `Scanned_sensitivity3_event_should_round_trip_redacted_through_cache` (real `CacheRepository`, in-memory SQLite, `UpsertMessageAsync`/`GetMessageAsync` and `UpsertEventAsync`/`GetEventAsync`; no shaper in act/assert path) |
| A7 | Each redaction logged with bridge id only; no protected content in logs | PASS | `Message_redaction_should_log_bridge_id_only_at_information_level` and `Event_redaction_should_log_bridge_id_only_at_information_level` (capturing logger; exactly one Information-level redaction line containing the bridge id; every captured line scanned for all protected values) |

### Spec Group B — Safe-mode shaping suppression (#20)

| # | Criterion (abbreviated) | Verdict | Evidence |
|---|---|---|---|
| B1 | `ShapeMessage` safe mode nulls `ToJson`/`CcJson`/`SenderEmailResolved`/`FromEmailAddress`, sets `ProtectedFieldsAvailable = false`, no regression on existing suppression | PASS | `ShapeMessage_safe_mode_should_suppress_full_protected_field_set` (all 7 nulls + flag); regression guard: modified `ShapeMessage_in_safe_mode_should_suppress_protected_fields_without_setting_is_redacted`; fail-before EXIT 1 (`shaper-suppression-fail-before.2026-07-02T09-17.md`) |
| B2 | `ShapeMessage` safe mode retains all 12 other message fields | PASS | `ShapeMessage_safe_mode_should_retain_all_mechanical_fields` (all 12 asserted) |
| B3 | `ShapeEvent` safe mode nulls `Organizer`, sets flag false, empties `Categories`, no regression on existing 5-field suppression | PASS | `ShapeEvent_safe_mode_should_suppress_organizer_categories_and_set_flag` (all 6 nulls + empty array + flag) |
| B4 | `ShapeEvent` safe mode retains `Location` and all 17 mechanical fields | PASS | `ShapeEvent_safe_mode_should_retain_location_and_all_mechanical_fields` (Location "Conference Room A" retained + 17 fields) |
| B5 | Enhanced mode nulls nothing, does not force flag; preview sanitized/truncated; `BodyFull` verbatim | PASS | `Enhanced_mode_should_pass_through_all_fields_without_forcing_flag` (pass-through of all protected fields, `ProtectedFieldsAvailable` stays true, preview truncated to "Preview" at max 7 chars, `BodyFull` verbatim) |
| B6 | Already-null protected fields shape without error in both modes | PASS | `Already_null_protected_fields_should_shape_without_error_in_both_modes` (4 `NotThrow` assertions across both DTOs x both modes) |

### Spec Group C — Composition invariants (#18 x #20)

| # | Criterion (abbreviated) | Verdict | Evidence |
|---|---|---|---|
| C1 | Sensitivity 2/3 item served in enhanced mode stays redacted; `IsRedacted` remains true | PASS | `Redacted_message_should_survive_enhanced_mode_shaping`, `Redacted_event_should_survive_enhanced_mode_shaping` (inputs built with the production `RedactMessage`/`RedactEvent` transforms); fail-before EXIT 1 (`composition-invariants-fail-before.2026-07-02T09-20.md`) proves the pre-change enhanced branch falsified the flag |
| C2 | Redacted DTO through safe-mode shaping keeps `IsRedacted = true` | PASS | `Redacted_dtos_should_keep_is_redacted_through_safe_mode_without_error` (no-throw + flag true, both kinds) |
| C3 | Neither shaper mutates `IsRedacted` in either mode | PASS | `Shapers_should_never_mutate_is_redacted_in_either_mode` (4 assertions: safe/false-stays-false and enhanced/true-stays-true, both kinds); code inspection confirms neither `ShapeMessage` nor `ShapeEvent` assigns `IsRedacted` |
| C4 | `ProtectedFieldsAvailable = false` holds on both paths | PASS | `Protected_fields_available_false_should_hold_on_both_paths` (redaction-written false survives enhanced; safe mode forces false on unredacted) |
| C5 | Boundary values 0, 1, null, -1, 4, 99 untouched by redaction | PASS | `Boundary_sensitivity_message_should_stay_unredacted` and `Boundary_sensitivity_event_should_stay_unredacted` (6 DataRows each, original values asserted intact through the scanner); `IsSensitive_should_be_false_for_non_sensitive_and_out_of_range` at transform level |

### Spec — Toolchain and coverage

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| T1 | Full toolchain passes in a single pass; line coverage >= 85%, branch coverage >= 75%, changed lines covered with no regression | **PARTIAL** | Toolchain: PASS in a single pass, reviewer-re-run at head (format EXIT 0; build 0/0; architecture 2/2; 647/647 tests; contract N/A — wire shape unchanged; integration = cache round-trip tests). Pooled coverage 90.51% line / 79.60% branch (PASS, no regression: +0.25/+0.24 vs baseline); all changed lines covered. However, the uniform coverage gate applied per new file (quality-tiers.md) is not met: NEW `OutlookScanner.Redaction.cs` is 100% line / **71.43% branch (10/14) < 75%**. The executor checked this item off based on per-file line-only measurement; the reviewer's per-file branch re-measurement (`evidence/qa-gates/coverage-review.2026-07-02T09-45.md`) shows the shortfall. Blocking policy finding; remediation required. |

### User-story acceptance criteria (`user-story.md`)

| # | Criterion (abbreviated) | Verdict | Evidence |
|---|---|---|---|
| US1 | Sensitivity 2/3 stored and served with placeholder subject, fields withheld, flags set — in both modes | PASS | A1/A3 (stored) + A6 (round-trip) + C1/C2 (served in both modes) |
| US2 | Redacted item remains usable as busy block (times, busy status, meeting status, recurrence, ids, sensitivity, label preserved) | PASS | A2/A4 retention tests (scanner + transform level) |
| US3 | Content never ingested; no body read, sender resolution, or attendee enumeration; logged by bridge id only | PASS | A5 never-ingest assertions + A7 log assertions |
| US4 | Sensitivity 0, 1, null, out-of-range completely unaffected | PASS | C5 boundary tests (message + event, 6 values each) |
| US5 | Safe mode suppresses the complete protected field set and signals via `protected_fields_available: false` without setting `is_redacted` | PASS | B1/B3 suppression tests + C3 never-sets assertion |
| US6 | `is_redacted` means exactly one thing and survives shaping in both modes | PASS | C1/C2/C3 invariant tests; enhanced/safe branches contain no `IsRedacted` assignment (code inspection) |

## Summary

- **Spec AC:** 18 of 19 PASS; 1 PARTIAL (T1, toolchain-and-coverage — new-file branch coverage 71.43% < 75% despite all pooled/package/modified-file gates passing).
- **User-story AC:** 6 of 6 PASS.
- All PASS verdicts are backed by named tests in the reviewer's own 647/647 run at branch head, and Groups A/B/C each have fail-before regression evidence (EXIT 1 against pre-change code).
- The delivered behavior matches the spec field-for-field, including the staleness-reconciliation delta table for post-#71/#72/#73 fields, the Location-retained decision, and the empty-categories invariant.
- Remediation (expected test-only) is required before PR: (1) sensitive meeting-message normalization tests to close the new-file branch-coverage gap; (2) T2 property-test density for the three new pure functions (or a recorded exception). See `remediation-inputs.2026-07-02T09-45.md` and the policy audit.

## Acceptance Criteria Check-off

Per `acceptance-criteria-tracking`, the reviewer checks off criteria evaluated PASS and leaves unmet criteria unchecked.

- All 19 spec items and all 6 user-story items were already checked `[x]` by the executor during plan execution; no new check-offs were needed for the 24 PASS items.
- **Discrepancy recorded:** spec item T1 ("Full toolchain passes ... line coverage >= 85%, branch coverage >= 75% ...") is evaluated **PARTIAL** by this audit but is currently checked `[x]` in `spec.md` (and its mirror in `issue.md`). The executor's check-off predates the reviewer's per-file branch measurement. Per the check-off protocol the reviewer does not add phantom criteria and does not modify criterion text; the disputed status is recorded here and in the remediation inputs rather than by editing the source files. The remediation cycle's re-audit should confirm the criterion after the coverage gap closes.

### Acceptance Criteria Status
- Source: `docs/features/active/sensitivity-redaction-18/spec.md`, `docs/features/active/sensitivity-redaction-18/user-story.md`
- Total AC items: 25 (19 spec + 6 user-story)
- Checked off (delivered): 25 (all pre-checked by executor; 24 confirmed PASS by this audit)
- Remaining (unchecked): 0
- Items remaining: none unchecked; 1 checked item disputed as PARTIAL (spec T1, toolchain-and-coverage) pending remediation
