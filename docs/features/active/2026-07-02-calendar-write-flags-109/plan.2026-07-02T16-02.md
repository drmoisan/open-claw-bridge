# calendar-write-flags — Plan

- **Issue:** #109
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T16-02
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata)

## Required References

- Policy reading order per `.claude/skills/policy-compliance-order/SKILL.md`:
  1. `CLAUDE.md` (auto-loaded)
  2. `.claude/rules/general-code-change.md`
  3. `.claude/rules/general-unit-test.md`
  4. `.claude/rules/csharp.md` (C# is the only language in scope)
- Requirements sources (authoritative): `docs/features/active/2026-07-02-calendar-write-flags-109/spec.md` (AC-1..AC-5), `docs/features/active/2026-07-02-calendar-write-flags-109/user-story.md` (AC-U1..AC-U3), `docs/features/active/2026-07-02-calendar-write-flags-109/issue.md`.

**All work must comply with these policies; do not duplicate their content here.**

## Conventions Used in This Plan

- `FEATURE` = `docs/features/active/2026-07-02-calendar-write-flags-109`
- `<ts>` = actual execution timestamp in `yyyy-MM-ddTHH-mm` form (per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`).
- Evidence artifacts live only under `FEATURE/evidence/<kind>/` (`baseline`, `regression-testing`, `qa-gates`, `other`). Writing evidence under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, or similar is a policy violation.
- Raw command intermediates (TRX files, Cobertura XML, tool console dumps) go under `artifacts/csharp/`; evidence markdown summaries in `FEATURE/evidence/<kind>/` reference them.
- Every command-step evidence artifact MUST contain: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (1–20 lines). Test-step artifacts MUST include numeric line and branch coverage in `Output Summary:`.
- C# toolchain commands (exact forms):
  - Format: `csharpier format .` (write) / `csharpier check .` (verify) — global tool 1.3.0; do not use `dotnet csharpier`.
  - Build (lint + type check: analyzers + nullable): `dotnet build OpenClaw.MailBridge.sln`
  - Test with coverage (includes architecture-boundary and contract tests in suite): `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
- No-SKIPPED rule: every command-bearing task below must execute its stated command; `EXIT_CODE: SKIPPED` is not a passing outcome.

## Scope Guard (applies to all phases)

- Production diff is confined to exactly three files: `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`, `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` (new), `src/OpenClaw.Core/appsettings.json`. Test diff is confined to two new files: `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs`, `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyPropertyTests.cs`. Plus feature-folder docs/evidence.
- Additive only: no signature, rename, or removal changes to any existing type or member. `SchedulingWorker.Pipeline.cs`, `SchedulingWorker.Audit.cs`, and the `ActingFlags` format must not be modified.
- No temp files anywhere, including tests (use `ConfigurationBuilder.AddInMemoryCollection` for binding tests). Every touched file stays <= 500 lines.
- Test stack: MSTest + FluentAssertions + CsCheck (all already referenced by `OpenClaw.Core.Tests`); no new dependencies.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read the policy files in required order — `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md` — and record the read in `FEATURE/evidence/baseline/phase0-instructions-read.md`.
  - Acceptance: Artifact exists at `docs/features/active/2026-07-02-calendar-write-flags-109/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of the four files read.
- [x] [P0-T2] Capture the formatting baseline by running `csharpier check .` from the repo root and recording the result in `FEATURE/evidence/baseline/baseline-format.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, and `Output Summary:` stating pass/fail and any file count reported.
- [x] [P0-T3] Capture the build baseline (analyzers + nullable type checking) by running `dotnet build OpenClaw.MailBridge.sln` and recording the result in `FEATURE/evidence/baseline/baseline-build.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including warning and error counts.
- [x] [P0-T4] Capture the test-and-coverage baseline by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, storing raw TRX/Cobertura output under `artifacts/csharp/`, and recording the result in `FEATURE/evidence/baseline/baseline-test-coverage.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing pass/fail test counts plus numeric baseline line-coverage percent and branch-coverage percent (no placeholders); artifact names the raw Cobertura file path under `artifacts/csharp/`.
- [x] [P0-T5] Record the pre-change state of the untouched surfaces by capturing the current `git rev-parse HEAD`, the `CalendarWriteEnabled` gate line in `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`, and the `ActingFlags` format lines in `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Audit.cs`, into `FEATURE/evidence/baseline/baseline-untouched-surfaces.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, the commit SHA, and verbatim quotes of the pipeline gate and acting-flags lines with their file paths, to support the AC-3/AC-U3 no-behavior-change verification in Phase 3.

### Phase 1 — Production Scaffolding (Flags, Helper, Config Sample)

- [x] [P1-T1] Add `EnableOrganizerReschedule` and `EnableAttendeeProposeNewTime` bool auto-properties (no initializer; default `false`) to the existing "Kill switches (master Section 7.5)" region of `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`, with XML docs that (a) name the master's canonical flag names `ENABLE_ORGANIZER_RESCHEDULE` / `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`, (b) name the environment realization `OpenClaw__AgentPolicy__EnableOrganizerReschedule` / `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`, and (c) describe the three-flag composition (`CalendarWriteEnabled` global kill switch AND per-path flag).
  - Acceptance: Both properties compile as plain bindable auto-properties defaulting to `false`; no existing member of the file is changed; XML docs contain both canonical names, both env forms, and the composition description; file remains <= 500 lines. (AC-1, AC-4, AC-U1)
- [x] [P1-T2] Create `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` containing a pure static class `CalendarWritePolicy` with exactly two public predicates — `static bool OrganizerRescheduleAllowed(AgentPolicyOptions options)` and `static bool AttendeeProposeNewTimeAllowed(AgentPolicyOptions options)` — each returning `options.CalendarWriteEnabled && <specific flag>`, throwing `ArgumentNullException` on `null` options (fail-fast), with XML docs referencing the truth-table composition; no I/O, no clock, no state.
  - Acceptance: New file exists, compiles, contains only the static class with the two predicates and null guard; no production code outside this file references `CalendarWritePolicy`; file <= 500 lines. (AC-2 implementation half)
- [x] [P1-T3] Add `"EnableOrganizerReschedule": false` and `"EnableAttendeeProposeNewTime": false` to the existing `OpenClaw:AgentPolicy` section of `src/OpenClaw.Core/appsettings.json`.
  - Acceptance: Both keys present with value `false` under the `AgentPolicy` object; no other key in the file is altered; JSON remains valid. (AC-4, AC-U1)

### Phase 2 — Tests (Defaults, Binding, Truth Table, Properties, Regression)

- [x] [P2-T1] Create `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs` (MSTest + FluentAssertions) containing: (a) a defaults test asserting a freshly constructed `AgentPolicyOptions` has both new flags `false`; (b) an exhaustive truth-table test (all 8 combinations of `CalendarWriteEnabled`, `EnableOrganizerReschedule`, `EnableAttendeeProposeNewTime`, e.g. via `[DataRow]`) asserting both helper results match the table in `spec.md` Behavior; and (c) null-argument tests asserting each helper throws `ArgumentNullException` for `null` options.
  - Acceptance: New file exists in `tests/` mirror location; all 8 combinations are individually asserted for both predicates; tests follow Arrange–Act–Assert with descriptive names; file <= 500 lines. (AC-2, AC-U2)
- [x] [P2-T2] Add configuration-binding tests to `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs` using `ConfigurationBuilder.AddInMemoryCollection` (no temp files): (a) binding an empty `OpenClaw:AgentPolicy` section leaves both new flags `false`; (b) `OpenClaw:AgentPolicy:EnableOrganizerReschedule=true` binds only that property; (c) `OpenClaw:AgentPolicy:EnableAttendeeProposeNewTime=true` binds only that property.
  - Acceptance: Three binding scenarios present and asserting property-level independence; no filesystem or environment mutation used. (AC-1, AC-U1)
- [x] [P2-T3] Create `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyPropertyTests.cs` with CsCheck property tests: (a) for arbitrary flag combinations, `CalendarWriteEnabled == false` implies both predicates return `false`; (b) `OrganizerRescheduleAllowed` is invariant under `EnableAttendeeProposeNewTime`; (c) `AttendeeProposeNewTimeAllowed` is invariant under `EnableOrganizerReschedule` — yielding at least one property test per helper (OpenClaw.Core is T1).
  - Acceptance: New file exists with at least one CsCheck property covering each of the two predicates; generators are seeded/reproducible per CsCheck defaults; file <= 500 lines. (AC-2, AC-U2)
- [x] [P2-T4] Run the scheduling-worker regression subset unchanged with `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerTests|FullyQualifiedName~SchedulingWorkerAuditTests"` and record the result in `FEATURE/evidence/regression-testing/regression-schedulingworker.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing all `SchedulingWorkerTests` and `SchedulingWorkerAuditTests` tests passing with zero test-file modifications (confirmed via `git status` on `tests/OpenClaw.Core.Tests/Agent/Runtime/`). (AC-3, AC-U3)
- [x] [P2-T5] Verify no production code invokes the new helpers and no untouched surface changed: run `git diff --name-only` (must list only the five in-scope code files plus feature docs/evidence) and search `src/` for `CalendarWritePolicy` references (must appear only in `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs`); record both checks in `FEATURE/evidence/other/scope-and-no-consumer-check.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, the exact commands, `EXIT_CODE:` per command, and `Output Summary:` confirming diff-scope confinement, zero production consumers of `CalendarWritePolicy`, and no diff to `SchedulingWorker.Pipeline.cs` / `SchedulingWorker.Audit.cs` (cross-referenced against `FEATURE/evidence/baseline/baseline-untouched-surfaces.<ts>.md`). (AC-3, AC-U3)

### Phase 3 — Final QA Loop and Coverage Verification

> Loop rule: if any task P3-T1..P3-T3 fails or changes files, restart from P3-T1 and repeat until all three complete cleanly in one pass. Each artifact records the final clean-pass run.

- [x] [P3-T1] Run the formatting stage — `csharpier format .` followed by `csharpier check .` — and record the final clean-pass result in `FEATURE/evidence/qa-gates/final-qa-format.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, both commands, `EXIT_CODE: 0` for `csharpier check .`, and `Output Summary:`; if `csharpier format .` changed any file, the loop restarted and the artifact reflects the final pass. (AC-5)
- [x] [P3-T2] Run the lint/type-check stage — `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable) — and record the final clean-pass result in `FEATURE/evidence/qa-gates/final-qa-build.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` including error/warning counts (0 errors). (AC-5)
- [x] [P3-T3] Run the full test stage in coverage mode — `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (this suite includes the architecture-boundary and contract tests) — storing raw TRX/Cobertura under `artifacts/csharp/`, and record the final clean-pass result in `FEATURE/evidence/qa-gates/final-qa-test-coverage.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` containing pass/fail test counts and numeric post-change line-coverage and branch-coverage percents (no placeholders); artifact names the raw Cobertura file path under `artifacts/csharp/`. (AC-5)
- [x] [P3-T4] Verify coverage thresholds and no-regression by comparing the Phase 0 baseline Cobertura against the Phase 3 post-change Cobertura: report baseline line/branch percent, post-change line/branch percent, and changed-line coverage for `AgentPolicyOptions.cs` and `CalendarWritePolicy.cs` (every changed/added production line covered); record the comparison in `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md`.
  - Acceptance: Artifact exists with `Timestamp:`, the comparison inputs (both Cobertura paths), and numeric values showing post-change line coverage >= 85%, branch coverage >= 75%, no reduction in coverage for changed lines, and 100%-covered changed production lines; if any threshold fails, outcome is remediation-required, not PASS. (AC-5)
- [x] [P3-T5] Update the acceptance-criteria checkboxes in `docs/features/active/2026-07-02-calendar-write-flags-109/spec.md` (AC-1..AC-5), `user-story.md` (AC-U1..AC-U3), and `issue.md`, checking each item only where a Phase 0–3 evidence artifact substantiates it, and cite the artifact path next to each checked item.
  - Acceptance: Every checked AC in the three documents names at least one existing evidence artifact under `FEATURE/evidence/`; no AC is checked without evidence.

## Test Plan

- Unit (new): `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs` — defaults (both flags `false`), in-memory configuration binding (empty section, each key independently), exhaustive 8-row truth table for both predicates, `ArgumentNullException` fail-fast.
- Property (new, T1): `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyPropertyTests.cs` — CsCheck: kill-switch-false forces both predicates false; each predicate invariant under the other path's flag.
- Regression (unchanged): `SchedulingWorkerTests` and `SchedulingWorkerAuditTests` run without modification (P2-T4).
- Integration / Manual: none — no I/O, wire, or host surface changes.
- Coverage evidence:
  - Baseline: `FEATURE/evidence/baseline/baseline-test-coverage.<ts>.md` (raw Cobertura in `artifacts/csharp/`)
  - Post-change: `FEATURE/evidence/qa-gates/final-qa-test-coverage.<ts>.md` (raw Cobertura in `artifacts/csharp/`)
  - Comparison: `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md`

## Open Questions / Notes

- Env-name mapping is a recorded decision in `spec.md` Inputs/Outputs: the master's `ENABLE_*` names are canonical semantic names realized through `OpenClaw__AgentPolicy__*`; no alias layer is added. No open questions remain.
- `docker-compose.yml` passthrough entries are explicitly out of scope (spec Inputs/Outputs; user-story Non-Goals).
