# 2026-04-05-refactor-and-test - Refactor Plan

- **Issue:** #9
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-06T14-25
- **Status:** In Review
- **Version:** 0.1

## Required References (read, do not restate)

- Coding workflow and standards: [`docs/code-change.instructions.md`](../../code-change.instructions.md)
- Unit test policy: [`docs/unit-test-policy.md`](../../unit-test-policy.md)

## Strategy

Refactor the runtime host out of `Program.cs`, add targeted runtime tests, and keep the checklist state aligned with evidence on disk.

## Work Breakdown

### Phase 0 — Intake, Policy, and Baseline
- [x] [P0-T1] Read applicable repository policy instructions and collect baseline repo status evidence.
- [x] [P0-T2] Confirm request classification and change-budget route selection.
- [x] [P0-T3] Initialize orchestration checkpoint and active feature artifacts.

### Phase 1 — Refactor Runtime Classes
- [x] [P1-T1] Split runtime classes from `src/OpenClaw.MailBridge/Program.cs` into dedicated production files.
- [x] [P1-T2] Keep runtime wiring behavior and contracts unchanged after refactor.

### Phase 2 — Unit Test Expansion
- [x] [P2-T1] Add project references/internals access needed for runtime class tests.
- [x] [P2-T2] Add unit tests that exercise each refactored runtime file/class.

### Phase 3 — QA and Review
- [x] [P3-T1] Run formatting/build/tests and record results.
- [x] [P3-T2] Generate coverage report and verify per-file coverage target.
- [ ] [P3-T3] Update acceptance criteria checklist and orchestration checkpoint with completion summary.

## Test Plan

- Unit/Integration: impacted runtime classes and regression tests for runtime invariants
- CLI/Workflow: host startup path remains delegated through `BridgeApplication`
- Tooling: formatting, build, and test evidence captured in feature artifacts

## Rollback / Contingency

If the runtime split regresses behavior, restore the previous runtime wiring from branch history and replay the refactor in smaller verified steps.

## Open Questions / Notes

- `P3-T2` is now evidence-backed by `evidence/qa-gates/coverage-summary.2026-04-06T21-37.md` and `evidence/qa-gates/coverage-thresholds.2026-04-06T21-37.md`, which record `BridgeApplication.cs` at `90.5%` and `ComActiveObject.cs` at `100.0%`.
- `P3-T3` remains unchecked because remediation produced status reconciliation artifacts, but did not create a matching orchestration-checkpoint completion artifact or a fully completed acceptance-source state.
