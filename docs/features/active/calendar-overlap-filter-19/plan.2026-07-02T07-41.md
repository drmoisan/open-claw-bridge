# calendar-overlap-filter (Plan)

- **Issue:** #19
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T07-41
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-bug (per `issue.md` metadata; `spec.md` is the authoritative acceptance-criteria source)

**Fail-closed evidence rule:** Include explicit baseline artifact tasks, final-QA artifact tasks, and coverage-comparison tasks for each in-scope language when policy requires coverage. If any required baseline artifact, QA artifact, or coverage-comparison artifact is missing, the audit verdict must be BLOCKED or INCOMPLETE, never PASS.

**Evidence accounting rule:** Record the expected artifact path or location in each evidence-producing task. Do not mark evidence-backed work complete without the artifact.

## Scope

- Bug: `OutlookScanner.BuildCalendarFilter` (`src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs`, lines 48–49) restricts the calendar scan with a start-only predicate `[Start] >= '<start>' AND [Start] < '<end>'`, excluding in-progress and window-spanning events.
- Fix: minimal Restrict-string change to the interval-overlap predicate `[Start] < '<windowEnd>' AND [End] > '<windowStart>'`, preserving the existing `'{value.LocalDateTime:MM/dd/yyyy hh:mm tt}'` formatting exactly.
- Only in-scope language: C#. Only production file changed: `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs`. New tests only under `tests/OpenClaw.MailBridge.Tests/`. No opportunistic refactors. No live Outlook COM in tests. No temporary files in tests. MSTest + FluentAssertions (existing test-project conventions). 500-line file cap applies.
- Consumer context (unchanged): `src/OpenClaw.MailBridge/OutlookScanner.cs` `ScanCalendarFolderAsync` (lines ~278–284) keeps `Sort("[Start]")`, `IncludeRecurrences = true`, then `Restrict(filter)`.

## Evidence Locations (canonical, non-overridable)

All evidence artifacts are written under `docs/features/active/calendar-overlap-filter-19/evidence/<kind>/` per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`:

- Baseline: `docs/features/active/calendar-overlap-filter-19/evidence/baseline/`
- Regression testing (fail-before / pass-after): `docs/features/active/calendar-overlap-filter-19/evidence/regression-testing/`
- Final QA gates and coverage comparison: `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/`
- Policy-read evidence: `docs/features/active/calendar-overlap-filter-19/evidence/other/`

Note on spec AC-7 path: `spec.md` names `evidence/coverage/` for coverage artifacts. `coverage` is not a canonical evidence kind; this plan maps coverage evidence to the canonical kinds instead — baseline coverage under `evidence/baseline/`, post-change coverage and the comparison under `evidence/qa-gates/`. This mapping satisfies AC-7's evidence requirement at canonical locations. Raw coverage intermediates (cobertura XML, TestResults folders) may be staged under `artifacts/csharp/` (non-evidence intermediates only); the auditable numeric evidence lives in the canonical artifacts above.

Every command-step evidence artifact MUST include: `Timestamp:` (ISO-8601 `yyyy-MM-ddTHH-mm`), `Command:`, `EXIT_CODE:`, `Output Summary:`. Coverage-bearing test artifacts MUST include numeric line and branch coverage in `Output Summary:`. `<timestamp>` in filenames below is the ISO-8601 run timestamp.

## Toolchain Commands (C#)

- Format: `csharpier format .` (write) / `csharpier check .` (verify) — CSharpier is a global tool (1.3.0); this repo has no local tool manifest, so do NOT use `dotnet csharpier`.
- Lint + type check: `dotnet build OpenClaw.MailBridge.sln` (analyzers, nullable, warnings-as-errors via `Directory.Build.props`).
- Architecture + unit + integration tests with coverage: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (architecture-boundary tests run inside the solution test suite, e.g. `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs`).

### Phase 0 — Policy Reading and Baseline Capture

- [x] [P0-T1] Read policy documents in the `policy-compliance-order` sequence: `CLAUDE.md`-loaded rules, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`. Write `docs/features/active/calendar-overlap-filter-19/evidence/other/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read. Acceptance: artifact exists with all three fields.
- [x] [P0-T2] Record the branch name and HEAD commit hash (`git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`) in `docs/features/active/calendar-overlap-filter-19/evidence/baseline/baseline-git.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Acceptance: artifact exists and names the exact baseline commit used later by [P4-T6].
- [x] [P0-T3] Run `csharpier check .` from repo root and record `docs/features/active/calendar-overlap-filter-19/evidence/baseline/baseline-csharpier-check.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Acceptance: artifact exists; expected `EXIT_CODE: 0` on the clean baseline.
- [x] [P0-T4] Run `dotnet build OpenClaw.MailBridge.sln` and record `docs/features/active/calendar-overlap-filter-19/evidence/baseline/baseline-build.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (error/warning counts). Acceptance: artifact exists; expected `EXIT_CODE: 0`.
- [x] [P0-T5] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and record `docs/features/active/calendar-overlap-filter-19/evidence/baseline/baseline-test-coverage.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` containing pass/fail test counts plus numeric baseline line coverage percent and branch coverage percent (solution-wide, and the `OpenClaw.MailBridge` module values from the cobertura output). Raw cobertura/TestResults intermediates may be copied under `artifacts/csharp/baseline/`. Acceptance: artifact exists with numeric line and branch coverage values (no placeholders).

