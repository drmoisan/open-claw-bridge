# Feature Audit: core-response-status-roundtrip (#80)

**Audit Date:** 2026-07-02
**Auditor:** feature-review agent
**Work Mode:** `full-bug` (persisted marker `- Work Mode: full-bug` in `issue.md`)

---

## Scope and Baseline

- **Feature branch:** `bug/core-response-status-roundtrip-80` @ head `99ae0d66e9af9f6c33fdd2ecd1a1229e9d6c3615`
- **Resolved base branch:** `main` (supplied by caller; consistent with `pr_context.summary.txt` "Base ref (resolved): origin/main")
- **Merge-base SHA:** `2a6031f46e16ad51960721c631268eb756621b72`
- **Diff range:** `2a6031f46e16ad51960721c631268eb756621b72..99ae0d66e9af9f6c33fdd2ecd1a1229e9d6c3615`
- **PR-context artifacts:** `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`, refreshed at head `99ae0d6` (matches current HEAD; not stale).
- **Changed surface:** 2 modified production C# files, 1 new C# test file, 18 Markdown files (feature docs/evidence, research doc, agent memory). Working tree clean at review time.
- **AC source (per `full-bug` work mode):** `spec.md` `## Acceptance Criteria` only. The identical AC list mirrored in `issue.md` and `user-story.md` is noted for consistency but `spec.md` is authoritative.

## Acceptance Criteria Inventory

From `docs/features/active/core-response-status-roundtrip-80/spec.md` `## Acceptance Criteria` (5 checkbox items, all currently marked `[x]` by the executor):

- **AC-1:** The Core `events` schema includes a `response_status INTEGER NULL` column in both `CreateTablesSql` (fresh database) and an idempotent, guarded ALTER migration in `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (existing-database upgrade); running `InitializeAsync` twice on the same database raises no error.
- **AC-2:** `EventDto.ResponseStatus` survives a Core cache write/read round-trip (`UpsertEventsAsync` then `GetEventAsync`) for a non-null value (e.g. 4 = Declined) and for null (read back as null, not 0).
- **AC-3:** No behavior change for any other `EventDto` field: all existing tests in `tests/OpenClaw.Core.Tests/` pass unchanged.
- **AC-4:** A regression test in `tests/OpenClaw.Core.Tests/` (mirroring `tests/OpenClaw.MailBridge.Tests/CacheRepositoryResponseStatusTests.cs`) fails before the fix and passes after.
- **AC-5:** Full C# toolchain passes: `dotnet csharpier check .`, `dotnet build` (analyzers clean), `dotnet test --collect:"XPlat Code Coverage"`; line coverage >= 85%, branch coverage >= 75%, and all changed lines are covered.

## Acceptance Criteria Evaluation

| AC | Verdict | Evidence |
|----|---------|----------|
| AC-1 | PASS | Diff shows `response_status INTEGER NULL` in the `events` `CREATE TABLE` inside `CreateTablesSql` and a guarded ALTER (`EventsColumnExistsAsync` / `PRAGMA table_info` guard) at the start of `MigrateEventsSchemaAsync` in `CoreCacheRepository.Schema.cs`. Test `InitializeAsync_should_add_response_status_column_to_existing_database` seeds a pre-#80 database shape, runs `InitializeAsync` twice, and asserts `NotThrowAsync` — passed in the executor run and in the reviewer's independent run at head (213/213 Core.Tests). |
| AC-2 | PASS | Tests `UpsertEvents_then_GetEvent_should_round_trip_response_status_when_declined` (writes 4, reads 4) and `..._when_null` (writes null, asserts `BeNull()` — not 0) both pass at head (reviewer run). Wiring verified in diff: `$response_status` in INSERT/VALUES/`DO UPDATE SET`, bound via `ToDbValue(evt.ResponseStatus)`, read via `ReadNullableInt(reader, "response_status")`. |
| AC-3 | PASS | Baseline suite: 210 Core.Tests, all passing (`evidence/baseline/baseline-test-coverage.2026-07-01T22-16.md`). Post-change: 213 = 210 unchanged pre-existing tests + 3 new, all passing; full solution 590 passed / 0 failed / 5 pre-existing environment-gated skips, identical skip set to baseline. Independently confirmed by the reviewer's full test run at head 99ae0d6. No existing test file was modified in the diff. |
| AC-4 | PASS | New test file `tests/OpenClaw.Core.Tests/CoreCacheRepositoryResponseStatusTests.cs` mirrors the bridge-side reference test. Fail-before: `evidence/regression-testing/regression-fail-before.2026-07-01T22-16.md`, EXIT_CODE 1, tests 1 and 3 failing with expected-4-actual-null against pre-fix code (test 2's pre-fix pass is explicitly explained in the artifact as an artifact of the hardcoded-null defect). Pass-after: `evidence/regression-testing/regression-pass-after.2026-07-01T22-16.md`, EXIT_CODE 0, 3/3 passing. |
| AC-5 | PASS | Reviewer independently re-ran at head: `csharpier check .` EXIT 0 (194 files); `dotnet build OpenClaw.MailBridge.sln` 0 warnings / 0 errors (analyzers + nullable as errors); full `dotnet test` 590 passed / 0 failed. Coverage (reviewer cobertura, fresh run): pooled 90.26% line >= 85%, 79.36% branch >= 75%; changed files Schema.cs 100%/100%, Events.cs 97.14%/93.75% with changed lines 187 and 250 covered (hits 18 and 13) and the only uncovered lines (213-215) pre-existing and untouched. Evidence: `evidence/qa-gates/coverage-review.2026-07-02T07-35.md`, `evidence/qa-gates/final-csharpier.2026-07-01T22-16.md`, `evidence/qa-gates/final-build.2026-07-01T22-16.md`, `evidence/qa-gates/final-test-coverage.2026-07-01T22-16.md`. |

## Summary

All 5 acceptance criteria from the authoritative `spec.md` source evaluate to **PASS** with direct diff, test, and coverage evidence, each independently re-verified by the reviewer at branch head rather than accepted solely from executor evidence. The fix is minimal and scope-disciplined: no changes to `EventDto`, the bridge cache, consumer logic, or any non-goal surface. The policy audit (`policy-audit.2026-07-02T07-35.md`) found no Blocking or material PARTIAL findings, and the code review (`code-review.2026-07-02T07-35.md`) recorded only two no-action Informational observations. Remediation is not required.

**Go/no-go recommendation: Go — ready for PR to `main`.**

## Acceptance Criteria Check-off

- Source file: `docs/features/active/core-response-status-roundtrip-80/spec.md` (authoritative for `full-bug`); identical mirrors in `issue.md` and `user-story.md`.
- All 5 AC checkboxes were already marked `[x]` by the executor. The reviewer verified each criterion independently and confirms every `[x]` is justified; no check-off edits were needed and none were made. No criteria remain unchecked; no phantom criteria were added.

### Acceptance Criteria Status
- Source: docs/features/active/core-response-status-roundtrip-80/spec.md
- Total AC items: 5
- Checked off (delivered): 5
- Remaining (unchecked): 0
- Items remaining: none
