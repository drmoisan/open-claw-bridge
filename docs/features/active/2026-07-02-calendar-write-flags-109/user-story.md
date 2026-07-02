# `calendar-write-flags` — User Story

- Issue: #109
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-02T16-02

## Story Statement

- As the pilot operator of the OpenClaw agent, I want an independent, default-off rollout lever for each of the two Stage 2 calendar-write paths (organizer reschedule, attendee propose-new-time), so that I can enable them one at a time in the master's prescribed order without one path's rollout state affecting the other.
- As the pilot operator, I want the existing `CalendarWriteEnabled` global kill switch to remain authoritative over both path flags, so that clearing one switch during an incident disables every calendar write regardless of how the per-path flags are set.
- As the Stage 2 implementer (features F18/F19), I want the flags and their composition helper to exist now under the master's canonical names, so that Stage 2 work starts against stable configuration keys instead of renaming `CalendarWriteEnabled` mid-track.

## Problem / Why

The master specification names two calendar-write feature flags — `ENABLE_ORGANIZER_RESCHEDULE` and `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` (`docs/open-claw-approach.master.md` §5.4, §11, §13 steps 10-12) — that gate the Stage 2 write paths independently. The current `AgentPolicyOptions` has only a single coarse `CalendarWriteEnabled` boolean. Starting Stage 2 work (F18 organizer reschedule, F19 attendee propose-new-time) against the coarse flag would force a config rename mid-track; the research roadmap (gap F10, `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`) schedules the naming scaffolding now so Stage 2 starts from the master's exact flag names with no churn.

## Personas & Scenarios

- Persona: Pilot operator (currently the repository owner running the Stage 0 deployment)
  - Who: operates the agent container, owns `appsettings.json` / compose environment values, and flips kill switches during pilot operation.
  - Cares about: blast radius. Mistaken reschedules are higher-stakes than mistaken replies (master §8 Phase 5 risk note), so every write capability must default off and be individually controllable.
  - Constraints: configuration is the only control surface — flags are set in `appsettings.json` or as `OpenClaw__AgentPolicy__...` environment variables; there is no admin UI.
  - Goals and frustrations: wants the rollout order the master prescribes (organizer reschedule first, attendee propose-new-time second) to be expressible as two independent levers; does not want to discover at Stage 2 rollout time that both paths share one coarse switch.
  - Context and motivations: the operator already runs `SendEnabled`/`CalendarWriteEnabled` default-off and expects the same safe-by-default pattern for any new capability flag.

- Persona: Stage 2 implementer (agent or developer delivering F18/F19)
  - Who: implements the organizer-reschedule and attendee-propose-new-time write paths in later features.
  - Cares about: starting from stable, canonically named configuration and a single already-tested permission predicate, rather than inventing gating logic inside each write path.
  - Constraints: master §13 step 11 forbids collapsing the two write flows into one code path; each flow must consult its own gate.
  - Goals: call `CalendarWritePolicy.OrganizerRescheduleAllowed(...)` / `AttendeeProposeNewTimeAllowed(...)` and inherit correct kill-switch composition with no new decision logic.

- Scenario 1: Staged enablement in the master's rollout order
  - Who acts: the pilot operator, after F18 (organizer reschedule) ships and is validated.
  - Trigger: the decision to enable the first write path for the pilot mailbox.
  - Steps: the operator sets `CalendarWriteEnabled: true` and `EnableOrganizerReschedule: true` (in `appsettings.json`, or `OpenClaw__AgentPolicy__CalendarWriteEnabled=true` / `OpenClaw__AgentPolicy__EnableOrganizerReschedule=true` in the container environment), leaving `EnableAttendeeProposeNewTime` at its default `false`.
  - Obstacles/decisions: none — the attendee path stays denied without any additional action because its flag was never enabled.
  - Expected outcome: organizer reschedule is permitted; attendee propose-new-time remains denied. Weeks later, enabling the attendee path is a single additional flag with no change to the organizer path.

- Scenario 2: Incident kill switch
  - Who acts: the pilot operator during an incident involving unwanted calendar writes.
  - Trigger: a defective or unexpected write is observed while both path flags are enabled.
  - Steps: the operator sets `CalendarWriteEnabled: false` and restarts/redeploys; the per-path flags are left as-is.
  - Obstacles/decisions: the operator must not need to remember every per-path flag under pressure — one switch must be sufficient.
  - Expected outcome: both write paths are denied (`CalendarWriteEnabled AND flag` composition), and the per-path flag state is preserved for a clean re-enable after the incident.

- Scenario 3: Today (this feature only) — nothing changes
  - Who acts: the pilot operator upgrading to a build containing this scaffolding.
  - Trigger: routine deployment.
  - Steps: no configuration edits. The two new keys are absent (or present as `false` from the updated sample).
  - Expected outcome: identical behavior to the previous build — no write path exists, the pipeline's `CalendarWriteEnabled` gating and audit acting-flags output are unchanged, and both new flags read back `false`.

## Acceptance Criteria

- [x] AC-U1: The operator can set each path flag independently through the existing configuration conventions — `OpenClaw:AgentPolicy:EnableOrganizerReschedule` and `OpenClaw:AgentPolicy:EnableAttendeeProposeNewTime` in `appsettings.json`, or the `OpenClaw__AgentPolicy__...` environment forms — and both flags default to `false` when unset. — Evidence: `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md` (defaults test + 3 independent-binding tests pass).
- [x] AC-U2: With `CalendarWriteEnabled` false, both effective permissions are false regardless of the per-path flags; with `CalendarWriteEnabled` true, each path's effective permission equals its own flag and is unaffected by the other path's flag (verified by the truth-table and property tests in the spec). — Evidence: `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md` (8-row truth table + kill-switch-dominance and path-independence CsCheck properties pass).
- [x] AC-U3: Upgrading with no configuration changes produces no behavior change: existing pipeline gating, audit acting-flags output, and all existing tests are unchanged, and no calendar write can occur (no write path exists and none is added). — Evidence: `evidence/regression-testing/regression-schedulingworker.2026-07-02T16-24.md`, `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md`, `evidence/baseline/baseline-untouched-surfaces.2026-07-02T16-17.md`.

## Non-Goals

- No calendar-write RPCs, HostAdapter/MailBridge surface, or pipeline consumer changes — F18/F19 are the first consumers of the new helpers.
- No per-mailbox enablement flags (master §7.5 third bullet); Stage 0 is single-mailbox, and per-mailbox flags are deferred per the gap analysis.
- No change to the audit `ActingFlags` string format (`SendEnabled=<bool>;CalendarWriteEnabled=<bool>`, issue #107 contract); extending it with the new flags is F18/F19 scope when the flags first act.
- No custom environment-variable alias layer: the master's `ENABLE_*` names are realized through the repo's standard section-based configuration keys, not a bespoke mapping.
- No `docker-compose.yml` environment passthrough additions; documenting the mechanism is in scope, editing compose files is not.
