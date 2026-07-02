# Feature Audit: sensitivity-redaction (#18, co-delivers #20) — Remediation Cycle 1 Re-audit (R4)

**Audit Date:** 2026-07-02
**Auditor:** feature-review agent

## Scope and Baseline

- **Feature branch:** `feature/sensitivity-redaction-18` @ head `82504ff12a8ccda9ac64d0535356769c8f1b01fa` (includes remediation cycle 1 commit `82504ff`, test-only)
- **Resolved base branch:** `main` (`origin/main`) @ merge-base `8c969f1a6e96120dd95f835a289c8b185abee202`
- **Diff range:** `8c969f1a6e96120dd95f835a289c8b185abee202..82504ff12a8ccda9ac64d0535356769c8f1b01fa` (66 files)
- **Work mode:** `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` **and** `user-story.md` per `acceptance-criteria-tracking`. `issue.md` mirrors the spec AC verbatim.
- **Primary evidence:** `artifacts/pr_context.summary.txt`, `artifacts/pr_context.appendix.txt` (refreshed for this head), executor evidence under `docs/features/active/sensitivity-redaction-18/evidence/**` including the remediation-cycle artifacts (`remediation-baseline/`, `qa-gates/*.2026-07-02T10-11.md`, `other/property-test-decision.2026-07-02T10-07.md`), and the reviewer's independent toolchain/coverage re-run at this head (`evidence/qa-gates/coverage-review.2026-07-02T10-23.md`).
- **Verification model:** every criterion below is mapped to named tests executed in the reviewer's own run (660 passed / 0 failed / 5 env-gated skips at head `82504ff`) and, where applicable, to fail-before regression evidence and reviewer-parsed per-file line-and-branch coverage.
- **Relation to prior audit:** the 2026-07-02T09-45 audit evaluated 24 of 25 items PASS and spec item T1 PARTIAL (new-file branch coverage 71.43% < 75%). This re-audit re-verifies the full item set at the new head and re-evaluates T1 against the remediation delivery.

## Acceptance Criteria Inventory

Source 1 — `spec.md` `## Acceptance Criteria` (19 checkbox items, all currently `[x]`):

