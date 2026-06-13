# hostadapter-mailboxsettings-getschedule — Spec

- **Issue:** #74
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-13
- **Status:** Ready
- **Version:** 1.0

## Overview

This feature adds two Microsoft Graph-shaped routes to the `OpenClaw.HostAdapter` HTTP
surface and two additive methods to the `IHostAdapterClient` contract (a T2 boundary in
`OpenClaw.HostAdapter.Contracts`):

- `GET /users/{id}/mailboxSettings` — returns the mailbox time zone and working hours, sourced
  from HostAdapter configuration.
- `GET /users/{id}/calendar/getSchedule` — returns a free/busy grid computed deterministically
  from the bridge calendar data the HostAdapter already fetches through the CLI client process.

The two methods are then implemented in `OpenClaw.Core` by `HostAdapterSchedulingService`,
which currently throws `NotSupportedException` for both with comments marking them deferred to
#74/#75. After this change those two stubs are removed and replaced with delegations to
`IHostAdapterClient`, mirroring the existing `GetCalendarViewAsync`/`GetEventAsync` methods.
The `SendMailAsync` stub remains deferred to #75.

The three scheduling DTOs (`MailboxSettingsDto`, `FreeBusyScheduleDto`, `BusyIntervalDto`) are
relocated from `OpenClaw.Core.Agent` to `OpenClaw.HostAdapter.Contracts` so the interface can
return them inside `ApiEnvelope<T>` without `OpenClaw.HostAdapter.Contracts` taking a
dependency on `OpenClaw.Core` (which the dependency rules forbid).

- Target users/personas and primary use cases: the PI-1 OpenClaw agent whose `propose_times`
  pipeline (`SlotProposer.ProposeTimes` driven by `SchedulingWorker`) needs a mailbox time
  zone, working hours, and a free/busy grid; the request shapes are portable to real Microsoft
  Graph in PI-1.
- Success metrics or expected impact: the two `NotSupportedException` stubs for
  `GetMailboxSettingsAsync` and `GetFreeBusyAsync` are removed; the `propose_times` pipeline
  computes candidate slots from real availability; the additive `IHostAdapterClient` methods
  compile and pass against the new routes; the free/busy computation is deterministic under
  `FakeTimeProvider`.

## Behavior

The HostAdapter gains two Graph-shaped routes. Both follow the existing route lifecycle in
`Program.cs` (parameter validation via `HostAdapterRequestValidation`, response shaping via
`HostAdapterResponses` and `ToHttpResult`, envelope `ApiEnvelope<T>`).

- Main user flow (happy path):
  - The agent issues `GET /users/me/mailboxSettings`. The handler reads
    `IOptions<HostAdapterOptions>`, constructs a `MailboxSettingsDto` from the configured
    `MailboxSettings` section, and returns `ApiEnvelope<MailboxSettingsDto>`. This route does
    not call `RequireReadyBridgeAsync` and does not invoke `IHostAdapterProcessRunner`, because
    the data is config-sourced.
  - The agent issues
    `GET /users/me/calendar/getSchedule?startDateTime={iso8601}&endDateTime={iso8601}`. The
    handler validates the window (reusing `TryValidateWindow`), fetches the bridge calendar
    events for the window through the existing CLI client chain
    (`IHostAdapterProcessRunner.ExecuteAsync` with the calendar-list command), computes the
    busy intervals from the returned `EventDto` list, and returns
    `ApiEnvelope<FreeBusyScheduleDto>`.

- Free/busy computation (decision D1, explicit): the busy grid is derived from the
  `EventDto.BusyStatus` field (Outlook `OlBusyStatus`: 0=free, 1=tentative, 2=busy,
  3=outOfOffice). An event contributes a `BusyIntervalDto(StartUtc, EndUtc)` when its
  `BusyStatus` is not 0 (free). A null `BusyStatus` is treated as busy (conservative default)
  so an unannotated event does not silently mark time as free. The computation is a pure
  projection over the fetched events and is deterministic given a fixed event set and window.

