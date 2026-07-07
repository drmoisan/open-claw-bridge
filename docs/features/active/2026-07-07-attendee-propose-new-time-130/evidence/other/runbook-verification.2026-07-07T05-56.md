# Runbook Content-Area Verification (F19, #130)

Timestamp: 2026-07-07T05-56
Command: `Read docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md` (content-area inspection; no shell command)
EXIT_CODE: 0

Output Summary:

Runbook exists at the exact path:
`docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`

Required content areas, in order, each found (pass/fail):

1. Confirm the `Calendars.ReadWrite` application-permission grant and tenant-admin consent (shared with F18; may already be granted) — PASS. Prerequisites item 2 (lines 38-45) and Step 1 (lines 71-86) cover confirming or granting `Calendars.ReadWrite` with admin consent, noting the F18 shared grant.
2. Enable `OpenClaw__AgentPolicy__CalendarWriteEnabled` and `OpenClaw__AgentPolicy__EnableAttendeeProposeNewTime` in a real deployment — PASS. Step 2 (lines 88-101) sets both environment variables and states both must be true simultaneously.
3. Observe a live two-mailbox proposal — the 202 response, the `proposed_new_time` audit row with the four time columns, the dedupe row, and the proposal arriving on the organizer's `attendee.proposedNewTime` — PASS. Steps 3-6 (lines 103-153) and Verification items 1-4 (lines 159-167) cover the two-mailbox setup, the 202-no-body Graph call, the `proposed_new_time` audit with `EventId`/`OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc`, the dedupe row, and delivery to the organizer via `attendee.proposedNewTime` (Source table row for `outlook-calendar-meeting-proposals`).
4. Disable the flags after verification (the global kill switch shuts the path off independently) — PASS. Rollback (lines 182-203) sets both flags back to `false`, noting either flag set to `false` returns to dry-run parity because the gate requires both.

Verdict: PASS. All four content areas present; no remediation required.
