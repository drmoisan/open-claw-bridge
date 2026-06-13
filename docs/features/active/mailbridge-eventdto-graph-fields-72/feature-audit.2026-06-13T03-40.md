# Feature Audit: mailbridge-eventdto-graph-fields (#72)

**Audit Date:** 2026-06-13
**Feature Folder:** `docs/features/active/mailbridge-eventdto-graph-fields-72`
**Base Branch:** `main`
**Head Branch:** `open-claw-bridge-wt-2026-06-12-22-12` (commit `c92fae9b82adaeebfe7bcab4d4b9783aa0e19ff4`)
**Work Mode:** `full-feature`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `3041d083691cd77b2b2e888580fc9f2ab8bc611f`)
- **Head branch/commit:** `open-claw-bridge-wt-2026-06-12-22-12` (commit `c92fae9b82adaeebfe7bcab4d4b9783aa0e19ff4`)
- **Merge base:** `3041d083691cd77b2b2e888580fc9f2ab8bc611f`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/**`
  - Additional evidence: `git diff 3041d08..c92fae9`; cobertura files under `tests/**/TestResults/`
- **Feature folder used:** `docs/features/active/mailbridge-eventdto-graph-fields-72`
- **Requirements source:** `user-story.md` (AC1-AC6) and `spec.md` Definition of Done
- **Work mode resolution note:** `issue.md` is absent from the feature folder. Per the SKILL fail-closed rule, a missing/malformed work-mode marker defaults to `full-feature` (sources: `spec.md` and `user-story.md`). The caller also explicitly specified full-feature. AC source files were read directly.
- **Scope note:** Audit scope is the full feature-vs-base branch diff. The PR-context summary's "Core logic changes: 0 files" classification was rejected as inaccurate (recorded in the policy audit under `## Rejected Scope Narrowing`); the authoritative `git diff` shows 12 C# source files and 7 C# test files. Coverage was verified from pre-existing executor artifacts and corroborated by cobertura files rather than re-run.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/mailbridge-eventdto-graph-fields-72/user-story.md` — primary (AC1-AC6)
- `docs/features/active/mailbridge-eventdto-graph-fields-72/spec.md` — secondary (Definition of Done)

### From user-story.md (## Acceptance Criteria)

1. `EventDto` exposes all nine new fields with the specified types and remains source-compatible (all existing in-repo callers compile without modification to their call sites).
2. `OutlookScanner.NormalizeEvent` populates all nine fields from the specified COM analogs/derivations.
3. `ResponseShaper.ShapeEvent` nulls `bodyFull` in safe mode (redaction parity with `BodyPreview`); enhanced mode returns the full untruncated `bodyFull`.
4. Both SQLite caches (`CacheRepository`, `CoreCacheRepository`) round-trip all nine new fields (write then read returns the same values), with idempotent schema migrations.
5. A scan of a recurring online meeting yields non-null `iCalUId`, `isOnlineMeeting=true`, and the correct `sensitivityLabel`.
6. Existing contract tests pass; new unit tests cover the new fields, the safe/enhanced shaping of `bodyFull`, and the cache round-trip. Coverage thresholds hold: line >= 85%, branch >= 75% (T2).

### From spec.md (## Definition of Done)

