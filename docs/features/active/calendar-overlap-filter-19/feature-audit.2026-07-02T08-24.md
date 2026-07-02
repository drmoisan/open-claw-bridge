# Feature Audit: calendar-overlap-filter (#19)

**Audit Date:** 2026-07-02
**Auditor:** feature-review agent
**Work Mode:** `full-bug` (persisted marker `- Work Mode: full-bug` in `issue.md`)

---

## Scope and Baseline

- **Feature branch:** `bug/calendar-overlap-filter-19` @ head `d7fc69a31b441c9a5d98abf693ef6d00916134e1`
- **Resolved base branch:** `main` (supplied by caller; consistent with `pr_context.summary.txt` "Base ref (resolved): origin/main")
- **Merge-base SHA:** `1bc4148867bd757b724af503b59a3a19bc6f37b4`
- **Diff range:** `1bc4148867bd757b724af503b59a3a19bc6f37b4..d7fc69a31b441c9a5d98abf693ef6d00916134e1`
- **PR-context artifacts:** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`, refreshed at head `d7fc69a` (matches current HEAD; not stale).
- **Changed surface:** 1 modified production C# file (one-line predicate fix), 1 new C# test file, 20 Markdown files (feature docs/evidence, agent memory). Working tree clean at review time.
- **AC source (per `full-bug` work mode):** `spec.md` `## Acceptance Criteria` only. A 5-item mirror in `issue.md` is noted for consistency (it condenses spec AC-4/AC-5 and AC-6/AC-7 pairs); `spec.md` is authoritative. `user-story.md` carries no trackable AC by its own declaration.

## Acceptance Criteria Inventory

From `docs/features/active/calendar-overlap-filter-19/spec.md` `## Acceptance Criteria` (7 checkbox items, all currently marked `[x]` by the executor):

