# calendar-write-flags — Spec

- **Issue:** #109
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T16-02
- **Status:** Draft
- **Version:** 0.2

## Overview

The master specification names two calendar-write feature flags — `ENABLE_ORGANIZER_RESCHEDULE` and `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` (`docs/open-claw-approach.master.md` §5.4, §11, §13 steps 10-12) — that gate the Stage 2 write paths independently. The current `AgentPolicyOptions` has only a single coarse `CalendarWriteEnabled` boolean. Starting Stage 2 work (F18 organizer reschedule, F19 attendee propose-new-time) against the coarse flag would force a config rename mid-track; the research roadmap (gap F10, `docs/research/2026-07-01-open-claw-vision-gap-analysis.md` Epic B item 10) schedules the naming scaffolding now so Stage 2 starts from the master's exact flag names with no churn.

This is a scaffolding-only feature: two new default-off booleans on the existing options POCO, one pure composition helper, configuration-sample and XML-doc updates, and tests. No write path exists yet and none is introduced; no production code consumes the helpers until F18/F19.

## Behavior

- **New flags.** Add `EnableOrganizerReschedule` and `EnableAttendeeProposeNewTime` booleans to `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`, in the existing "Kill switches (master Section 7.5)" region, as plain auto-properties with no initializer (defaulting to `false`, matching `SendEnabled`/`CalendarWriteEnabled`). XML docs follow the file's existing conventions and name the master's canonical flag names (`ENABLE_ORGANIZER_RESCHEDULE`, `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`) so the semantic mapping is discoverable at the definition site.
- **Composition rule.** `CalendarWriteEnabled` remains the global calendar-write kill switch (master §7.5). An individual write path is permitted only when `CalendarWriteEnabled AND` its specific flag are both true:

  | `CalendarWriteEnabled` | `EnableOrganizerReschedule` | `EnableAttendeeProposeNewTime` | Organizer reschedule allowed | Attendee propose-new-time allowed |
  |---|---|---|---|---|
  | false | false | false | false | false |
  | false | false | true  | false | false |
  | false | true  | false | false | false |
  | false | true  | true  | false | false |
  | true  | false | false | false | false |
  | true  | false | true  | false | true  |
  | true  | true  | false | true  | false |
  | true  | true  | true  | true  | true  |

  The two paths are gated independently of each other (master §13 step 11: organizer-owned reschedule and attendee-side proposal must not collapse into one code path), and the repo default state (all three flags false → both paths denied) is the first row.
- **Composition helper.** A new pure static class `CalendarWritePolicy` in `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` exposes `OrganizerRescheduleAllowed(AgentPolicyOptions)` and `AttendeeProposeNewTimeAllowed(AgentPolicyOptions)`, encoding the truth table in exactly one place. No production code invokes these helpers in this feature; F18/F19 become their first consumers.
- **No behavior change.** The pipeline's existing gate (`SchedulingWorker.Pipeline.cs:288`, `if (!options.CalendarWriteEnabled)`) is untouched, as is the audit acting-flags format `SendEnabled=<bool>;CalendarWriteEnabled=<bool>` (`SchedulingWorker.Audit.cs:19-20`, part of the issue #107 audit contract). All calendar writes remain disabled: no write RPC exists in Stage 0.
- **Documentation.** The three-flag model (global kill switch + two path flags) is documented in the options XML docs and in the configuration sample `src/OpenClaw.Core/appsettings.json`.

## Inputs / Outputs

- Inputs (config keys; no new CLI flags):
  - `OpenClaw:AgentPolicy:EnableOrganizerReschedule` (bool, default `false`)
  - `OpenClaw:AgentPolicy:EnableAttendeeProposeNewTime` (bool, default `false`)
- How an operator sets the flags (verified against `Program.cs:59-61`): `AgentPolicyOptions` binds from the `OpenClaw:AgentPolicy` section via standard .NET options binding, and `WebApplication.CreateBuilder` includes the default environment-variable configuration provider. The flags are therefore set either in `appsettings.json` or via the double-underscore environment form:
  - `OpenClaw__AgentPolicy__EnableOrganizerReschedule=true`
  - `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime=true`
