# Human-Interaction Record — Live-Tenant Verification Exception (F19, #130)

Timestamp: 2026-07-07T05-56

This artifact documents the `human_interaction` requirement that the orchestrator must
record in `artifacts/orchestration/orchestrator-state.json` for the F19 attendee
propose-new-time feature. The executor produces this evidence record only; the orchestrator
(not this plan's executor) writes the entry into the checkpoint.

## Requirement

- **Requirement (description):** Live-tenant verification of the attendee propose-new-time
  Graph write (`POST /users/{principal}/events/{id}/tentativelyAccept`) cannot be automated
  in this environment or CI. No Azure/Exchange credentials exist here or in CI, and issuing
  the write requires a tenant administrator's `Calendars.ReadWrite` admin consent plus a
  human operator with tenant access and two mailboxes to observe the proposal. The write
  path itself is proven by mocked-Graph contract tests
  (`GraphHostAdapterClientProposeNewTimeTests`, the established `FakeHttpHandler` pattern);
  only the unautomatable live-tenant confirmation is deferred to a human.
- **`response`:** `exception`
- **`runbook_path`:** `docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`

## Invariant satisfied

Per `.claude/rules/orchestrator-state.md`, a `human_interaction.requirements[]` entry whose
`response == "exception"` must carry a non-empty `runbook_path`. The `runbook_path` above is
non-empty and points to an existing runbook (verified in
`evidence/other/runbook-verification.2026-07-07T05-56.md`), satisfying the invariant.

## Suggested checkpoint block (for the orchestrator to record)

```json
{
  "human_interaction": {
    "requirements": [
      {
        "id": "HI-1",
        "description": "Live-tenant verification of the attendee propose-new-time Graph write cannot be automated (no Azure/Exchange credentials in this environment or CI; requires tenant-admin Calendars.ReadWrite consent and a human operator with two mailboxes).",
        "response": "exception",
        "runbook_path": "docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md"
      }
    ]
  }
}
```