- **AC-1:** The calendar filter/selection includes: (a) events starting within the window, (b) events starting before the window but ending inside it, and (c) events spanning the entire window.
- **AC-2:** The calendar filter/selection excludes events ending at-or-before windowStart and events starting at-or-after windowEnd (boundary semantics explicit: `End == windowStart` excluded, `Start == windowEnd` excluded).
- **AC-3:** A regression test in `tests/OpenClaw.MailBridge.Tests/` fails before the fix and passes after it, exercising the pure filter-string builder and/or a post-filter predicate, with no live Outlook COM dependency (file path and test names recorded). Recorded in the spec: `OutlookScannerCalendarOverlapFilterTests.cs`; tests `ScanCalendarAsync_emits_interval_overlap_restrict_filter` and `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` (5 DataRow boundary cases).
- **AC-4:** Date formatting (`MM/dd/yyyy hh:mm tt`, `LocalDateTime` conversion) and the timezone handling established by the #55 fix are unchanged; all existing tests pass unchanged.
- **AC-5:** No behavior change outside `BuildCalendarFilter` (and, only if required, a documented post-filter pass in the calendar scan path).
- **AC-6:** Full C# toolchain passes in a single clean pass: CSharpier check -> `dotnet build` (analyzers, nullable, warnings-as-errors) -> architecture tests -> `dotnet test` with coverage.
- **AC-7:** Line coverage >= 85% and branch coverage >= 75% hold, with all changed lines covered; coverage baseline/post/comparison evidence stored under `docs/features/active/calendar-overlap-filter-19/evidence/coverage/`.

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|----|---------|----------|
| AC-1 | PASS | The fixed predicate `[Start] < '<windowEnd>' AND [End] > '<windowStart>'` (diff, `OutlookScanner.Helpers.cs` line 49) admits all three inclusion cases. Verified behaviorally by DataRows (1, 1.5, True), (-1, 0.5, True), (-1, 38, True) evaluated against the actually emitted filter, all passing at head (reviewer run). Fail-before evidence shows the in-progress and window-spanning rows fail under the old predicate — proving the criterion is delivered by this change, not pre-existing. |
| AC-2 | PASS | Strict `<`/`>` operators in the emitted filter; DataRows (-1, 0, False) (`End == windowStart`) and (37, 38, False) (`Start == windowEnd`) both pass at head, and both also passed pre-fix (fail-before artifact), confirming exclusion semantics were preserved rather than loosened. |
| AC-3 | PASS | New file `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs` exercises the filter through `ScanCalendarAsync` with fake COM doubles only (`FakeComActiveObject`; no live Outlook). Fail-before: `evidence/regression-testing/regression-fail-before.2026-07-02T07-59.md`, EXIT_CODE 1, 3 named failing tests. Pass-after: `evidence/regression-testing/regression-pass-after.2026-07-02T08-00.md`, EXIT_CODE 0, 6/6. File path and test names are recorded in spec AC-3 and mirrored in issue.md (plan P3-T1/P3-T2). |
| AC-4 | PASS | Diff preserves `LocalDateTime` conversion and the `MM/dd/yyyy hh:mm tt` format verbatim; the #55 normalization path (`OutlookComHelpers`) is untouched by the diff. The exact-string regression test pins the format. Full suite after fix: 596 passed / 0 failed / 5 pre-existing skips with no existing test file modified (`full-suite-after-fix.2026-07-02T08-00.md`; independently reproduced by the reviewer at head, including `OutlookScannerCalendarUtcTests`). |
| AC-5 | PASS | The production diff is exactly one expression body in `BuildCalendarFilter` (+1/-1); no post-filter pass was needed or added. `ScanCalendarAsync` (`Sort`, `IncludeRecurrences`, `Restrict` ordering) unchanged, verified by direct diff and file read. Scope-verification artifact (`scope-verification.2026-07-02T08-04.md`) enumerates the full change set and matches the authoritative `git diff --stat` (22 files: 2 C#, 20 Markdown). |
| AC-6 | PASS | Executor Phase 4 single-pass loop artifacts: `final-csharpier-format` / `final-csharpier-check` (EXIT 0, no files reformatted), `final-build` (0W/0E), `final-test-coverage` (596 passed, architecture tests included in the solution run) — all at 2026-07-02T08-02. Reviewer independently re-ran the full chain at head `d7fc69a` in one clean pass: `csharpier check .` EXIT 0 (195 files); `dotnet build` 0W/0E; NetArchTest subset 2/2; full `dotnet test` with coverage 596/0/5. |
| AC-7 | PASS | Thresholds (reviewer re-measured from fresh cobertura): pooled 90.26% line >= 85%, 79.36% branch >= 75%; `OpenClaw.MailBridge` package 92.40%/84.62%; changed file `OutlookScanner.Helpers.cs` 100% line / 100% branch; the single changed line (49) covered with 56 hits; zero regression versus baseline (values identical). Evidence: baseline `evidence/baseline/baseline-test-coverage.2026-07-02T07-55.md`, post `evidence/qa-gates/final-test-coverage.2026-07-02T08-02.md`, comparison `evidence/qa-gates/coverage-comparison.2026-07-02T08-03.md`, reviewer `evidence/qa-gates/coverage-review.2026-07-02T08-24.md`. Path note: the AC text names `evidence/coverage/`, which is not a canonical evidence kind in this repo; the plan's "Evidence Locations" section documents the mapping of coverage evidence to the canonical `evidence/baseline/` and `evidence/qa-gates/` kinds. The substantive requirement — thresholds met with recorded baseline/post/comparison evidence at canonical locations — is fully satisfied; the documented path mapping does not reduce the verdict. |

## Summary

All 7 acceptance criteria from the authoritative `spec.md` source evaluate to **PASS** with direct diff, test, and coverage evidence, each independently re-verified by the reviewer at branch head rather than accepted solely from executor evidence. The fix is a one-line predicate correction with a discriminating fail-before/pass-after regression suite; scope discipline is exact (no changes to `BuildInboxFilter`, timezone handling, `FreeBusyProjection`, or any named non-goal). The policy audit (`policy-audit.2026-07-02T08-24.md`) found no Blocking or material PARTIAL findings, and the code review (`code-review.2026-07-02T08-24.md`) recorded one Minor pre-existing observation (culture-dependent Restrict formatting, follow-up recommended) and two no-action Informational notes. Remediation is not required.

**Go/no-go recommendation: Go — ready for PR to `main`.**

## Acceptance Criteria Check-off

- Source file: `docs/features/active/calendar-overlap-filter-19/spec.md` (authoritative for `full-bug`); a condensed 5-item mirror exists in `issue.md`; `user-story.md` defers to spec.md by design.
- All 7 AC checkboxes were already marked `[x]` by the executor (issue.md mirror: all 5 marked `[x]`). The reviewer verified each criterion independently and confirms every `[x]` is justified; no check-off edits were needed and none were made. No criteria remain unchecked; no phantom criteria were added.

### Acceptance Criteria Status
- Source: docs/features/active/calendar-overlap-filter-19/spec.md
- Total AC items: 7
- Checked off (delivered): 7
- Remaining (unchecked): 0
- Items remaining: none
