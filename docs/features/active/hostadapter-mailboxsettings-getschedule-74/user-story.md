# `hostadapter-mailboxsettings-getschedule` — User Story

- Issue: #74
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-06-13

## Story Statement

- As the PI-1 OpenClaw agent, I want the local HostAdapter to expose Microsoft
  Graph-shaped `mailboxSettings` and `calendar/getSchedule` (free/busy) routes, so that the
  `propose_times` scheduling pipeline can read a mailbox time zone, working hours, and a
  deterministic free/busy grid using the same request shapes I will issue against real
  Microsoft Graph in Product Increment 1 without rework.
- As a maintainer of `OpenClaw.Core`, I want the two scheduling methods that are currently
  `NotSupportedException` stubs (`HostAdapterSchedulingService.GetMailboxSettingsAsync` and
  `GetFreeBusyAsync`) implemented by delegating to `IHostAdapterClient`, so that the
  scheduling seam returns real data over the existing loopback HTTP contract while the
  send-mail path remains deferred to #75.

## Problem / Why

The deterministic scheduling pipeline (`SlotProposer.ProposeTimes`, driven by
`SchedulingWorker`) is already wired to call `ISchedulingService.GetMailboxSettingsAsync`
and `GetFreeBusyAsync`, but both methods throw `NotSupportedException` with comments marking
them deferred to #74/#75. As a result the `propose_times` pipeline cannot compute candidate
slots from real availability: it has no mailbox time zone, no working-hours window, and no
busy intervals to avoid.

The HostAdapter exposes a Microsoft Graph-shaped HTTP surface (post-#76:
`/users/{id}/messages`, `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`), but it
does not yet expose the two Graph endpoints the scheduling pipeline needs:
`GET /users/{id}/mailboxSettings` and the `getSchedule` free/busy operation. Adding these two
routes in the same Graph-shaped style means the agent's request-construction code is portable
to real Graph in PI-1, and it closes the two deferred stubs without coupling
`OpenClaw.Core`'s scheduling path to the SQLite cache (`CoreCacheRepository`). This is one of
the pieces of work identified as required to reach the Local MVP.

## Personas & Scenarios

- Persona: the PI-1 OpenClaw agent (acting through `HostAdapterHttpClient`, the
  `HostAdapterSchedulingService` seam, and the `SlotProposer` in `OpenClaw.Core`).
  - who the user is: the local agent runtime that computes proposed meeting times over
    loopback HTTP from the HostAdapter.
  - what they care about: reading a mailbox time zone, working hours, and a deterministic
    free/busy grid through the same Graph route shapes that PI-1 will use against real
    Microsoft Graph.
  - their constraints: must operate over the existing envelope contract (`ApiEnvelope<T>`);
    must not introduce new project dependencies or cross the COM boundary; must not couple
    the Core scheduling path to `CoreCacheRepository`; free/busy must be deterministic
    (driven by the injected `TimeProvider`, never wall-clock time).
  - their goals and frustrations: the goal is a working, Graph-portable `propose_times`
    pipeline; the frustration being removed is the two `NotSupportedException` stubs that
    block slot proposal.
  - their context and motivations: Stage 0 local development that must transfer cleanly to a
    real Graph integration in PI-1.
- Scenario: the agent proposes meeting times in response to a scheduling message.
  - who is acting? the OpenClaw agent, through `SchedulingWorker` ->
    `HostAdapterSchedulingService` -> `HostAdapterHttpClient`.
  - what triggered the action? a scheduling message enters the `propose_times` pipeline and
    `SlotProposer.ProposeTimes` needs the mailbox working hours and the free/busy grid.
  - what steps do they take? the client issues
    `GET /users/me/mailboxSettings` to obtain the time zone and working hours, then issues the
    Graph-shaped `getSchedule` request
    (`GET /users/me/calendar/getSchedule?startDateTime={iso8601}&endDateTime={iso8601}`)
    against the local adapter to obtain the free/busy grid for the proposal window.
  - what obstacles or decisions occur? the adapter sources the mailbox settings from
    `HostAdapterOptions.MailboxSettings` configuration (no bridge round-trip), and computes
    the free/busy grid from the bridge calendar data it already fetches through the CLI client
    process (`IHostAdapterProcessRunner`). The grid boundaries are deterministic; the window
    is supplied by the caller, which computed it from the injected `TimeProvider`.
  - what outcome do they expect? two `200` responses — an
    `ApiEnvelope<MailboxSettingsDto>` carrying a valid time zone and working hours, and an
    `ApiEnvelope<FreeBusyScheduleDto>` carrying busy intervals — so the `SlotProposer` can
    select candidate slots that avoid busy time and fall within working hours.