- HTTP method shape (decision D2, explicit): real Microsoft Graph `getSchedule` is a POST with
  a JSON body (`{ schedules, startTime, endTime, availabilityViewInterval }`). For Stage 0 this
  spec uses a `GET /users/{id}/calendar/getSchedule` with `startDateTime`/`endDateTime` query
  parameters, matching the existing `calendarView` route validation and keeping all five
  current routes uniform (all `MapGet`). The Stage-0 GET form remains portable to PI-1 Graph
  because `HostAdapterHttpClient` is the single seam that constructs the wire request: when the
  PI-1 Graph backend replaces the local adapter, only `HostAdapterHttpClient.GetFreeBusyAsync`
  changes from a GET to the Graph POST-with-body form; the `IHostAdapterClient` method
  signature (`startUtc`, `endUtc`, `requestId`, `cancellationToken`) and every caller are
  unchanged. The window-as-typed-parameters signature is the portability boundary, not the
  wire verb.

- Mailbox-settings source (decision D3, explicit): the time zone and working hours are sourced
  from a new `MailboxSettings` subsection of `HostAdapterOptions`. The HostAdapter is the local
  configuration authority for host-specific settings; it does not shell out to the CLI for this
  data. The external `appsettings.json` loaded from `HostAdapterOptions.DefaultAppSettingsPath`
  is the site-specific override point.

- Alternate/edge flows: an empty calendar window yields an `ApiEnvelope<FreeBusyScheduleDto>`
  with an empty `BusyIntervals` list (not an error). A window with `start >= end` is rejected by
  the existing window validation with the current status and error codes.

- Error handling and recovery behavior: both routes use the existing `ApiError`/`ApiEnvelope<T>`
  envelope and the `HostAdapterResponses` factories (`Success`, `Failure`, `InvalidRequest`,
  `ConfigurationError`). On the Core side, `HostAdapterSchedulingService` follows the existing
  pattern of the read methods: when the envelope is not `{ Ok: true, Data: not null }`, the
  method returns the documented empty/default shape rather than throwing, so the deterministic
  pipeline degrades gracefully (consistent with `GetCalendarViewAsync` returning an empty list).

## Inputs / Outputs

- Inputs (CLI flags, files, env vars): no new CLI flags. The new configuration input is the
  `HostAdapterOptions.MailboxSettings` subsection (HostAdapter side). The `getSchedule` route
  reads `startDateTime` and `endDateTime` query parameters.
- Outputs (artifacts, logs, telemetry): two new response envelopes,
  `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>`. No new telemetry.
- Config keys and defaults (new `MailboxSettings` subsection under
  `OpenClaw:HostAdapter:MailboxSettings`, on a new `MailboxSettingsOptions` POCO referenced
  from `HostAdapterOptions`):
  - `TimeZoneId` (string, default `"UTC"`).
  - `WorkingDaysOfWeek` (string array, default `["Monday","Tuesday","Wednesday","Thursday","Friday"]`).
  - `WorkingHoursStart` (string `HH:mm`, default `"09:00"`).
  - `WorkingHoursEnd` (string `HH:mm`, default `"17:00"`).
- Versioning or backward-compatibility constraints: this is an additive change to the
  HostAdapter HTTP surface and to the `IHostAdapterClient` contract (T2 boundary). Adding new
  interface methods does not break existing callers, but every existing implementation
  (`HostAdapterHttpClient`) and every non-strict mock used in tests must add the new members.
  No existing route, DTO field, or envelope shape is removed or renamed.

## API / CLI Surface

New routes served by `OpenClaw.HostAdapter` (`Program.cs`):

```
GET /users/{id}/mailboxSettings
GET /users/{id}/calendar/getSchedule?startDateTime={iso8601}&endDateTime={iso8601}
```

New `IHostAdapterClient` methods (additive; `OpenClaw.HostAdapter.Contracts`):

```csharp
Task<ApiEnvelope<MailboxSettingsDto>> GetMailboxSettingsAsync(
    string? requestId = null,
    CancellationToken cancellationToken = default
);

Task<ApiEnvelope<FreeBusyScheduleDto>> GetFreeBusyAsync(
    DateTimeOffset startUtc,
    DateTimeOffset endUtc,
    string? requestId = null,
    CancellationToken cancellationToken = default
);
```

Example invocations with expected outputs (concise):

- `GET /users/me/mailboxSettings`
  -> `200` with `ApiEnvelope<MailboxSettingsDto>` (`TimeZoneId` UTC, working days Mon–Fri,
  09:00–17:00 by default).
- `GET /users/me/calendar/getSchedule?startDateTime=2026-06-15T00:00:00Z&endDateTime=2026-06-20T00:00:00Z`
  -> `200` with `ApiEnvelope<FreeBusyScheduleDto>` whose `BusyIntervals` reflect the cached
  calendar events in the window with `BusyStatus != 0`.