- Env-name mapping decision (recorded): the master's `ENABLE_ORGANIZER_RESCHEDULE` / `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` are the **canonical semantic names**; this repository realizes them through its existing section-based configuration conventions as the keys above. No custom env-alias mapping infrastructure exists in the repo (verified: `Program.cs` uses only `GetSection(...).Bind(...)`; `docker-compose.yml` forwards explicit `OpenClaw__*` variables), and none is added. Container note: `docker-compose.yml` forwards an explicit `environment:` list and currently forwards no `AgentPolicy` keys (the existing kill switches are likewise not forwarded); an operator enabling a flag in the container adds the `OpenClaw__AgentPolicy__...` entry to the compose `environment:` block or edits `appsettings.json`. Adding compose passthrough entries is out of scope for this feature.
- Outputs: none. No new logs, telemetry, endpoints, or artifacts. The existing audit `ActingFlags` string is unchanged.
- Versioning / backward compatibility: additive options properties; existing configurations bind unchanged (missing keys yield the `false` defaults).

## API / CLI Surface

- No CLI or HTTP surface changes.
- New public API (host-neutral, `OpenClaw.Core.Agent`):
  - `AgentPolicyOptions.EnableOrganizerReschedule : bool` (default `false`)
  - `AgentPolicyOptions.EnableAttendeeProposeNewTime : bool` (default `false`)
  - `static bool CalendarWritePolicy.OrganizerRescheduleAllowed(AgentPolicyOptions options)`
  - `static bool CalendarWritePolicy.AttendeeProposeNewTimeAllowed(AgentPolicyOptions options)`
- Contracts and validation rules: helpers are pure (no I/O, no clock, no state) and total over all inputs; a `null` options argument fails fast per the repo's fail-fast policy. No `PostConfigure` normalization is needed — `false` is both the binder default and the safe default.

## Data & State

- No data flow, storage, or state changes. No migration or backfill. The flags are process-lifetime configuration read through `IOptions<AgentPolicyOptions>`.
- Invariant: default configuration (both new keys absent or `false`) yields both helpers returning `false` regardless of `CalendarWriteEnabled` — no write path can become permitted by this change alone.

## Constraints & Risks

- Scaffolding-only: deliberately small. No HostAdapter/MailBridge/wire changes; no Runtime consumer beyond the composition helpers (which have no consumer until F18/F19).
- MSTest + FluentAssertions (+ Moq where doubles are needed; none expected here) per the existing `tests/OpenClaw.Core.Tests/` conventions; CsCheck for property tests (OpenClaw.Core is T1 in `quality-tiers.yml`). No temp files; 500-line cap per file.
- Risk (low): dead-code analyzers may flag the unconsumed helpers. Mitigation: the helpers are public API with tests as consumers; if an analyzer diagnostic fires, address it per policy rather than suppressing broadly.
- Risk (low): future drift between the master's env-style names and the repo's property names. Mitigation: XML docs at the definition site record the canonical-name mapping (this spec is the second record).

## Implementation Strategy

