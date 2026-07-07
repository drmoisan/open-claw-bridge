# Attendee Propose-New-Time Live-Tenant Verification — Human-Exception Runbook

- **Feature:** attendee-propose-new-time (Issue #130)
- **Runbook path:** `docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`
- **Authored:** 2026-07-07
- **Source checklist:** `docs/features/active/2026-07-07-attendee-propose-new-time-130/spec.md` (Behavior, API/Contract Surface, Constraints & Risks sections); `docs/features/active/2026-07-07-attendee-propose-new-time-130/research/2026-07-07T09-45-attendee-propose-new-time.research.md` (Section 2 Graph contract, Automation Feasibility table); F18 (`organizer-reschedule`, issue #128) `runbooks/organizer-reschedule-live-verification.runbook.md` HI-1 record-shape precedent

## Cue

Act on this runbook when the orchestrator records live-tenant execution of the F19
attendee-propose-new-time verification as a permitted human exception:
`artifacts/orchestration/orchestrator-state.json` contains a `human_interaction.requirements[]`
entry (HI-1) with `response: "exception"` and `runbook_path` pointing to this file. The trigger
event is a request to enable `ENABLE_ATTENDEE_PROPOSE_NEW_TIME` in a real Microsoft 365 tenant, or
a request to verify the first live attendee-side propose-new-time response before that enablement is
relied upon in production. Live execution against a real tenant cannot and must not run in CI: no
Azure/Exchange credentials exist in this environment or CI, and issuing the write requires a human
tenant administrator's `Calendars.ReadWrite` admin consent plus a human operator with tenant access
and two mailboxes to observe the result. The write path itself is proven by mocked-Graph contract
tests (`GraphHostAdapterClientProposeNewTimeTests`, the established `FakeHttpHandler` pattern); this
runbook covers only the unautomatable live-tenant confirmation.

This path is the attendee-side analogue of F18 organizer-reschedule. Two facts differ materially
from F18 and are reflected in the steps below: the Graph call is a
`POST /users/{principal}/events/{id}/tentativelyAccept` that returns `202 Accepted` with no response
body (F18 was a `PATCH` returning `200` plus the updated event), and the call sends a meeting-response
proposal to the organizer rather than moving any calendar event, so the test setup requires two
mailboxes and there is no `series_moves` interaction on this path.

## Prerequisites

All of the following must be true before starting:

1. **Tenant and app registration.** A Microsoft 365 / Microsoft Entra tenant exists, and an
   Enterprise Application (service principal) is registered for the OpenClaw agent, either newly
   created or the same registration used for F18 organizer-reschedule and F11's Exchange RBAC
   scoping.
2. **`Calendars.ReadWrite` permission granted with admin consent.** The registered application
   holds the Microsoft Graph application permission `Calendars.ReadWrite` (the least-privileged
   application permission for `tentativelyAccept`, and the **same grant F18 requires**), and a
   tenant administrator (Privileged Role Administrator, Cloud Application Administrator, or
   Application Administrator) has granted tenant-wide admin consent. `Calendars.ReadWrite` requires
   admin consent; it is not grantable by an end user. If the F18 organizer-reschedule runbook was
   already executed against this tenant, this consent is likely already in place and Step 1 becomes
   a confirmation rather than a new grant.
3. **OpenClaw agent deployed.** The Stage 1 infrastructure-as-code deployment (F16, issue #125) is
   complete, and the deployed agent can reach Microsoft Graph and authenticate as the registered
   application. The Graph adapter must be the active backend: the local Stage-0 HTTP adapter fails
   this path closed with a `NOT_SUPPORTED` envelope by design (it has no meeting-response route).
4. **Environment variables available to set.** Access to the deployed agent's configuration
   (environment variables or the equivalent configuration store) to set both of:
   - `OpenClaw__AgentPolicy__CalendarWriteEnabled=true` (the global kill switch; defaults `false`)
   - `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime=true` (the per-path flag; defaults
     `false`)
5. **Two mailboxes for a live proposal.** Unlike F18 (which needed only one organizer-owned event),
   the attendee path requires two mailboxes:
   - A **second (organizer) mailbox** that creates a test meeting and **invites the principal**,
     with "allow new time proposals" enabled on the meeting (Outlook default; confirm it is on).
   - The **principal mailbox** (the invited attendee the agent is authorized to act on), for which
     the agent computes a propose-new-time intent — an eligible intent per the spec's evaluation
     order: hydrated event, `IsOrganizer == false`, `AllowNewTimeProposals == true`, non-null
     `Start`/`End`, non-empty `EventId`, and at least one proposed slot. Use a low-impact test
     meeting, not a production meeting with live attendees.
6. **Read access to the agent's audit store and logs.** Access to query the `action_audit` table
   (or equivalent operational log surface) and the sent-action dedupe store, to confirm the audit
   trail described in Verification below. Note there is no `series_moves` row on this path, so no
   move-history query is required.

## Step-by-step Instructions

### Step 1 — Verify or grant `Calendars.ReadWrite` admin consent (third-party UI, MCP-first/web-second sourced)

1. Sign in to the Microsoft Entra admin center at https://entra.microsoft.com with an account
   holding Privileged Role Administrator, Cloud Application Administrator, or Application
   Administrator.
2. Browse to **Entra ID** > **App registrations** > **All applications** (or **Enterprise
   applications** > **All applications** if the app is already provisioned in the tenant), and
   select the OpenClaw agent's application.
3. Select **API permissions** (under **Manage**).
4. Confirm the Microsoft Graph application permission `Calendars.ReadWrite` is listed. Because this
   is the same permission F18 requires, it may already be present and consented from the
   organizer-reschedule verification. If it is not listed, add it before continuing (adding the
   permission itself is outside this runbook).
5. If the permission row does not already show consent, select **Grant admin consent for
   `<tenant>`** and confirm **Yes** in the dialog.
6. Confirm the permission row shows status **Granted for `<tenant>`** with a green check mark.

### Step 2 — Enable the two calendar-write flags

Set both environment variables on the deployed agent's configuration, then restart or otherwise
apply the configuration change per the deployment's normal operational process (out of scope for
this runbook, which assumes F16's Stage 1 IaC deployment is already in place):

```
OpenClaw__AgentPolicy__CalendarWriteEnabled=true
OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime=true
```

Both flags default to `false`; both must be `true` simultaneously for the write path to proceed
(the four-row gate truth table in the spec: only `true`/`true` writes; any other combination remains
dry-run).

### Step 3 — Create the test meeting and invite the principal

From the **second (organizer) mailbox**, create a calendar meeting and invite the principal
mailbox. Confirm the meeting allows new time proposals — in Outlook, the "allow new time proposals"
response option is on by default; verify it has not been disabled for this meeting. Send the
invitation so that it arrives in the principal's mailbox as a meeting request the agent will
evaluate. On this path the principal is the invited attendee (`IsOrganizer == false`), which is the
exact mirror of F18's organizer check.

### Step 4 — Trigger or wait for an attendee propose-new-time intent

Allow the agent's normal evaluation cycle to run against the principal's invitation from Step 3, or
trigger the agent's evaluation pipeline manually per the deployment's existing operational procedure
for invoking a single evaluation cycle. The agent computes a proposed alternate time and, with both
flags on, issues the propose-new-time response.

### Step 5 — Observe the Microsoft Graph `POST .../tentativelyAccept` call

Confirm, from the agent's structured logs or telemetry, that exactly one
`POST /users/{principal}/events/{id}/tentativelyAccept` request was issued to Microsoft Graph for
the test event, carrying a bearer token, a `client-request-id` header, and a JSON body containing
exactly two top-level properties:

```json
{
  "sendResponse": true,
  "proposedNewTime": {
    "start": { "dateTime": "<proposed-start>", "timeZone": "UTC" },
    "end":   { "dateTime": "<proposed-end>",   "timeZone": "UTC" }
  }
}
```

`sendResponse` is hardcoded `true` because Graph requires it whenever `proposedNewTime` is set
(Graph returns `400` otherwise). The `start` and `end` values are `dateTimeTimeZone` pairs rendered
in UTC at seconds precision. No other properties should appear in the request — in particular no
`comment` and no top-level `start`/`end`/`body`/`subject`/`attendees` (the adapter structurally
cannot send them; this call sends a meeting-response proposal and cannot rewrite the event). A
successful call returns `202 Accepted` with **no response body**.

Note that this call does **not** modify the organizer's calendar. The proposal is delivered to the
organizer as a meeting-response message; the organizer sees it on the response message and on the
`attendee.proposedNewTime` property. The event's times change only if the organizer separately
accepts the proposal.

### Step 6 — Confirm the proposal reached the organizer

Open the **second (organizer) mailbox** and confirm a meeting-response message arrived from the
principal proposing the new time (the tentative response carrying a proposed new time). Confirm the
organizer's copy of the event has **not** been moved by this call — the original meeting time is
unchanged until the organizer acts on the proposal.

## Verification

Confirm success by observing all of the following:

1. **Graph call and response.** Exactly one `POST /users/{principal}/events/{id}/tentativelyAccept`
   was issued (Step 5), and Graph returned `202 Accepted` with an empty body.
2. **Proposal delivered.** A tentative meeting-response message carrying the proposed new time
   arrived in the organizer mailbox, and the organizer's event was not moved (Step 6).
3. **Audit record.** Exactly one `action_audit` row for this evaluation carries result code
   `proposed_new_time`, action type `attendee-propose-new-time`, and populated `EventId`,
   `OriginalStartUtc`, `OriginalEndUtc`, `NewStartUtc`, `NewEndUtc` columns.
4. **Dedupe record.** Exactly one sent-action dedupe row exists for key
   `{mailbox}:{messageId}:attendee-propose-new-time`, written only after the successful write.
5. **No move-history record.** There is **no** `series_moves` row for this path in any branch. The
   attendee propose-new-time performs no calendar move and does not consult or write the move guard.
   If a `series_moves` row appears, investigate — it indicates a defect, because writing here would
   corrupt the F18 move-guard history.
6. **Dedupe prevents a duplicate write.** Re-trigger evaluation of the same message (Step 4) without
   changing the flags. Confirm the agent issues zero further Graph `POST` requests for this event
   and the audit trail shows a `dedupe_skipped` result code instead.
7. **On failure of the write itself:** if the audit instead shows `propose_new_time_failed`, no
   proposal was delivered and no dedupe row was written (per the spec's fail-closed ordering, a
   failed write leaves no dedupe bookkeeping so a retry on the next cycle remains possible).
   Investigate the recorded `ErrorDetail` before retrying. A `400 INVALID_REQUEST` here most likely
   means the meeting did not actually allow new time proposals (Graph is authoritative and returns
   `400` when `allowNewTimeProposals` is `false`); re-check Step 3.

### Rollback

To return to the pre-verification, dry-run state:

1. Set both flags back to their default `false` values:
   ```
   OpenClaw__AgentPolicy__CalendarWriteEnabled=false
   OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime=false
   ```
   and apply the configuration change per the deployment's normal operational process. Setting
   either flag back to `false` is sufficient to return to dry-run parity, because the gate requires
   both.
2. Confirm dry-run parity: trigger or wait for the next evaluation cycle against an eligible intent
   and confirm the audit trail shows `propose_new_time_disabled` (with the four time columns still
   populated for visibility) and that no Graph `POST`, no write-path token acquisition, and no
   dedupe row is produced. This matches the spec's documented flag-off behavior.
3. If `Calendars.ReadWrite` admin consent was granted solely to perform this verification and the
   organization does not intend to keep the calendar-write paths enabled, revoke the tenant-wide
   consent (Entra admin center > the application's **API permissions** > remove the assignment) per
   the organization's access-review policy. Note that this consent is shared with F18
   organizer-reschedule; do not revoke it if F18 is intended to remain enabled. Revocation is an
   operator decision outside this runbook's required steps.

## Source and Citation

Step 1 and the rollback's consent-revocation note are third-party UI navigation, sourced MCP-first,
web-second. No MCP documentation-retrieval tool is wired as a dependency in this repository at the
time of writing (confirmed by repo-wide search; documented as a standing condition in the
two-axis-model-selection spec's Out of Scope section), so `WebFetch` against `learn.microsoft.com`
is the sole sourcing mechanism used below. All citations were captured 2026-07-07; the `updated_at`
column records the last-published date shown on each Microsoft Learn page at capture time.

| Step | Content documented | Source URL | updated_at |
|---|---|---|---|
| Prerequisites (permission) | `Calendars.ReadWrite` application permission — description, admin-consent requirement | https://learn.microsoft.com/en-us/graph/permissions-reference | 2026-07-07 |
| Step 1 (UI navigation) | Grant tenant-wide admin consent to an application (Microsoft Entra admin center) | https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent | 2026-06-15 |
| Steps 5–6 (wire contract) | `event: tentativelyAccept` — HTTP method/URL, application permission `Calendars.ReadWrite`, `202 Accepted` with no response body, `sendResponse`/`proposedNewTime` parameters, `sendResponse` must be `true` when `proposedNewTime` is set (400 otherwise) | https://learn.microsoft.com/en-us/graph/api/event-tentativelyaccept | 2025-07-23 |
| Steps 5–6 (concept) | Propose new meeting times — `tentativelyAccept` is the canonical propose-new-time response; organizer receives the proposal on `attendee.proposedNewTime`; organizer's calendar not modified by this call | https://learn.microsoft.com/en-us/graph/outlook-calendar-meeting-proposals | 2026-07-07 |
| Internal source | Gate truth table, evaluation order, eligibility predicates, audit result codes, dedupe key, wire-body shape, no-`series_moves` decision, fail-closed rules | `docs/features/active/2026-07-07-attendee-propose-new-time-130/spec.md`, sections "Behavior" and "API / Contract Surface" | 2026-07-07 |
| Internal source | Section 2 Graph contract verification, Automation Feasibility table (HI-1 exception + runbook path) | `docs/features/active/2026-07-07-attendee-propose-new-time-130/research/2026-07-07T09-45-attendee-propose-new-time.research.md` | 2026-07-07 |
| Internal precedent | HI-1 record shape and five-section runbook structure | `docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md` | 2026-07-07 |
