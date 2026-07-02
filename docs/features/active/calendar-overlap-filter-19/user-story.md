# calendar-overlap-filter (User Story)

- **Issue:** #19
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T07-41
- **Status:** Draft
- **Version:** 0.1

> Note: This feature runs in `full-bug` work mode, where `spec.md` is the sole authoritative acceptance-criteria source. This user story exists because the planner gate mechanically requires the artifact; it carries context only, not trackable acceptance criteria.

## Story

As the scheduling agent computing free/busy from the cached calendar window, I need the calendar scan to return every event that overlaps the query window — including events that started before the window but are still in progress, and events that span the entire window — so that the deterministic availability algorithm (`docs/open-claw-approach.master.md` section 10.4) never reports occupied time as free.

## Narrative

The scan window today is defined by start-time membership: only events whose `Start` falls inside `[windowStart, windowEnd)` are cached. From the scheduling agent's perspective, an in-progress meeting is exactly the kind of event that must block a proposed slot, yet it is the event most likely to be excluded by a start-only predicate. Correct behavior is interval overlap: an event belongs to the window when `Start < windowEnd AND End > windowStart`.

## Value

- Free/busy projections (`src/OpenClaw.HostAdapter/FreeBusyProjection.cs`) reflect actual calendar contents.
- Scheduling recommendations stop proposing times that conflict with in-progress or window-spanning meetings.

## Acceptance Criteria

See `spec.md` — Acceptance Criteria (authoritative for `full-bug` work mode).
