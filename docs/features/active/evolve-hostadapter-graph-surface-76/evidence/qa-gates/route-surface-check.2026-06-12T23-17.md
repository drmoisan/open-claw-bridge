# Route Surface Check

Timestamp: 2026-06-12T23-17

Command: `grep -c '"/v1/' src/OpenClaw.HostAdapter/Program.cs` and `grep -A1 'app.MapGet' src/OpenClaw.HostAdapter/Program.cs`

EXIT_CODE: 0

## /v1/ template scan

Zero matches for the `"/v1/` literal in `src/OpenClaw.HostAdapter/Program.cs`. No bespoke `/v1/*` route template remains.

## Route inventory (post-change)

The HostAdapter registers exactly five GET routes:

1. `GET /status` — operational probe (sole non-`/users/{id}/...` route; no Graph equivalent).
2. `GET /users/{id}/messages` — single handler with two branches:
   - plain messages: dispatches `BuildListMessages` when `$filter` has no `meetingMessageType ne null` predicate.
   - meeting-requests: dispatches `BuildListMeetingRequests` when `$filter` contains `meetingMessageType ne null` (design note N1; verified by the two new dispatch tests in `HostAdapterMappingTests.cs`).
3. `GET /users/{id}/messages/{messageId}` — single message.
4. `GET /users/{id}/calendarView` — calendar window (`startDateTime`/`endDateTime`/`$top`).
5. `GET /users/{id}/events/{eventId}` — single event.

## Verdict

PASS. No `/v1/` template remains; `/status` is the only non-Graph route; all other routes are Graph-shaped `/users/{id}/...` forms.