- Group A — Normalization-time sensitivity redaction (#18): A1-A7
- Group B — Safe-mode shaping suppression (#20): B1-B6
- Group C — Composition invariants (#18 x #20): C1-C5
- Toolchain and coverage: T1 (single item; unchecked at remediation cycle start per plan P0-T6, re-checked at P3-T7 with the dated sub-bullet "Re-verified 2026-07-02T10-11 (remediation cycle 1)" citing `evidence/qa-gates/coverage-remediation-verification.2026-07-02T10-11.md`)

Source 2 — `user-story.md` `## Acceptance Criteria` (6 checkbox items, all currently `[x]`): US1-US6.

Note: `spec.md` also contains a 7-item `## Definition of Done` checklist that remains unchecked. It is not under the `## Acceptance Criteria` heading and is not an AC source; its substance (tests added, docs updated, logging, toolchain pass) is verified by the evidence cited throughout this audit. Recorded as an observation only; the reviewer does not check off non-AC checklists.

## Acceptance Criteria Evaluation

### Spec Groups A, B, C (A1-A7, B1-B6, C1-C5) — re-verified unchanged

The production code implementing Groups A, B, and C is byte-identical to the prior audit (`git diff --name-only d267c66..82504ff` contains zero `src/` paths). All named tests cited in the prior audit's per-item evaluation re-ran and passed in the reviewer's 660/660 run at this head, and the fail-before regression dossiers under `evidence/regression-testing/` are unchanged. The per-item verdicts and evidence in `feature-audit.2026-07-02T09-45.md` therefore carry forward: **A1-A7 PASS, B1-B6 PASS, C1-C5 PASS** (18 items).

Remediation cycle 1 additionally strengthened two of these items beyond their prior evidence:

| # | Criterion (abbreviated) | Verdict | New evidence this cycle |
|---|---|---|---|
| A2 | `NormalizeMessage` retains all 11 mechanical fields on redacted messages | PASS (gap closed) | The prior audit noted retention of `MeetingMessageType`/meeting `ItemKind` was proven only at transform level. Now proven through the scanner: `Sensitive_meeting_message_should_be_redacted_with_meeting_kind` (DataRows 2, 3) asserts `ItemKind == "meeting"`, meeting-variant `BridgeId`, and retained `MeetingMessageType == 1` end-to-end. |
| A5 | Sensitive normalization never reads protected COM content | PASS (strengthened) | Hard never-ingest: `Sensitive_mail_scan_with_throwing_protected_members_should_stay_redacted` and `Sensitive_event_scan_with_throwing_protected_members_should_stay_redacted` set `ThrowOnProtectedAccess = true`, proving any protected access would have failed the scan rather than merely being recorded. |

### Spec — Toolchain and coverage

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| T1 | Full toolchain passes in a single pass; line coverage >= 85%, branch coverage >= 75%, changed lines covered with no regression | **PASS** | Toolchain (reviewer re-run at head `82504ff`, single consecutive pass): format `csharpier check .` EXIT 0 (204 files); build 0 warnings / 0 errors; architecture 2/2; tests 660/660 runnable; contract N/A (wire shape unchanged); integration = cache round-trip tests. Coverage (reviewer-parsed fresh cobertura): pooled 90.51% line / 79.95% branch (gates 85%/75%; no regression — branch +0.59% vs feature baseline, +4 conditions vs pre-remediation); NEW `OutlookScanner.Redaction.cs` **100% line (109/109) / 100% branch (14/14)** — the prior 71.43% shortfall is closed; modified files 90.73%/90.00%, 100%/100%, 100%/100% with changed lines covered. Executor verification: `evidence/qa-gates/coverage-remediation-verification.2026-07-02T10-11.md`; reviewer confirmation: `evidence/qa-gates/coverage-review.2026-07-02T10-23.md` (numbers identical). The T2 property-test obligation attached to this branch's new pure functions is satisfied per the policy audit Section 4 (delivered invariant suites under the recorded option (b) decision). |

### User-story acceptance criteria (`user-story.md`)

| # | Criterion (abbreviated) | Verdict | Evidence |
|---|---|---|---|
| US1 | Sensitivity 2/3 stored and served with placeholder subject, fields withheld, flags set — in both modes | PASS | A1/A3 (stored) + A6 (round-trip) + C1/C2 (served in both modes); re-verified at head. |
| US2 | Redacted item remains usable as busy block (times, busy status, meeting status, recurrence, ids, sensitivity, label preserved) | PASS | A2/A4 retention tests, now including scanner-level meeting-message retention and the invariant-matrix mechanical-preservation suites (all 11 message / all 16 event fields across 4 variants each). |
| US3 | Content never ingested; no body read, sender resolution, or attendee enumeration; logged by bridge id only | PASS | A5 never-ingest assertions — now including the hard throw-on-access variants — plus A7 log assertions. |
| US4 | Sensitivity 0, 1, null, out-of-range completely unaffected | PASS | C5 boundary tests (scanner level, 6 values each kind) plus the invariant suite's 11-value `IsSensitive` full-domain equivalence (adds `int.MinValue`, 5, `int.MaxValue`). |
| US5 | Safe mode suppresses the complete protected field set and signals via `protected_fields_available: false` without setting `is_redacted` | PASS | B1/B3 suppression tests + C3 never-sets assertion; re-verified at head. |
| US6 | `is_redacted` means exactly one thing and survives shaping in both modes | PASS | C1/C2/C3 invariant tests; production shaper unchanged since prior audit. |

## Summary

- **Spec AC: 19 of 19 PASS** (prior cycle: 18 PASS, 1 PARTIAL — T1 now PASS after remediation).
- **User-story AC: 6 of 6 PASS.**
- All PASS verdicts are backed by named tests in the reviewer's own 660/660 run at branch head `82504ff`, fail-before regression evidence for Groups A/B/C, and reviewer-independent per-file line-and-branch coverage re-measurement.
- Remediation cycle 1 was verified test-only (zero `src/` paths in the delta) and delivered: the sensitive meeting-message scanner tests (Blocking fix), the deterministic invariant suites for the three pure functions (Major fix, directed option (b) with dated decision record), and the hard never-ingest tests (bundled Minor fix).
- The remediation-loop exit condition from `remediation-inputs.2026-07-02T09-45.md` is met: `OutlookScanner.Redaction.cs` branch coverage >= 75% with per-file line AND branch evidence recorded (100%/100%); Fix 2 resolved via delivered invariant tests under the recorded, directive-backed exception; full toolchain single-pass clean at the new head; zero blocking findings.
- **Recommendation: Go for PR.** See `policy-audit.2026-07-02T10-23.md` (COMPLIANT) and `code-review.2026-07-02T10-23.md` (Approve).

## Acceptance Criteria Check-off

Per `acceptance-criteria-tracking`, the reviewer checks off criteria evaluated PASS and leaves unmet criteria unchecked.

- All 19 spec items and all 6 user-story items are checked `[x]` in the source files; all 25 are evaluated PASS by this audit. No new check-offs were needed.
- The prior audit's recorded discrepancy on spec item T1 (checked while the per-file branch gate was failing) is resolved: the remediation plan unchecked the item at cycle start (P0-T6), delivered the fix, and re-checked it only after the coverage verification passed (P3-T7), appending the dated re-verification sub-bullet citing `evidence/qa-gates/coverage-remediation-verification.2026-07-02T10-11.md`. This audit independently confirms the underlying gate at the current head, so the `[x]` state is now evidence-backed.
- No AC text was modified in either source file (verified: the remediation delta touches spec.md by exactly one added sub-bullet line; criterion wording is byte-identical).

### Acceptance Criteria Status
- Source: `docs/features/active/sensitivity-redaction-18/spec.md`, `docs/features/active/sensitivity-redaction-18/user-story.md`
- Total AC items: 25 (19 spec + 6 user-story)
- Checked off (delivered): 25
- Remaining (unchecked): 0
- Items remaining: none