## Acceptance Criteria

- [x] `mailboxSettings` returns a valid time zone and working hours, sourced from
  `HostAdapterOptions.MailboxSettings` configuration (defaults: `TimeZoneId` UTC, working days
  Monday–Friday, working hours 09:00–17:00).
- [x] `getSchedule` returns a free/busy grid computed deterministically from the cached
  calendar data the HostAdapter fetches through the CLI client process; the grid is computed
  via the injected `TimeProvider`, never wall-clock time.
- [x] `IHostAdapterClient` and `HostAdapterHttpClient` expose both new methods
  (`GetMailboxSettingsAsync`, `GetFreeBusyAsync`) returning `ApiEnvelope<MailboxSettingsDto>`
  and `ApiEnvelope<FreeBusyScheduleDto>` respectively; the additions are additive to the
  interface.
- [x] `HostAdapterSchedulingService.GetMailboxSettingsAsync` and `GetFreeBusyAsync` are
  implemented by delegating to `IHostAdapterClient` (mirroring `GetCalendarViewAsync` and
  `GetEventAsync`); the `NotSupportedException` stubs for these two methods are removed
  (the `SendMailAsync` stub remains deferred to #75); the `propose_times` pipeline consumes
  them.
- [x] `MailboxSettingsDto`, `FreeBusyScheduleDto`, and `BusyIntervalDto` are relocated from
  `OpenClaw.Core.Agent` to `OpenClaw.HostAdapter.Contracts`; all references are updated; the
  contract/schema round-trip tests pass.
- [x] The free/busy computation uses `TimeProvider`; tests advance simulated time with
  `FakeTimeProvider` and mock the data source (`IHostAdapterProcessRunner` on the HostAdapter
  side, `IHostAdapterClient` on the Core side); no temporary files; no real Outlook or
  network.
- [x] Line coverage >= 85% and branch coverage >= 75% on changed code; no coverage regression
  on changed lines.

## Non-Goals

The following are explicitly excluded from #74:

- Send-mail (outbound) support. `ISchedulingService.SendMailAsync` and its
  `HostAdapterSchedulingService` stub remain `NotSupportedException` and are scoped to #75.
  No outbound mail route is added.
- Any Outlook COM change. No code in `OpenClaw.MailBridge` is modified; the HostAdapter
  continues to obtain calendar data only through the CLI client process, not through COM.
- New project dependencies. No new `ProjectReference` edge is added. The relocation of the
  DTOs into `OpenClaw.HostAdapter.Contracts` uses only the existing dependency on
  `OpenClaw.MailBridge.Contracts`.
- Coupling the Core scheduling path to `CoreCacheRepository`. Free/busy for `OpenClaw.Core`
  is obtained over HTTP from the HostAdapter; the Core SQLite cache is not consulted for
  free/busy.
- The `availabilityView` bitmap and `scheduleItems` detail beyond busy intervals.
  `FreeBusyScheduleDto` carries only `BusyIntervals`; the Graph `availabilityView` string is
  an intentional Stage-0 simplification because the `SlotProposer` needs only busy intervals.
- Changing the `ApiEnvelope<T>`, `MessageDto`, `EventDto`, `ApiMeta`, or `ApiError` schemas.
  This feature is additive at the route and contract level only.