### Phase 1 — Regression Test (must fail before fix)

- [x] [P1-T1] Create `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs` (MSTest `[TestClass]` + FluentAssertions, fixed clock `FixedNow` injected via the `() => FixedNow` constructor seam, `FakeComActiveObject`/`FakeOutlookApplication`/`FakeOutlookFolder`/`FakeScanStateRepository` doubles — same pattern as `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs`) containing one test `ScanCalendarAsync_emits_interval_overlap_restrict_filter` that runs `ScanCalendarAsync` and asserts `calendar.Items.LastFilter` (captured by `FakeOutlookItems.Restrict` in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`) equals exactly `[Start] < '<endLocal>' AND [End] > '<startLocal>'`, where `<endLocal>` = `(FixedNow.AddDays(CalendarFutureDays)).LocalDateTime` and `<startLocal>` = `(FixedNow.AddDays(-CalendarPastDays)).LocalDateTime`, both formatted `MM/dd/yyyy hh:mm tt` with `CultureInfo.InvariantCulture` (preserving the #55-era formatting expectation). No live Outlook COM; no temp files. Acceptance: file exists, compiles, and contains this test; file <= 500 lines.
- [x] [P1-T2] In the same test file, add a small private static filter evaluator (parse `LastFilter` into its two clauses — field name `[Start]`/`[End]`, operator `<`/`>`/`>=`/`<=`, boundary parsed via `DateTime.ParseExact("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture)` — and evaluate event membership) plus a `[DataTestMethod]` `ScanCalendarAsync_filter_membership_matches_interval_overlap_semantics` with exactly five `[DataRow]` cases evaluated against the emitted filter: (a) event fully within the window — included; (b) in-progress event, `Start` before windowStart and `End` inside the window — included; (c) event spanning the entire window — included; (d) event with `End == windowStart` — excluded; (e) event with `Start == windowEnd` — excluded. Acceptance: all five boundary cases are asserted explicitly; file still <= 500 lines.
- [x] [P1-T3] [expect-fail] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerCalendarOverlapFilterTests"` against the unfixed predicate and record `docs/features/active/calendar-overlap-filter-19/evidence/regression-testing/regression-fail-before.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:` (non-zero expected), and an `Output Summary:` naming the failing tests (the exact-filter-string test and the in-progress/spanning data rows must fail; the exclusion rows and fully-within row may pass under the old predicate). Acceptance: artifact exists showing a non-zero exit code and named failing tests before the fix.

### Phase 2 — Minimal Fix

- [x] [P2-T1] Edit `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs` `BuildCalendarFilter` expression body (lines 48–49) to emit the interval-overlap predicate: `$"[Start] < '{endUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}' AND [End] > '{startUtc.LocalDateTime:MM/dd/yyyy hh:mm tt}'"`. Preserve the method signature, `private` visibility, `LocalDateTime` conversion, and the `MM/dd/yyyy hh:mm tt` format exactly; change nothing else in the file (`BuildInboxFilter` and all other members untouched). No changes to `src/OpenClaw.MailBridge/OutlookScanner.cs` (`Sort("[Start]")`, `IncludeRecurrences = true`, and the `Restrict` call remain unchanged). Acceptance: `git diff` for production code touches only the `BuildCalendarFilter` expression body in `OutlookScanner.Helpers.cs`.
- [x] [P2-T2] Re-run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerCalendarOverlapFilterTests"` and record `docs/features/active/calendar-overlap-filter-19/evidence/regression-testing/regression-pass-after.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` listing the passing test names. Acceptance: artifact exists; all regression tests pass.
- [x] [P2-T3] Run the full suite `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings` and record `docs/features/active/calendar-overlap-filter-19/evidence/regression-testing/full-suite-after-fix.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` with pass counts confirming all pre-existing tests (including `OutlookScannerCalendarUtcTests`) pass unchanged. Acceptance: artifact exists; zero failing tests; no existing test file modified.

### Phase 3 — Documentation and Status

- [x] [P3-T1] Update `docs/features/active/calendar-overlap-filter-19/spec.md`: in Acceptance Criteria item 3, record the regression test file path (`tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs`) and the test names added in [P1-T1]/[P1-T2]; bump `Last Updated`. Do not alter the substance of any acceptance criterion. Acceptance: spec.md AC-3 names the file and tests.
- [x] [P3-T2] Update `docs/features/active/calendar-overlap-filter-19/issue.md`: annotate the mirrored regression-test acceptance bullet with the same test file path and test names, keeping the mirror consistent with spec.md. Acceptance: issue.md mirror references the file and tests.

### Phase 4 — Final QA Loop and Coverage Gates (C#)

Loop rule: if any task in this phase fails or changes files, fix the cause and restart the phase from [P4-T1] until all tasks pass in one clean pass. Every command task below is unconditional; `EXIT_CODE: SKIPPED` is not a valid outcome.

- [x] [P4-T1] Run `csharpier format .` from repo root and record `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/final-csharpier-format.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (state whether any files were reformatted; if yes, restart the phase after this task completes). Acceptance: artifact exists.
- [x] [P4-T2] Run `csharpier check .` and record `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/final-csharpier-check.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:`. Acceptance: artifact exists with exit code 0.
- [x] [P4-T3] Run `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable, warnings-as-errors) and record `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/final-build.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (0 errors, 0 warnings). Acceptance: artifact exists with exit code 0.
- [x] [P4-T4] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (covers architecture-boundary, unit, and integration tests) and record `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/final-test-coverage.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and an `Output Summary:` containing pass/fail counts plus numeric post-change line and branch coverage percent (solution-wide and `OpenClaw.MailBridge` module). Raw cobertura/TestResults intermediates may be staged under `artifacts/csharp/post-fix/`. Acceptance: artifact exists with numeric coverage values and zero failing tests.
- [x] [P4-T5] Produce the coverage comparison artifact `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/coverage-comparison.<timestamp>.md` reporting: baseline line/branch coverage (from [P0-T5]), post-change line/branch coverage (from [P4-T4]), and changed-line coverage for `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs` (verify from the post-change cobertura data that the modified `BuildCalendarFilter` line(s) are covered by the new tests). Verify and state: line coverage >= 85%, branch coverage >= 75%, no coverage regression versus baseline, all changed lines covered. Acceptance: artifact exists with all numeric values (no placeholders); any threshold miss makes the plan outcome remediation-required, not PASS.
- [x] [P4-T6] Run `git diff --name-only <baseline-commit-from-P0-T2>` and record `docs/features/active/calendar-overlap-filter-19/evidence/qa-gates/scope-verification.<timestamp>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` confirming the only production change is `src/OpenClaw.MailBridge/OutlookScanner.Helpers.cs`, the only new/changed test file is `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarOverlapFilterTests.cs`, and remaining paths are feature docs/evidence. Acceptance: artifact exists and the diff list matches this scope exactly.

## Acceptance Criteria Mapping (spec.md is authoritative, full-bug mode)

| Spec AC | Criterion (abbreviated) | Plan tasks |
|---|---|---|
| AC-1 | Includes events starting within, starting-before-ending-inside, and spanning the window | P1-T1, P1-T2 (rows a–c), P2-T1, P2-T2 |
| AC-2 | Excludes `End == windowStart` and `Start == windowEnd` (strict boundary semantics) | P1-T2 (rows d–e), P2-T1, P2-T2 |
| AC-3 | Regression test fails before / passes after, no live COM, file+test names recorded | P1-T1, P1-T2, P1-T3 [expect-fail], P2-T2, P3-T1, P3-T2 |
| AC-4 | `MM/dd/yyyy hh:mm tt` / `LocalDateTime` formatting and #55 timezone handling unchanged; existing tests pass unchanged | P1-T1 (format assertion), P2-T1 (format preserved), P2-T3, P4-T4 |
| AC-5 | No behavior change outside `BuildCalendarFilter` | P2-T1, P2-T3, P4-T6 |
| AC-6 | Full C# toolchain passes in a single clean pass | P4-T1 through P4-T4 (single-pass loop rule) |
| AC-7 | Line >= 85%, branch >= 75%, changed lines covered, baseline/post/comparison evidence | P0-T5, P4-T4, P4-T5 (canonical-location mapping noted above) |

## Assumptions and Decisions

- `BuildCalendarFilter` stays `private`; the regression test asserts the filter string captured by `FakeOutlookItems.LastFilter` through `ScanCalendarAsync`, so no visibility change and no public-API change is needed (spec offered either option; this is the smaller diff).
- The Restrict-string-only change is sufficient; no post-filter pass over materialized occurrences is planned. If [P2-T2] or [P2-T3] reveals a recurring-occurrence edge case the Restrict string cannot express, stop and report remediation-required — do not extend scope silently.
- The test project uses MSTest + FluentAssertions (existing convention in `tests/OpenClaw.MailBridge.Tests/`), which takes precedence over the xUnit default named in `.claude/rules/csharp.md` for this existing project.
