# Organizer Reschedule Live-Tenant Verification — Human-Exception Runbook

- **Feature:** organizer-reschedule (Issue #128)
- **Runbook path:** `docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md`
- **Authored:** 2026-07-07
- **Source checklist:** `docs/features/active/2026-07-07-organizer-reschedule-128/spec.md` (Behavior, API/Contract Surface, Constraints & Risks sections); F11 (`exchange-rbac-scripts`, issue #111) `runbooks/exchange-rbac-setup.runbook.md` HI-1 record-shape precedent

## Cue

Act on this runbook when the orchestrator records live-tenant execution of the F18
organizer-reschedule verification as a permitted human exception:
`artifacts/orchestration/orchestrator-state.json` contains a `human_interaction.requirements[]`
entry (HI-1) with `response: "exception"` and `runbook_path` pointing to this file. The trigger
event is a request to enable `ENABLE_ORGANIZER_RESCHEDULE` in a real Microsoft 365 tenant, or a
request to verify the first live organizer-owned event move before that enablement is relied upon
in production. Live execution against a real tenant cannot and must not run in CI: no
Azure/Exchange credentials exist in this environment or CI, and issuing the write requires a human
tenant administrator's `Calendars.ReadWrite` admin consent plus a human operator with tenant
access to observe the result. The write path itself is fully proven by mocked-Graph contract tests
(`GraphHostAdapterClientRescheduleEventTests`, the established `FakeHttpHandler` pattern); this
runbook covers only the unautomatable live-tenant confirmation.

## Prerequisites

All of the following must be true before starting:

1. **Tenant and app registration.** A Microsoft 365 / Microsoft Entra tenant exists, and an
   Enterprise Application (service principal) is registered for the OpenClaw agent, either newly
   created or the same registration used for F11's Exchange RBAC scoping.
2. **`Calendars.ReadWrite` permission granted with admin consent.** The registered application
   holds either the Microsoft Graph application permission `Calendars.ReadWrite`, or the scoped
   Exchange Online Application RBAC role `Application Calendars.ReadWrite` (see F11's
   `-IncludeCalendarWrite` extension to `Grant-OpenClawRbacRoles`), and a tenant administrator
   (Privileged Role Administrator, Cloud Application Administrator, or Application Administrator)
   has granted tenant-wide admin consent. `Calendars.ReadWrite` requires admin consent; it is not
   grantable by an end user.
3. **OpenClaw agent deployed.** The Stage 1 infrastructure-as-code deployment (F16, issue #125) is
   complete, and the deployed agent can reach Microsoft Graph and authenticate as the registered
   application.
4. **Environment variables available to set.** Access to the deployed agent's configuration
   (environment variables or the equivalent configuration store) to set:
   - `OpenClaw__AgentPolicy__CalendarWriteEnabled=true` (the global kill switch; both flags
     default `false`)
   - `OpenClaw__AgentPolicy__EnableOrganizerReschedule=true` (the per-path flag)
5. **A test event to reschedule.** At least one organizer-owned calendar event, in a mailbox the
   agent is authorized to act on, for which the agent computes a reschedule proposal (an eligible
   intent per the spec's evaluation order: hydrated event, `IsOrganizer == true`, non-null
   `Start`/`End`, non-empty `EventId`, at least one proposed slot). Ideally use a low-impact test
   event, not a production meeting with live attendees, since the PATCH moves the event's
   start/end for all attendees.
6. **Read access to the agent's audit store and logs.** Access to query the `action_audit` table
   (or equivalent operational log surface) and the `series_moves` table, to confirm the audit
   trail described in Verification below.

## Step-by-step Instructions

### Step 1 — Verify or grant `Calendars.ReadWrite` admin consent (third-party UI, MCP-first/web-second sourced)

1. Sign in to the Microsoft Entra admin center at https://entra.microsoft.com with an account
   holding Privileged Role Administrator, Cloud Application Administrator, or Application
   Administrator.
2. Browse to **Entra ID** > **App registrations** > **All applications** (or **Enterprise
   applications** > **All applications** if the app is already provisioned in the tenant), and
   select the OpenClaw agent's application.
3. Select **API permissions** (under **Manage**).
4. Confirm the Microsoft Graph application permission `Calendars.ReadWrite` is listed. If it is
   not, add it before continuing (adding the permission itself is outside this runbook).
5. Review the permission, then select **Grant admin consent for `<tenant>`** and confirm **Yes**
   in the dialog.
6. Confirm the permission row now shows status **Granted for `<tenant>`** with a green check mark.

If the tenant instead scopes calendar write access through Exchange Online Application RBAC
(F11's `Grant-OpenClawRbacRoles` module), re-run that cmdlet with `-IncludeCalendarWrite` to add
the `Application Calendars.ReadWrite` role to the existing scoped assignment, then re-run
`Test-OpenClawScopeBoundary` to confirm the boundary still holds. This is the scoped alternative to
the tenant-wide application permission above; use one or the other, not both, per the
organization's chosen access model.

### Step 2 — Enable the two calendar-write flags

Set both environment variables on the deployed agent's configuration, then restart or otherwise
apply the configuration change per the deployment's normal operational process (out of scope for
this runbook, which assumes F16's Stage 1 IaC deployment is already in place):

```
OpenClaw__AgentPolicy__CalendarWriteEnabled=true
OpenClaw__AgentPolicy__EnableOrganizerReschedule=true
```

Both flags default to `false`; both must be `true` simultaneously for the write path to proceed
(the four-row gate truth table in the spec: only `true`/`true` writes; any other combination
remains dry-run).

### Step 3 — Trigger or wait for an organizer-reschedule intent

Allow the agent's normal evaluation cycle to run against the test event identified in
Prerequisites item 5, or trigger the agent's evaluation pipeline manually per the deployment's
existing operational procedure for invoking a single evaluation cycle.

### Step 4 — Observe the Microsoft Graph `PATCH` call

Confirm, from the agent's structured logs or telemetry, that exactly one `PATCH
/users/{principal}/events/{id}` request was issued to Microsoft Graph for the test event, carrying
a bearer token, a `client-request-id` header, and a JSON body containing exactly two top-level
properties, `start` and `end`, each a `dateTimeTimeZone` pair in UTC at seconds precision. No
`body`, `subject`, `location`, or `attendees` property should appear in the request (the adapter
structurally cannot send them; this call updates only `start`/`end`).

### Step 5 — Confirm the event moved

Open the test event in the tenant's calendar (Outlook web, desktop, or mobile) and confirm its
start and end time now match the new time the agent proposed.

## Verification

Confirm success by observing all of the following:

1. **Calendar state.** The target event's start and end time in the tenant calendar match the new
   proposed time, not the original time.
2. **Audit record.** Exactly one `action_audit` row for this evaluation carries result code
   `rescheduled`, action type `organizer-reschedule`, and populated `EventId`,
   `OriginalStartUtc`, `OriginalEndUtc`, `NewStartUtc`, `NewEndUtc` columns.
3. **Move-history record.** Exactly one `series_moves` row exists for the event's series key,
   keyed on the pre-move occurrence start (so a subsequent reschedule evaluation for the same
   series observes this move via `OneOnOneMoveGuard`).
4. **Dedupe prevents a duplicate write.** Re-trigger evaluation of the same message (Step 3)
   without changing the flags. Confirm the agent issues zero further Graph `PATCH` requests for
   this event and the audit trail shows a `dedupe_skipped` result code instead.
5. **On failure of the write itself:** if the audit instead shows `reschedule_failed`, the event
   did not move, and no `series_moves` or dedupe row was written (per the spec's fail-closed
   ordering, a failed write leaves no partial bookkeeping so a retry remains possible). Investigate
   the recorded `ErrorDetail` before retrying.

### Rollback

To return to the pre-verification, dry-run state:

1. Set both flags back to their default `false` values:
   ```
   OpenClaw__AgentPolicy__CalendarWriteEnabled=false
   OpenClaw__AgentPolicy__EnableOrganizerReschedule=false
   ```
   and apply the configuration change per the deployment's normal operational process.
2. Confirm dry-run parity: trigger or wait for the next evaluation cycle against an eligible
   intent and confirm the audit trail shows `reschedule_disabled` (with the four time columns
   still populated for visibility) and that no further Graph `PATCH`, `series_moves` row, or
   dedupe row is produced. This matches the spec's documented behavior for both flags off.
3. If `Calendars.ReadWrite` admin consent or the Exchange Application RBAC
   `Application Calendars.ReadWrite` role was granted solely to perform this verification and the
   organization does not intend to keep organizer-reschedule enabled, revoke the tenant-wide
   consent (Entra admin center > the application's **API permissions** > remove the assignment) or
   remove the scoped Application RBAC role assignment, per the organization's access-review policy.
   Revocation is an operator decision outside this runbook's required steps.

## Source and Citation

Steps 1 and the rollback's consent-revocation note are third-party UI navigation, sourced
MCP-first, web-second. No MCP documentation-retrieval tool is wired as a dependency in this
repository at the time of writing (confirmed by repo-wide search; documented as a standing
condition in the two-axis-model-selection spec's Out of Scope section), so `WebFetch` against
`learn.microsoft.com` is the sole sourcing mechanism used below. All citations were captured
2026-07-07.

| Step | Content documented | Source URL | updated_at |
|---|---|---|---|
| Prerequisites (permission) | `Calendars.ReadWrite` application permission — description, admin-consent requirement | https://learn.microsoft.com/en-us/graph/permissions-reference | 2026-07-07 |
| Step 1 (UI navigation) | Grant tenant-wide admin consent to an application (Microsoft Entra admin center) | https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent | 2026-06-15 |
| Step 1 (Exchange RBAC alternative) | `Application Calendars.ReadWrite` Exchange Online Application RBAC role | https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac | 2026-03-16 |
| Step 4 (wire contract) | Update event — HTTP method/URL, `Calendars.ReadWrite` permission requirement, updatable properties including `start`/`end` | https://learn.microsoft.com/en-us/graph/api/event-update | 2026-05-19 |
| Internal source | Gate truth table, evaluation order, audit result codes, wire-body shape, fail-closed rules | `docs/features/active/2026-07-07-organizer-reschedule-128/spec.md`, sections "Behavior" and "API / Contract Surface" | 2026-07-07 |
| Internal precedent | HI-1 record shape and `-IncludeCalendarWrite` deferred-role pattern | `docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` | 2026-07-02 |
