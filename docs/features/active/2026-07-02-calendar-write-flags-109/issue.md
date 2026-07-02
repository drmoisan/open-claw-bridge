# calendar-write-flags (Issue #109)

- Date captured: 2026-07-02
- Author: drmoisan
- Status: Promoted -> docs/features/active/calendar-write-flags/ (Issue #109)

- Issue: #109
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/109
- Last Updated: 2026-07-02
- Work Mode: full-feature

## Problem / Why

The master specification names two calendar-write feature flags — `ENABLE_ORGANIZER_RESCHEDULE` and `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` (`docs/open-claw-approach.master.md` §5.4, §11, §13 steps 10-11) — that gate the Stage 2 write paths independently. The current `AgentPolicyOptions` has only a single coarse `CalendarWriteEnabled` boolean. Starting Stage 2 work (F18 organizer reschedule, F19 attendee propose-new-time) against the coarse flag would force a config rename mid-track; the research roadmap (gap F10, `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`) schedules the naming scaffolding now so Stage 2 starts from the master's exact flag names with no churn.

## Proposed Behavior

- Add `EnableOrganizerReschedule` and `EnableAttendeeProposeNewTime` booleans (both default `false`) to `AgentPolicyOptions`, bindable from configuration/environment using the master's exact env-var names (`ENABLE_ORGANIZER_RESCHEDULE`, `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`) via the existing options binding conventions.
- Composition rule with the existing coarse flag: `CalendarWriteEnabled` remains the global calendar-write kill switch (master §7.5); an individual write path is permitted only when `CalendarWriteEnabled AND` its specific flag are both true. Effective-permission helpers (e.g. `OrganizerRescheduleAllowed`, `AttendeeProposeNewTimeAllowed`) encode this composition in one place.
- Existing behavior unchanged: all calendar writes remain disabled (no write path exists yet); the pipeline's current `CalendarWriteEnabled` gating keeps functioning identically.
- Document the three-flag model (global kill switch + two path flags) in the options XML docs and configuration sample (`appsettings.json` if flags are represented there).

## Acceptance Criteria

- [x] `AgentPolicyOptions` exposes `EnableOrganizerReschedule` and `EnableAttendeeProposeNewTime`, both defaulting to `false`, bound through the existing `OpenClaw:AgentPolicy` configuration section (environment form `OpenClaw__AgentPolicy__EnableOrganizerReschedule` / `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`); the XML docs name the master's canonical flag names `ENABLE_ORGANIZER_RESCHEDULE` and `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`. — Evidence: `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md`, `evidence/qa-gates/final-qa-build.2026-07-02T16-27.md`.
- [x] Effective-permission composition (`CalendarWriteEnabled AND` specific flag) is implemented in exactly one place — a pure static helper (`CalendarWritePolicy.OrganizerRescheduleAllowed` / `CalendarWritePolicy.AttendeeProposeNewTimeAllowed`) — and unit-tested for all 8 combinations of the three booleans, with at least one CsCheck property test per helper (OpenClaw.Core is T1). — Evidence: `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md`, `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md`.
- [x] No behavior change: `SchedulingWorker` pipeline gating, the audit `ActingFlags` format, and all existing tests pass unchanged; no calendar-write path is introduced and no production code invokes the new helpers. — Evidence: `evidence/regression-testing/regression-schedulingworker.2026-07-02T16-24.md`, `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md`, `evidence/baseline/baseline-untouched-surfaces.2026-07-02T16-17.md`.
- [x] Configuration sample and docs show the three-flag model: `src/OpenClaw.Core/appsettings.json` gains both keys set to `false` under `OpenClaw:AgentPolicy`, and the options XML docs describe the global-kill-switch-plus-path-flag composition. — Evidence: `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md`, `evidence/qa-gates/final-qa-build.2026-07-02T16-27.md`.
- [x] Full C# toolchain passes (CSharpier, analyzers, nullable, architecture tests, MSTest suite); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered. — Evidence: `evidence/qa-gates/final-qa-format.2026-07-02T16-27.md`, `evidence/qa-gates/final-qa-build.2026-07-02T16-27.md`, `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T16-29.md`.

## Constraints & Risks

- Scaffolding-only: deliberately small. No HostAdapter/MailBridge/wire changes; no Runtime consumer beyond the composition helpers.
- MSTest + FluentAssertions + CsCheck as applicable; no temp files; 500-line cap.

## Test Conditions to Consider

- [ ] Unit: defaults false; binding from configuration keys; composition truth table (8 combinations).
- [ ] Regression: existing `SchedulingWorker` gating tests unchanged.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [ ] Create active feature folder from the template