- `GET /users/me/calendar/getSchedule?startDateTime=2026-06-20T00:00:00Z&endDateTime=2026-06-15T00:00:00Z`
  -> rejected by window validation with the existing invalid-request status and error code.

Contracts and validation rules: the `getSchedule` route reuses the existing window-validation
helper (`TryValidateWindow`) and the `MaxLimit` ceiling when an item cap applies. The
`mailboxSettings` route performs no window validation. The DTO and envelope contracts are
unchanged except for the namespace relocation of the three scheduling DTOs.

## Data & State

- Data transformations and invariants: the free/busy grid is a pure projection from
  `EventDto` to `BusyIntervalDto`. Invariant: an event marks busy time when `BusyStatus != 0`;
  a null `BusyStatus` is treated as busy. The projection introduces no wall-clock dependency;
  window boundaries are supplied by the caller, which derives them from the injected
  `TimeProvider`.
- Caching or persistence details: no new persistence. The HostAdapter holds no calendar cache;
  it fetches calendar events for the window through the CLI client process
  (`IHostAdapterProcessRunner`) exactly as the existing `calendarView` route does.
  `OpenClaw.Core`'s scheduling path obtains free/busy over loopback HTTP and does not consult
  `CoreCacheRepository`.
- Migration or backfill requirements (if any): none for stored data. The only operator-facing
  configuration addition is the optional `MailboxSettings` subsection; the documented defaults
  apply when it is absent.

## Constraints & Risks

- T1/T2 contract boundary: `IHostAdapterClient` lives in `OpenClaw.HostAdapter.Contracts` (T2).
  Adding the two methods is an additive contract change. Per `.claude/rules/quality-tiers.md`,
  a change at a host-service contract boundary requires a contract/schema compatibility check.
  The check must confirm `HostAdapterHttpClient` implements both new members and that the
  relocated DTOs round-trip via JSON. `SchedulingDtoContractTests` provides the JSON round-trip
  coverage for `MailboxSettingsDto` and `FreeBusyScheduleDto` (extended to cover the
  `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` envelope shapes).
- Determinism: the free/busy computation must use the injected `TimeProvider` for any "now"
  derivation, never wall-clock time; the grid and window boundaries are deterministic. Tests
  advance simulated time with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).
- Architecture boundaries preserved (per `.claude/rules/architecture-boundaries.md`): no new
  `ProjectReference` edge. `OpenClaw.HostAdapter` continues to depend only on
  `OpenClaw.HostAdapter.Contracts` and `OpenClaw.MailBridge.Contracts` and must not reference
  `OpenClaw.Core` or the COM host. `OpenClaw.HostAdapter.Contracts` continues to depend only on
  `OpenClaw.MailBridge.Contracts`; the relocated DTOs use only `DayOfWeek`, `TimeOnly`,
  `DateTimeOffset`, and `IReadOnlyList<T>` from the BCL, so the relocation does not introduce a
  new edge. `OpenClaw.Core` continues to depend only on `OpenClaw.HostAdapter.Contracts`. No
  COM boundary is crossed.
- Limits and acceptable trade-offs: the `availabilityView` bitmap is not represented; only busy
  intervals are returned. This is an intentional Stage-0 simplification sufficient for the
  `SlotProposer`. The calendar fetch for free/busy is bounded by the existing `MaxLimit` item
  ceiling.
- Security/privacy considerations: no change to authentication or token handling. The `{id}`
  segment defaults to the non-identifying literal `"me"`; no real UPN is placed into URLs
  unless an operator configures `MailboxId`.
- Operational/rollout risks and mitigations: no feature flag. The additive routes and methods
  are inert until the `propose_times` pipeline invokes them. Operators who want non-default
  working hours or time zone set the `MailboxSettings` subsection; the documented defaults
  cover deployments that do not.

## Implementation Strategy

- Implementation scope (what changes, not sequencing): add two routes to the HostAdapter, add
  two methods to `IHostAdapterClient` and implement them in `HostAdapterHttpClient`, add a
  `MailboxSettings` options POCO to `HostAdapterOptions`, relocate the three scheduling DTOs to
  `OpenClaw.HostAdapter.Contracts`, implement the two previously-stubbed methods in
  `HostAdapterSchedulingService`, and update all references and tests.

