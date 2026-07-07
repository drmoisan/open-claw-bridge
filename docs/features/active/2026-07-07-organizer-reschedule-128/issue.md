# organizer-reschedule (Issue #128)

- Date captured: 2026-07-07
- Author: drmoisan
- Status: Promoted -> docs/features/active/organizer-reschedule/ (Issue #128)
- Epic: openclaw-vision (Epic D — Stage 2 Final Vision), feature F18, wave 4
- Depends on: #109 calendar-write-flags, send-on-behalf-allowlist (#119), azure-bicep-iac (#125), negative-scope-smoke-test (#120)

- Issue: #128
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/128
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Problem / Why

The OpenClaw deterministic agent computes calendar reschedule decisions but has never
performed a real calendar write. Feature F12 (`#109`, calendar-write-flags) shipped the
policy scaffolding — the global `CalendarWriteEnabled` kill switch, the per-path
`EnableOrganizerReschedule` flag, and the pure `CalendarWritePolicy.OrganizerRescheduleAllowed`
composition predicate — but no code consumes that gate to issue a Microsoft Graph write.
F18 delivers the first real calendar-write RPC: an organizer reschedule realized as a
Graph `PATCH /events/{id}` behind the `ENABLE_ORGANIZER_RESCHEDULE` named feature flag.

## Proposed Behavior

- Add an organizer-reschedule calendar-write operation that, when gated on by
  `CalendarWritePolicy.OrganizerRescheduleAllowed(options)`, issues a Graph
  `PATCH /events/{id}` to move an organizer-owned event to a new start/end time.
- The flag defaults OFF. When `EnableOrganizerReschedule` (or the global
  `CalendarWriteEnabled` kill switch) is false, there is no behavior change: the
  deterministic pipeline still computes and logs the intended reschedule but performs
  no Graph write (dry-run parity with today's behavior).
- Compose with the existing `OneOnOneMoveGuard` / `ISeriesMoveHistory` seam (built by F8
  `#105` explicitly for F18) so a reschedule respects one-on-one move-history limits.
- Emit the reschedule as an auditable action (reuse the existing action-audit record path).
- No live tenant is available; the Graph write path ships as mocked-Graph contract tests
  behind the flag. Live verification is recorded as a human-interaction exception runbook
  (F11 HI-1 precedent).

## Acceptance Criteria (early draft)

- [ ] A calendar-write operation issues Graph `PATCH /events/{id}` only when
      `CalendarWritePolicy.OrganizerRescheduleAllowed(options)` returns true.
- [ ] With the flag OFF (default), no Graph write is attempted and behavior is unchanged
      (compute + log only).
- [ ] The reschedule path composes with `OneOnOneMoveGuard` / `ISeriesMoveHistory`.
- [ ] The reschedule is recorded as an auditable action.
- [ ] Graph `PATCH /events` behavior is proven by mocked-Graph contract tests behind the
      flag; coverage thresholds are met (line >= 85%, branch >= 75%).
- [ ] Live-tenant verification is documented as a human-interaction exception runbook.

## Constraints & Risks

- First real calendar write; must fail closed. Any ambiguity in the gate resolves to
  "no write".
- No Azure/Exchange credentials in this environment or CI; the Graph write is exercised
  only through a mocked Graph seam.
- Architecture boundaries (No-COM, layer boundaries) must hold; the write path lives in
  host-neutral domain/application code with the Graph call behind an adapter seam.
- Cross-module contract change (floor signal): the write path threads options, the move
  guard, the audit record, and the Graph adapter contract together.

## Test Conditions to Consider

- [ ] Unit: gate truth table (both flags on/off combinations) yields write / no-write.
- [ ] Unit: move-guard interaction blocks a reschedule that violates move history.
- [ ] Contract: mocked Graph `PATCH /events/{id}` request shape and response handling.
- [ ] Audit: a performed reschedule emits the expected action-audit record.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/organizer-reschedule/` folder from the template
