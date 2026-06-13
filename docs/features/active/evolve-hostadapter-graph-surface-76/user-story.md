# `evolve-hostadapter-graph-surface` — User Story

- Issue: #76
- Owner: drmoisan
- Status: Ready
- Last Updated: 2026-06-12

## Story Statement

- As the PI-1 OpenClaw agent, I want the local HostAdapter to expose Microsoft
  Graph-shaped routes (`/users/{id}/messages`, `/users/{id}/messages/{messageId}`,
  `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`, and a messages-filtered
  meeting-requests form), so that the request shapes I build in Stage 0 are the same ones I
  will issue against the real Microsoft Graph in Product Increment 1 without rework.
- As a maintainer of `OpenClaw.Core`, I want the breaking HostAdapter contract change to be
  signaled by a major adapter version (`1.0.0`) and a configurable mailbox identifier
  (`MailboxId`, default `"me"`), so that the route reshaping is explicit, traceable, and
  configurable without changing the unchanged DTO/envelope schema.

## Problem / Why

The HostAdapter currently serves a bespoke `/v1/*` route table (`/v1/messages`,
`/v1/calendar`, `/v1/meeting-requests`, and so on) with bespoke query parameters (`since`,
`start`, `end`, `limit`). The OpenClaw agent in Product Increment 1 will call Microsoft Graph,
which uses a different URL shape (`/users/{id}/messages` with OData `$filter` and `$top`,
`/users/{id}/calendarView` with `startDateTime`/`endDateTime`). If the agent is developed
against bespoke routes in Stage 0, its request-construction code must be rewritten to reach
Graph later. Reshaping the local adapter to Graph-shaped routes now lets the agent's
request-construction code be portable across the local adapter and real Graph, removing that
rework and reducing the divergence between Stage 0 and PI-1 behavior. This is one of the two
pieces of work identified as required to reach the Local MVP.

## Personas & Scenarios

- Persona: the PI-1 OpenClaw agent (acting through `HostAdapterHttpClient` and the
  scheduling layer in `OpenClaw.Core`).
  - who the user is: the local agent runtime that reads mail and calendar data over loopback
    HTTP from the HostAdapter.
  - what they care about: building request URLs once and reusing them unchanged against the
    real Microsoft Graph in PI-1.
  - their constraints: must operate over the existing envelope/DTO contract
    (`ApiEnvelope<T>`, `ItemsResponse<T>`, `MessageDto`, `EventDto`); must not introduce new
    project dependencies or cross the COM boundary.
  - their goals and frustrations: the goal is Graph-portable request construction; the
    frustration being removed is the bespoke `/v1/*` shape that has no Graph equivalent.
  - their context and motivations: Stage 0 local development that must transfer cleanly to a
    real Graph integration in PI-1.
- Scenario: the agent retrieves a recent message window.
  - who is acting? the OpenClaw agent, through `HostAdapterHttpClient`.
  - what triggered the action? a polling cycle or an agent task that needs recent messages
    since a known timestamp.
  - what steps do they take? the client issues
    `GET /users/me/messages?$filter=receivedDateTime ge 2026-06-12T00:00:00Z&$top=50`
    against the local adapter at the new base URL `http://host.docker.internal:4319/`
    (no `/v1/`). The `{id}` segment resolves to `me` from `HostAdapterOptions.MailboxId`.
  - what obstacles or decisions occur? the adapter validates the Graph-shaped query
    parameters (`$filter` receivedDateTime lower bound, `$top` against `MaxLimit`) and
    resolves the request through the unchanged bridge-cache RPC chain.
  - what outcome do they expect? a `200` response with an
    `ApiEnvelope<ItemsResponse<MessageDto>>` whose `meta.adapterVersion` is `1.0.0`, and a
    URL shape identical to the one the agent will later send to real Microsoft Graph.

## Acceptance Criteria

- [x] HostAdapter exposes the Graph-shaped routes (`/users/{id}/messages`, `/users/{id}/messages/{messageId}`, `/users/{id}/calendarView`, `/users/{id}/events/{eventId}`, and meeting-requests as a messages-filtered query on `meetingMessageType`) and no longer serves the `/v1/*` bespoke routes (the `/status` operational probe excepted).
- [x] `IHostAdapterClient` and `HostAdapterHttpClient` call the Graph-shaped endpoints and receive envelope-wrapped (`ApiEnvelope<T>`) results, with `ListMeetingRequestsAsync` retained on the interface.
- [x] `OpenClawOptions.HostAdapter.BaseUrl` default no longer contains `/v1/`.
- [x] The adapter version reports `1.0.0` (via `meta.adapterVersion`) to signal the breaking change.
- [x] `MailboxId` (default `"me"`) is configurable on `HostAdapterOptions` and is used to render the `{id}` path segment.
- [x] Existing contract/endpoint tests pass against the new routes (HostAdapter.Tests and Core.Tests updated, not weakened).
- [x] Line coverage >= 85% and branch coverage >= 75% on changed code; no coverage regression on changed lines.

## Non-Goals

The following are explicitly excluded from #76:

- Removing or reducing the meeting-requests capability. `ListMeetingRequestsAsync`, the
  HostAdapter meeting-requests route, the `list_recent_meeting_requests` bridge RPC chain,
  the `MessagePollingWorker.PollMeetingRequestsAsync` path, and the Core UI meeting-requests
  surface are all retained. Only the meeting-requests wire route and query parameters change
  to the Graph-shaped messages-filtered form.
- Changing DTOs or the envelope schema. `ApiEnvelope<T>`, `ItemsResponse<T>`, `MessageDto`,
  `EventDto`, `ApiMeta`, `ApiError`, and `BridgeStatusDto` are not modified. This is a
  path-and-query change only.
- Refactoring meeting-request filtering into the agent layer. Moving meeting-request
  identification out of the adapter and into client-side filtering on `meetingMessageType`
  is out of scope; the dedicated meeting-requests method and route are kept.
- Adding mailbox-settings, free/busy, or send-mail Graph routes. Those are scoped to #74/#75
  and remain unsupported here.