- New files:
  - `src/OpenClaw.HostAdapter.Contracts/SchedulingContracts.cs` — relocated
    `MailboxSettingsDto`, `FreeBusyScheduleDto`, and `BusyIntervalDto` records, in the
    `OpenClaw.HostAdapter.Contracts` namespace.
  - `src/OpenClaw.HostAdapter/MailboxSettingsOptions.cs` — new POCO carrying `TimeZoneId`,
    `WorkingDaysOfWeek`, `WorkingHoursStart`, `WorkingHoursEnd`.

- Production files to change:
  - `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` — add `GetMailboxSettingsAsync`
    and `GetFreeBusyAsync` with XML docs describing the Graph-shaped routes.
  - `src/OpenClaw.HostAdapter/HostAdapterOptions.cs` — add a `MailboxSettings` property of type
    `MailboxSettingsOptions` (default-constructed).
  - `src/OpenClaw.HostAdapter/Program.cs` — register `GET /users/{id}/mailboxSettings` (reads
    `IOptions<HostAdapterOptions>`, returns `ApiEnvelope<MailboxSettingsDto>`, no bridge
    readiness check, no process runner) and `GET /users/{id}/calendar/getSchedule` (validates
    the window, fetches calendar events via the existing CLI calendar chain, computes busy
    intervals, returns `ApiEnvelope<FreeBusyScheduleDto>`). Keep `Program.cs` within the
    500-line cap; extract the free/busy projection into a helper if needed.
  - `src/OpenClaw.Core/HostAdapterHttpClient.cs` — implement `GetMailboxSettingsAsync` and
    `GetFreeBusyAsync` by constructing the two Graph-shaped relative paths (sourcing the `{id}`
    segment from `MailboxId`, escaping the `startDateTime`/`endDateTime` values) and calling the
    existing `SendAsync<T>` helper.
  - `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` — remove the
    `NotSupportedException` stubs for `GetMailboxSettingsAsync` and `GetFreeBusyAsync`;
    implement both by delegating to `IHostAdapterClient` (mirroring `GetCalendarViewAsync` and
    `GetEventAsync`), mapping via `SchedulingDtoMapper` as needed; leave `SendMailAsync`
    deferred to #75.
  - `src/OpenClaw.Core/Agent/Contracts/MailboxSettingsDto.cs` and
    `src/OpenClaw.Core/Agent/Contracts/FreeBusyScheduleDto.cs` — delete after relocation (the
    records move to `OpenClaw.HostAdapter.Contracts`).
  - `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` — update the `using` directive to
    reference the relocated DTOs in `OpenClaw.HostAdapter.Contracts`.
  - `src/OpenClaw.Core/Agent/SlotProposer.cs` — update the `using` directive.
  - `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` — update the `using`
    directive.

- Test files to update or add:
  - `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` — replace
    the two `NotSupportedException` expectation tests with tests that set up
    `IHostAdapterClient.GetMailboxSettingsAsync`/`GetFreeBusyAsync` to return success envelopes
    and assert the returned DTOs; mock `IHostAdapterClient`; no real network.
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs` — add tests for the
    `mailboxSettings` and `getSchedule` routes, using `HostAdapterTestWebApplicationFactory`
    (in-memory config for `MailboxSettings`; `IHostAdapterProcessRunner` stub enqueues a
    calendar response for `getSchedule`).
  - `tests/OpenClaw.HostAdapter.Tests/HostAdapterValidationTests.cs` — add window-validation
    coverage for `getSchedule`.
  - `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — assert the two new
    Graph-shaped relative paths and the `ApiEnvelope<T>` round-trips.
  - `tests/OpenClaw.Core.Tests/Agent/SchedulingDtoContractTests.cs` — update the DTO namespace
    and add `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` round-trip
    coverage.
  - `tests/OpenClaw.Core.Tests/Agent/SlotProposerTests.cs`,
    `tests/OpenClaw.Core.Tests/Agent/SlotProposerPropertyTests.cs`,
    `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs` — update the DTO
    `using` directives (no behavioral change; the existing mocks of `ISchedulingService`
    remain valid).
  - Free/busy tests use `FakeTimeProvider` for any "now" derivation, mock the data source, and
    create no temporary files.

- Dependency changes (new/removed packages) and rationale: none.
- Logging/telemetry additions and locations: none beyond the existing request-scoped logging
  in the HostAdapter route lifecycle.
- Rollout plan (feature flags, staged deploys, fallback path): no feature flag. The additive
  routes and methods are inert until the `propose_times` pipeline consumes them; the change is
  gated by the contract/schema compatibility check and the coverage thresholds.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)