- Implementation scope (what changes, not sequencing):
  - `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` — two new bool auto-properties with XML docs describing the three-flag model and canonical names.
  - `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` (new) — static pure helper class with the two composition predicates.
  - `src/OpenClaw.Core/appsettings.json` — add `"EnableOrganizerReschedule": false` and `"EnableAttendeeProposeNewTime": false` to the `OpenClaw:AgentPolicy` section.
  - `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs` (new) — truth-table unit tests (8 combinations) plus defaults-and-binding coverage.
  - `tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyPropertyTests.cs` (new) — CsCheck property tests (e.g., `!CalendarWriteEnabled` implies both helpers `false`; each helper is independent of the other path's flag).
- Design decision — where the composition lives (recorded with evidence): `AgentPolicyOptions` stays a plain bindable POCO. Evidence: the options class currently contains only auto-properties, and every derived behavior in the codebase is projected out of it by a separate class (`TriagePolicy.FromOptions`, `WorkingHoursPolicy.FromOptions`, `OwnerSchedulingPolicy.FromOptions`). Computed properties on the POCO would bind (the binder ignores get-only members) but would break the repo's options-bag/pure-logic separation. A full `FromOptions` projection type is excess machinery for two predicates, so the minimal convention-consistent shape is a static pure helper class.
- Dependency changes: none. CsCheck and FluentAssertions are already referenced by `OpenClaw.Core.Tests`.
- Logging/telemetry additions: none.
- Rollout plan: both new flags default `false`; behavior is identical before and after this change. Stage 2 rollout order per master §8 Phase 5 / §13 steps 10-11: enable organizer reschedule (F18) first, attendee propose-new-time (F19) second, each behind its own flag, both under the `CalendarWriteEnabled` global kill switch. Kill-switch semantics: clearing `CalendarWriteEnabled` disables both paths regardless of the path flags.

## Acceptance Criteria

- [x] AC-1: `AgentPolicyOptions` exposes `EnableOrganizerReschedule` and `EnableAttendeeProposeNewTime`, both defaulting to `false`, bound through the existing `OpenClaw:AgentPolicy` configuration section (environment form `OpenClaw__AgentPolicy__EnableOrganizerReschedule` / `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime`); the XML docs name the master's canonical flag names `ENABLE_ORGANIZER_RESCHEDULE` and `ENABLE_ATTENDEE_PROPOSE_NEW_TIME`. — Evidence: `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md` (defaults + 3 binding tests pass), `evidence/qa-gates/final-qa-build.2026-07-02T16-27.md` (XML docs compile clean).
- [x] AC-2: Effective-permission composition (`CalendarWriteEnabled AND` specific flag) is implemented in exactly one place — `CalendarWritePolicy.OrganizerRescheduleAllowed` / `CalendarWritePolicy.AttendeeProposeNewTimeAllowed` — and unit-tested for all 8 combinations of the three booleans, with at least one CsCheck property test per helper (OpenClaw.Core is T1). — Evidence: `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md` (8-row truth table + 3 CsCheck properties pass), `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md` (single implementation site).
- [x] AC-3: No behavior change: `SchedulingWorker` pipeline gating (`Pipeline.cs` `CalendarWriteEnabled` check) and the audit `ActingFlags` format are unmodified; all existing tests pass unchanged; no calendar-write path is introduced and no production code invokes the new helpers. — Evidence: `evidence/regression-testing/regression-schedulingworker.2026-07-02T16-24.md` (16/16 pass, zero test-file modifications), `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md` (empty diff on Runtime/, zero consumers), `evidence/baseline/baseline-untouched-surfaces.2026-07-02T16-17.md`.
- [x] AC-4: Configuration sample and docs show the three-flag model: `src/OpenClaw.Core/appsettings.json` gains both keys set to `false` under `OpenClaw:AgentPolicy`, and the options XML docs describe the global-kill-switch-plus-path-flag composition. — Evidence: `evidence/other/scope-and-no-consumer-check.2026-07-02T16-25.md` (appsettings.json in diff scope; JSON validated during execution), `evidence/qa-gates/final-qa-build.2026-07-02T16-27.md` (XML docs compile clean).
- [x] AC-5: Full C# toolchain passes (CSharpier, analyzers, nullable, architecture tests, MSTest suite); coverage thresholds hold (line >= 85%, branch >= 75%) with changed lines covered. — Evidence: `evidence/qa-gates/final-qa-format.2026-07-02T16-27.md`, `evidence/qa-gates/final-qa-build.2026-07-02T16-27.md`, `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T16-29.md`.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)

- [ ] Unit: defaults false — a freshly constructed `AgentPolicyOptions` has both new flags `false`; binding an empty `OpenClaw:AgentPolicy` section (in-memory configuration, no temp files) leaves both `false`.
- [ ] Unit: binding from configuration keys — `ConfigurationBuilder.AddInMemoryCollection` with `OpenClaw:AgentPolicy:EnableOrganizerReschedule=true` (and the attendee key) binds each property independently.
- [ ] Unit: composition truth table — all 8 combinations of (`CalendarWriteEnabled`, `EnableOrganizerReschedule`, `EnableAttendeeProposeNewTime`) produce the expected pair of helper results per the table in Behavior.
- [ ] Property (CsCheck): for arbitrary flag combinations, `CalendarWriteEnabled == false` implies both helpers return `false`; `OrganizerRescheduleAllowed` is invariant under `EnableAttendeeProposeNewTime` and vice versa.
- [ ] Regression: existing `SchedulingWorker` gating tests (`SchedulingWorkerTests`, `SchedulingWorkerAuditTests` acting-flags assertions) pass unchanged.