- Acceptance criteria documented and mapped to tests or demos
- Behavior matches acceptance criteria in all documented environments
- Tests updated/added (unit/integration as applicable)
- Edge cases and error handling covered by tests
- Docs updated
- Telemetry/logging added or updated (if applicable)
- Toolchain pass completed (format -> lint -> type-check -> test)

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC1 | `EventDto` exposes nine new fields, source-compatible | PASS | `BridgeContracts.cs` appends nine optional keyword-defaulted parameters after `ResponseStatus`; analyzer + nullable builds green with no call-site edits | `dotnet build ... -p:TreatWarningsAsErrors=true` (`final-typecheck.md` EXIT 0) | Types match spec (`string[]?`, `bool`, `DateTimeOffset?`, `string?`). |
| AC2 | `OutlookScanner.NormalizeEvent` populates all nine fields | PASS | `OutlookScanner.GraphFields.cs` `BuildEventDto` derives all nine from COM analogs; `OutlookScannerGraphFieldsTests` covers each | `dotnet test ...` (`phase2-toolchain.md`, `final-test.md`) | Body read once; isOrganizer = ResponseStatus==1; seriesMasterId from RecurrenceState. |
| AC3 | `ResponseShaper.ShapeEvent` safe-nulls bodyFull; enhanced returns full untruncated | PASS | `ResponseShaper.cs` nulls `BodyFull` with `BodyPreview` in safe mode; enhanced leaves full body; `ResponseShaperEventBodyFullTests` asserts both, including a body exceeding the preview cap | `dotnet test ...` (`phase3-toolchain.md`) | Redaction parity verified; not routed through `BodySanitizer.NormalizePreview`. |
| AC4 | Both caches round-trip nine fields with idempotent migrations | PASS | `CacheRepository.*`/`CoreCacheRepository.*` write+read all columns; guarded `PRAGMA table_info` ALTER; `CacheRepositoryGraphFieldsTests`/`CoreCacheRepositoryGraphFieldsTests` cover populated, empty-categories, and double-init idempotency | `dotnet test ...` (`phase4-toolchain.md`, `phase5-toolchain.md`) | Categories serialized as JSON column; bridge `last_modified_utc` write wired. |
| AC5 | Recurring online meeting yields non-null iCalUId, isOnlineMeeting=true, correct sensitivityLabel | PASS | `OutlookScannerGraphFieldsTests.NormalizeEvent_recurring_online_meeting_yields_expected_graph_fields` asserts ICalUId="gid-graph", IsOnlineMeeting=true, SensitivityLabel="private", SeriesMasterId="gid-graph" | `dotnet test ...` | Mapper-side corroboration in `SchedulingDtoMapperTests`. |
| AC6 | Existing contract tests pass; new tests cover fields/shaping/round-trip; coverage line >= 85%, branch >= 75% (T2) | PASS | 459 pass / 0 fail / 3 skipped; MailBridge 93.55% line / 85.47% branch; Core 89.09% line / 77.59% branch; new files 100% | `dotnet test ... --collect:"XPlat Code Coverage"` (`final-test.md`, `coverage-delta.md`; cobertura 0.9355/0.8547 and 0.8909/0.7759) | Thresholds held; no changed-line regression. |
| DoD | spec.md Definition of Done (7 items) | PASS | acceptance-traceability.md maps each item; toolchain gates green; docs/remarks updated; no telemetry required | see Appendix B of policy audit | All seven DoD items satisfied. |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 6 user-story criteria + spec Definition of Done
- **PARTIAL:** 0
- **UNVERIFIED:** 0
- **FAIL:** 0

**Top gaps preventing PASS:**

1. None. (A non-blocking, pre-existing file-size condition on `CoreCacheRepository.cs` is recorded in the policy audit and code review as a follow-up; it does not affect any acceptance criterion.)

**Recommended follow-up verification steps:**

1. Open a follow-up refactor issue to split `CoreCacheRepository.cs` (687 lines) below the 500-line cap.
2. Confirm CI green status against the branch head in the orchestrator's S9 gate (CI status was "not available" in the PR-context artifact; no workflow files changed, so the `modified-workflow-needs-green-run` rule does not fire).

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- All six PASS criteria are already checked `[x]` in `user-story.md`, and all seven Definition-of-Done items are already checked `[x]` in `spec.md`. They were verified as delivered during this audit and require no state change.
- No PARTIAL/FAIL/UNVERIFIED criteria exist, so no items remain unchecked.

### AC Status Summary

- Source: `docs/features/active/mailbridge-eventdto-graph-fields-72/user-story.md`, `docs/features/active/mailbridge-eventdto-graph-fields-72/spec.md`
- Total AC items: 6 (user-story) + 7 (spec DoD) = 13
- Checked off (delivered): 13
- Remaining (unchecked): 0
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `user-story.md` | 6 | 6 | 0 | Checkbox-backed; already `[x]`, verified PASS this audit |
| `spec.md` | 7 (DoD) | 7 | 0 | Checkbox-backed; already `[x]`, verified PASS this audit |
