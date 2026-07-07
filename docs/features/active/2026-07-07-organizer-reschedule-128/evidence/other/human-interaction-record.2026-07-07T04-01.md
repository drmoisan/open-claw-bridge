# Human-Interaction Record — Live-Tenant Verification (Issue #128)

Timestamp: 2026-07-07T04-01

This record documents the single `human_interaction` requirement for feature F18 (organizer
reschedule, #128) for the orchestrator checkpoint. The executor produces this evidence
record; the orchestrator records the corresponding entry in
`artifacts/orchestration/orchestrator-state.json`.

## Requirement

- Requirement description: Live-tenant verification of the organizer-reschedule Graph
  write cannot be automated in this environment or CI. It requires (1) a tenant admin to
  grant the `Calendars.ReadWrite` application permission and provide admin consent, (2) a
  real Azure AD tenant and mailbox, and (3) a human to enable
  `OpenClaw__AgentPolicy__CalendarWriteEnabled` and
  `OpenClaw__AgentPolicy__EnableOrganizerReschedule` in a real deployment and observe a
  live organizer-owned event move (the calendar change, the `rescheduled` audit row, and
  the `series_moves` row), then disable the flags. No Azure/Exchange credentials exist in
  this development environment or CI. The mocked-Graph contract tests
  (`GraphHostAdapterClientRescheduleEventTests`) prove the wire contract; the live write is
  outside automated scope by design.
- response: "exception"
- runbook_path: docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md

## Orchestrator-state entry (for the orchestrator to record)

```json
{
  "human_interaction": {
    "requirements": [
      {
        "id": "HI-1",
        "description": "Live-tenant verification of the organizer-reschedule Graph write (Calendars.ReadWrite grant + admin consent, flag enablement, observed move) cannot be automated: no Azure/Exchange credentials in this environment or CI.",
        "response": "exception",
        "runbook_path": "docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md"
      }
    ]
  }
}
```

This satisfies the `.claude/rules/orchestrator-state.md` invariant that an `exception`
requirement carries a non-empty `runbook_path`. The referenced runbook is an
orchestrator-owned deliverable (authoring delegated to the human-exception-runbook
specialist); this executor does not author it. AC-9 is fully satisfied once the runbook
file exists at the path above.
