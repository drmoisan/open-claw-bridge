# hostadapter-mailboxsettings-getschedule - Plan

- **Issue:** #74
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-06-13T10-30
- **Status:** Ready for preflight
- **Version:** 1.0
- **Work Mode:** full-feature
- **Design:** Design A (operator-approved, locked). Two additive `IHostAdapterClient` methods, two Graph-shaped HostAdapter GET routes, free/busy computed in the HostAdapter from bridge calendar data via `IHostAdapterProcessRunner` (NOT from `CoreCacheRepository`).

## Required References

- General code change policy: `.claude/rules/general-code-change.md`
- General unit test policy: `.claude/rules/general-unit-test.md`
- C# standards and toolchain: `.claude/rules/csharp.md`
- Architecture boundaries: `.claude/rules/architecture-boundaries.md`
- Module rigor tiers and gate matrix: `.claude/rules/quality-tiers.md`
- Authoritative scope: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/spec.md`
- Acceptance criteria: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/user-story.md`
- Research: `artifacts/research/2026-06-13-issue-74-hostadapter-mailbox-freebusy-research.md`

**All work must comply with these policies; do not duplicate their content here.**

## Design Notes (authoritative for this plan)

- The research artifact recommends an Option B (Core-side computation from `CoreCacheRepository`). That recommendation is superseded. The operator-approved Design A is implemented: free/busy is computed inside `OpenClaw.HostAdapter` from bridge calendar events fetched via `IHostAdapterProcessRunner` (the existing `BuildListCalendar` CLI chain), and is served over the new `GET /users/{id}/calendar/getSchedule` route. `OpenClaw.Core` consumes both new routes over loopback HTTP through `IHostAdapterClient`. `CoreCacheRepository` is not consulted for free/busy.
- `ISchedulingService` interface signatures are unchanged: `GetMailboxSettingsAsync(CancellationToken ct) -> Task<MailboxSettingsDto>` and `GetFreeBusyAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct) -> Task<FreeBusyScheduleDto>`. Only the `HostAdapterSchedulingService` implementations change (stub removal + delegation).
- The new `IHostAdapterClient` methods return envelopes: `GetMailboxSettingsAsync(...) -> Task<ApiEnvelope<MailboxSettingsDto>>` and `GetFreeBusyAsync(startUtc, endUtc, ...) -> Task<ApiEnvelope<FreeBusyScheduleDto>>`.
- The three DTOs (`MailboxSettingsDto`, `FreeBusyScheduleDto`, `BusyIntervalDto`) relocate from the `OpenClaw.Core.Agent` namespace to `OpenClaw.HostAdapter.Contracts` so `IHostAdapterClient` can return them without `OpenClaw.HostAdapter.Contracts` depending on `OpenClaw.Core`.
- `Program.cs` is currently 411 lines. Adding two routes plus the free/busy projection would exceed the 500-line cap, so the free/busy projection (and, if needed, the route registration) is extracted into a dedicated helper file in `OpenClaw.HostAdapter`. Each task that touches `Program.cs` must keep it under 500 lines.

## Toolchain Gate (C#) — applies to every code/test task

Run in this exact order, restarting from step 1 whenever any step fails or rewrites files (restart-on-change loop), until all stages pass in a single uninterrupted pass:

1. **Format** — `dotnet csharpier .` (formatter output wins).
2. **Lint / analyzers** — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` (0 analyzer errors).
3. **Type-check (nullable)** — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` (0 nullable-flow warnings-as-errors).
4. **Architecture verification** — verify the `ProjectReference` graph against `.claude/rules/architecture-boundaries.md` (no new edges; HostAdapter does not reference Core or the COM host; `OpenClaw.HostAdapter.Contracts` depends only on `OpenClaw.MailBridge.Contracts`). Use `NetArchTest.Rules` assertions if/when an `*.ArchitectureTests` project exists; otherwise verify the project graph and record the verification.
5. **Test + coverage** — `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.

Coverage gates (uniform, all tiers): line >= 85%, branch >= 75%; no regression on changed lines.

## Evidence Locations (canonical, non-overridable)

All evidence is written under `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/<kind>/`:

- Baseline: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/`
- QA gates / final QA: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/`
- Regression / contract evidence: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/regression-testing/`
- Other (architecture verification, AC mapping): `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/other/`

Non-canonical paths such as `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, or `artifacts/evidence/` are forbidden for evidence output and must not be used. EVIDENCE_LOCATION_OVERRIDE_REJECTED applies if any caller supplies such a path.

Each command-step evidence artifact MUST include: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Baseline and final-QA test artifacts MUST record numeric coverage values (baseline percent and changed/new-code percent).

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read the policy files in required order and record an evidence artifact at `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/other/phase0-instructions-read.md`.
  - Acceptance: Artifact exists with `Timestamp:`, `Policy Order:`, and the explicit list of files read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`.
- [x] [P0-T2] Capture the baseline format state by running `dotnet csharpier --check .` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/format.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (clean/dirty file count).
- [x] [P0-T3] Capture the baseline build/analyzer state by running `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/build-lint.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts).
- [x] [P0-T4] Capture the baseline nullable type-check state by running `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/typecheck.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P0-T5] Capture the baseline architecture state by verifying the `ProjectReference` graph against `.claude/rules/architecture-boundaries.md` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/architecture.md`.
  - Acceptance: Artifact records the current edges for `OpenClaw.HostAdapter`, `OpenClaw.HostAdapter.Contracts`, and `OpenClaw.Core`, with `Timestamp:`, `Command:` (or inspection method), `EXIT_CODE:`, `Output Summary:` confirming no pre-existing violation.
- [x] [P0-T6] Capture the baseline test + coverage state by running `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/test-coverage.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with numeric baseline line% and branch% headline values and total passing test count.

### Phase 1 — Relocate the three scheduling DTOs to OpenClaw.HostAdapter.Contracts

- [x] [P1-T1] Create `src/OpenClaw.HostAdapter.Contracts/SchedulingContracts.cs` containing the `MailboxSettingsDto`, `FreeBusyScheduleDto`, and `BusyIntervalDto` `sealed record` types in namespace `OpenClaw.HostAdapter.Contracts`, preserving the exact existing field names, types, and order (`MailboxSettingsDto(string TimeZoneId, IReadOnlyList<DayOfWeek> WorkingDays, TimeOnly WorkingHoursStart, TimeOnly WorkingHoursEnd)`; `BusyIntervalDto(DateTimeOffset Start, DateTimeOffset End)`; `FreeBusyScheduleDto(string MailboxUpn, IReadOnlyList<BusyIntervalDto> BusyIntervals)`), with XML docs.
  - Acceptance: File compiles; uses only BCL types (`DayOfWeek`, `TimeOnly`, `DateTimeOffset`, `IReadOnlyList<T>`); introduces no `using` of `OpenClaw.Core`.
- [x] [P1-T2] Delete `src/OpenClaw.Core/Agent/Contracts/MailboxSettingsDto.cs` and `src/OpenClaw.Core/Agent/Contracts/FreeBusyScheduleDto.cs` (the relocated records replace them).
  - Acceptance: Both files are removed; no duplicate record definitions remain in `OpenClaw.Core`.
- [x] [P1-T3] Make the `MailboxSettingsDto` and `FreeBusyScheduleDto` references in `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` resolve to `OpenClaw.HostAdapter.Contracts` by ADDING `using OpenClaw.HostAdapter.Contracts;` (this file is declared in `namespace OpenClaw.Core.Agent;` and currently resolves the DTOs via the shared file-scoped namespace, so there is no existing DTO `using` to update).
  - Acceptance: File compiles; interface signatures are otherwise unchanged.
- [x] [P1-T4] Make the relocated DTO references resolve to `OpenClaw.HostAdapter.Contracts` in the three `OpenClaw.Core.Agent`-namespace source files that reference them: `src/OpenClaw.Core/Agent/SlotProposer.cs`, `src/OpenClaw.Core/Agent/SlotProposer.Window.cs` (references `FreeBusyScheduleDto`), and `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`. For files declared in `namespace OpenClaw.Core.Agent;` (`SlotProposer.cs`, `SlotProposer.Window.cs`), ADD `using OpenClaw.HostAdapter.Contracts;` (there is no existing DTO `using` to replace, because they currently resolve the DTOs via the shared file-scoped namespace). For `SchedulingWorker.Pipeline.cs` (namespace `OpenClaw.Core.Agent.Runtime`, which has `using OpenClaw.Core.Agent;`), ADD `using OpenClaw.HostAdapter.Contracts;` and RETAIN the existing `using OpenClaw.Core.Agent;`.
  - Acceptance: All three files compile with no behavioral change; `SlotProposer.Window.cs` is included; no `using OpenClaw.Core.Agent;` directive that is still required for other `OpenClaw.Core.Agent` types is removed.
- [x] [P1-T5] Update the `using` directives in `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` so the relocated DTO references resolve to `OpenClaw.HostAdapter.Contracts` (implementation changes are made later in Phase 6; this task only makes the existing stub file compile against the relocated types).
  - Acceptance: File compiles; the three `NotSupportedException` stubs are still present and unchanged at this point; the existing `using OpenClaw.Core.Agent;` is retained (it is still required for other `OpenClaw.Core.Agent` types).
- [x] [P1-T6] Update the `using` directives in the affected test files so the relocated DTO references resolve to `OpenClaw.HostAdapter.Contracts`: `tests/OpenClaw.Core.Tests/Agent/SchedulingDtoContractTests.cs`, `tests/OpenClaw.Core.Tests/Agent/SlotProposerTests.cs`, `tests/OpenClaw.Core.Tests/Agent/SlotProposerPropertyTests.cs`, `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerTests.cs`, and `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs`.
  - Acceptance: All listed test files compile against the relocated DTO namespace; no test assertion is changed in this task; for each test file the existing `using OpenClaw.Core.Agent;` is retained and `using OpenClaw.HostAdapter.Contracts;` is added (the test files reference both `OpenClaw.Core.Agent` types and the relocated DTOs). For files that already import `OpenClaw.HostAdapter.Contracts` (e.g. HostAdapterSchedulingServiceTests.cs), the using addition is a no-op and acceptable.
- [x] [P1-T7] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) with restart-on-change semantics and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/phase1-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:` per stage, `EXIT_CODE:`, and `Output Summary:` confirming the solution still builds and all existing tests pass after the relocation; `OpenClaw.HostAdapter.Contracts` still depends only on `OpenClaw.MailBridge.Contracts`.

### Phase 2 — Add the two additive IHostAdapterClient methods

- [x] [P2-T1] Add `Task<ApiEnvelope<MailboxSettingsDto>> GetMailboxSettingsAsync(string? requestId = null, CancellationToken cancellationToken = default)` to `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` with XML docs describing `GET /users/{id}/mailboxSettings` and its config-sourced semantics.
  - Acceptance: Interface compiles; method is additive (no existing member changed).
- [x] [P2-T2] Add `Task<ApiEnvelope<FreeBusyScheduleDto>> GetFreeBusyAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, string? requestId = null, CancellationToken cancellationToken = default)` to `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` with XML docs describing `GET /users/{id}/calendar/getSchedule?startDateTime={iso8601}&endDateTime={iso8601}` and the window-as-typed-parameters portability boundary (D2).
  - Acceptance: Interface compiles; method is additive.
- [x] [P2-T3] Build the solution to surface every implementer/mock that now lacks the two members and record the list in `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/other/phase2-implementers.md`.
  - Acceptance: Artifact enumerates `HostAdapterHttpClient` (implemented in Phase 5) and any `Mock<IHostAdapterClient>` usages that must add the members; includes `Timestamp:`, `Command:`, `EXIT_CODE:` (expected non-zero until Phase 5), `Output Summary:`. This artifact is documentation-only; the build is not required to pass at this task.

### Phase 3 — Add MailboxSettingsOptions and wire onto HostAdapterOptions

- [x] [P3-T1] Create `src/OpenClaw.HostAdapter/MailboxSettingsOptions.cs` as a `sealed class` POCO with `TimeZoneId` (default `"UTC"`), `WorkingDaysOfWeek` (`string[]`, default `["Monday","Tuesday","Wednesday","Thursday","Friday"]`), `WorkingHoursStart` (`string`, default `"09:00"`), `WorkingHoursEnd` (`string`, default `"17:00"`), with XML docs and the documented defaults.
  - Acceptance: File compiles; default-constructed instance yields the spec defaults.
- [x] [P3-T2] Add a `public MailboxSettingsOptions MailboxSettings { get; set; } = new();` property to `src/OpenClaw.HostAdapter/HostAdapterOptions.cs` bound under `OpenClaw:HostAdapter:MailboxSettings`.
  - Acceptance: `HostAdapterOptions` compiles; configuration binding resolves the new subsection; existing properties unchanged.
- [x] [P3-T3] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/phase3-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

### Phase 4 — Add the two HostAdapter routes and the free/busy projection helper

- [x] [P4-T1] Create `src/OpenClaw.HostAdapter/FreeBusyProjection.cs` containing a pure static projection that maps a fetched `IReadOnlyList<EventDto>` to `FreeBusyScheduleDto` per decision D1: an event contributes a `BusyIntervalDto(StartUtc, EndUtc)` when `BusyStatus != 0` (free), and a null `BusyStatus` is treated as busy (conservative default); the result preserves the supplied mailbox identifier as `MailboxUpn`.
  - Acceptance: File compiles; the projection has no wall-clock dependency and is deterministic for a fixed event list; file stays under 500 lines.
- [x] [P4-T2] Register `GET /users/{id}/mailboxSettings` in `src/OpenClaw.HostAdapter/Program.cs`: the handler reads `IOptions<HostAdapterOptions>`, parses `MailboxSettings` (parsing `WorkingDaysOfWeek` to `DayOfWeek`, `WorkingHoursStart`/`WorkingHoursEnd` to `TimeOnly`), constructs `MailboxSettingsDto`, and returns `ApiEnvelope<MailboxSettingsDto>` via `HostAdapterResponses.Success` and `ToHttpResult`. The handler does NOT call `RequireReadyBridgeAsync` and does NOT invoke `IHostAdapterProcessRunner`.
  - Acceptance: Route compiles and returns the configured/default mailbox settings; on malformed config it returns `HostAdapterResponses.ConfigurationError`; `Program.cs` stays under 500 lines.
- [x] [P4-T3] Register `GET /users/{id}/calendar/getSchedule` in `src/OpenClaw.HostAdapter/Program.cs`: the handler calls `RequireReadyBridgeAsync`, validates `startDateTime`/`endDateTime` via `TryGetUtcTimestamp` + `TryValidateWindow` (reusing the `calendarView` pattern), applies the `$top`/`MaxLimit` ceiling, fetches events via `processRunner.ExecuteAsync` with `commandBuilder.BuildListCalendar(startUtc, endUtc, limit)`, applies `FreeBusyProjection`, and returns `ApiEnvelope<FreeBusyScheduleDto>` via `ToHttpResult`. An empty window yields an empty `BusyIntervals` list (not an error).
  - Acceptance: Route compiles; `start >= end` is rejected by `TryValidateWindow` with the existing invalid-request status and error code; `Program.cs` stays under 500 lines (extract route-registration helpers into a separate file if needed to remain under the cap).
- [x] [P4-T4] Add HostAdapter route tests for the success and config paths of `mailboxSettings` to `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs` using `HostAdapterTestWebApplicationFactory`: configure in-memory `MailboxSettings` keys and assert the returned `ApiEnvelope<MailboxSettingsDto>` matches the configured values; add a default-config test asserting UTC / Mon-Fri / 09:00-17:00. No `IHostAdapterProcessRunner` enqueue is needed for this route.
  - Acceptance: Tests pass; no temporary files; no real network.
- [x] [P4-T5] Add HostAdapter route tests for `getSchedule` to `tests/OpenClaw.HostAdapter.Tests/HostAdapterEndpointTests.cs`: enqueue a status response and a calendar-list response on the `IHostAdapterProcessRunner` stub, then assert the `ApiEnvelope<FreeBusyScheduleDto>` `BusyIntervals` reflect only events with `BusyStatus != 0` and that a null `BusyStatus` event is treated as busy; add an empty-window test asserting an empty `BusyIntervals` list.
  - Acceptance: Tests pass; the stub (not real COM/network) supplies events; no temporary files.
- [x] [P4-T6] Add window-validation coverage for `getSchedule` to `tests/OpenClaw.HostAdapter.Tests/HostAdapterValidationTests.cs`: a `start >= end` request and a missing/malformed `startDateTime`/`endDateTime` request are rejected with the existing invalid-request status and error codes.
  - Acceptance: Validation tests pass and assert the documented status and error codes.
- [x] [P4-T7] Add unit tests for `FreeBusyProjection` to a new file `tests/OpenClaw.HostAdapter.Tests/FreeBusyProjectionTests.cs` covering: busy event (`BusyStatus = 2`) projected; tentative (`1`) and outOfOffice (`3`) projected as busy; free (`0`) excluded; null `BusyStatus` treated as busy; empty input yields empty intervals.
  - Acceptance: Tests pass and assert deterministic output for fixed inputs; no wall-clock, no `Thread.Sleep`/`Task.Delay`, no temporary files.
- [x] [P4-T8] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/phase4-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including line%/branch% for the changed HostAdapter code (line >= 85%, branch >= 75%); confirm `Program.cs` and all new files are under 500 lines.

### Phase 5 — Implement HostAdapterHttpClient client methods

- [x] [P5-T1] Implement `GetMailboxSettingsAsync` in `src/OpenClaw.Core/HostAdapterHttpClient.cs` by constructing the relative path `users/{id}/mailboxSettings` (sourcing `{id}` from `options.HostAdapter.MailboxId` via `Uri.EscapeDataString`) and calling the existing `SendAsync<MailboxSettingsDto>` helper.
  - Acceptance: Method compiles and returns `ApiEnvelope<MailboxSettingsDto>`; matches the existing client method style.
- [x] [P5-T2] Implement `GetFreeBusyAsync` in `src/OpenClaw.Core/HostAdapterHttpClient.cs` by constructing the relative path `users/{id}/calendar/getSchedule?startDateTime={...}&endDateTime={...}` (escaping the `startUtc`/`endUtc` "O"-format values and the `{id}` segment) and calling `SendAsync<FreeBusyScheduleDto>`.
  - Acceptance: Method compiles and returns `ApiEnvelope<FreeBusyScheduleDto>`; the wire request is the single seam that would change for the PI-1 Graph POST form (D2), with the method signature unchanged.
- [x] [P5-T3] Add `HostAdapterHttpClient` tests to `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` asserting the two new Graph-shaped relative paths are constructed exactly as specified and that the `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` responses deserialize correctly, using the existing mocked transport (no real network).
  - Acceptance: Tests assert the exact relative path strings and round-trip the envelope payloads; no temporary files.
- [x] [P5-T4] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/phase5-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including changed-code line%/branch%.

### Phase 6 — Implement the two HostAdapterSchedulingService methods by delegation

- [x] [P6-T1] Replace the `GetMailboxSettingsAsync(CancellationToken ct)` `NotSupportedException` stub in `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` with a delegation that calls `hostAdapterClient.GetMailboxSettingsAsync(cancellationToken: ct)` and returns `envelope.Data` when `envelope is { Ok: true, Data: not null }`, otherwise returns the documented default `MailboxSettingsDto` (UTC, Mon-Fri, 09:00-17:00) so the deterministic pipeline degrades gracefully (consistent with `GetCalendarViewAsync`).
  - Acceptance: Method compiles; the `NotSupportedException` for mailbox settings is removed; failure path returns the documented default rather than throwing.
- [x] [P6-T2] Replace the `GetFreeBusyAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct)` `NotSupportedException` stub in `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` with a delegation that calls `hostAdapterClient.GetFreeBusyAsync(start, end, cancellationToken: ct)` and returns `envelope.Data` when `envelope is { Ok: true, Data: not null }`, otherwise returns an empty-`BusyIntervals` `FreeBusyScheduleDto` (graceful degradation).
  - Acceptance: Method compiles; the `NotSupportedException` for free/busy is removed; the `SendMailAsync` `NotSupportedException` stub remains unchanged (deferred to #75).
- [x] [P6-T3] Replace the two `NotSupportedException` expectation tests in `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` with delegation tests: set up `Mock<IHostAdapterClient>.GetMailboxSettingsAsync` to return a success `ApiEnvelope<MailboxSettingsDto>` and assert the returned DTO; set up `GetFreeBusyAsync` to return a success `ApiEnvelope<FreeBusyScheduleDto>` and assert the returned `BusyIntervals`. Add failure-path tests asserting the documented defaults (mailbox) and empty intervals (free/busy) when the envelope is not `{ Ok: true, Data: not null }`. Retain a test asserting `SendMailAsync` still throws `NotSupportedException`.
  - Acceptance: Tests pass; `IHostAdapterClient` is mocked (no real network); window timestamps for the free/busy test are supplied via `FakeTimeProvider`-derived values (no wall-clock); no temporary files.
- [x] [P6-T4] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/phase6-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including changed-code line%/branch%; confirms only `GetMailboxSettingsAsync`/`GetFreeBusyAsync` stubs were removed and `SendMailAsync` remains a stub.

### Phase 7 — Contract / schema round-trip and cross-surface regression tests

- [x] [P7-T1] Extend `tests/OpenClaw.Core.Tests/Agent/SchedulingDtoContractTests.cs` with a JSON round-trip test for `ApiEnvelope<MailboxSettingsDto>` asserting the outer envelope (`Ok`, `Data`, `Meta`, `Error`) and the inner `MailboxSettingsDto` fields are preserved.
  - Acceptance: Test passes; uses `System.Text.Json` round-trip and `BeEquivalentTo` per the file's existing collection-comparison convention.
- [x] [P7-T2] Extend `tests/OpenClaw.Core.Tests/Agent/SchedulingDtoContractTests.cs` with a JSON round-trip test for `ApiEnvelope<FreeBusyScheduleDto>` asserting the outer envelope and the inner `FreeBusyScheduleDto` (including `BusyIntervals`) are preserved.
  - Acceptance: Test passes; the relocated DTO namespace is referenced.
- [x] [P7-T3] Verify the `SlotProposer` and `SchedulingWorker` tests still pass unchanged against the relocated DTOs and the now-implemented scheduling methods, and confirm the `propose_times` pipeline path consumes `GetMailboxSettingsAsync`/`GetFreeBusyAsync` (the `SchedulingWorkerTests` already mock and verify these calls). Record the result in `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/regression-testing/pipeline-consumption.md`.
  - Acceptance: Artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` confirming the pipeline tests pass and exercise both methods.
- [x] [P7-T4] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/phase7-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including changed-code line%/branch%.

### Phase 8 — Documentation

- [x] [P8-T1] Add `GET /users/{id}/mailboxSettings` and `GET /users/{id}/calendar/getSchedule?startDateTime=<utc>&endDateTime=<utc>` to the HostAdapter route table in `docs/api-reference.md`, including their `ApiEnvelope<MailboxSettingsDto>` / `ApiEnvelope<FreeBusyScheduleDto>` response shapes and the config-sourced vs. CLI-fetched distinction, and document the new `OpenClaw:HostAdapter:MailboxSettings` configuration subsection and its defaults.
  - Acceptance: `docs/api-reference.md` lists both routes consistent with the existing table format and documents the config keys and defaults.
- [x] [P8-T2] Update `README.md` HostAdapter surface description to mention the two new Graph-shaped routes.
  - Acceptance: `README.md` references the `mailboxSettings` and `getSchedule` routes; both docs files remain Markdown (exempt from the 500-line cap).

### Phase 9 — Final QA Loop and Acceptance-Criteria Verification

- [x] [P9-T1] Run formatting `dotnet csharpier .` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-format.md`.
  - Acceptance: `EXIT_CODE: 0`; artifact includes `Timestamp:`, `Command:`, `Output Summary:`; if files were rewritten, restart the loop from this step.
- [x] [P9-T2] Run lint/analyzers `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-lint.md`.
  - Acceptance: `EXIT_CODE: 0`, 0 analyzer errors; artifact includes `Timestamp:`, `Command:`, `Output Summary:`.
- [x] [P9-T3] Run nullable type-check `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-typecheck.md`.
  - Acceptance: `EXIT_CODE: 0`, 0 nullable warnings-as-errors; artifact includes `Timestamp:`, `Command:`, `Output Summary:`.
- [x] [P9-T4] Verify architecture boundaries against `.claude/rules/architecture-boundaries.md` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-architecture.md`.
  - Acceptance: Artifact confirms no new `ProjectReference` edge; `OpenClaw.HostAdapter.Contracts` depends only on `OpenClaw.MailBridge.Contracts`; `OpenClaw.HostAdapter` does not reference `OpenClaw.Core` or the COM host; `OpenClaw.Core` depends only on `OpenClaw.HostAdapter.Contracts`; includes `Timestamp:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P9-T5] Run tests with coverage `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-test-coverage.md`.
  - Acceptance: `EXIT_CODE: 0`, all tests pass; artifact records numeric post-change line% and branch% and changed/new-code line%/branch%; line >= 85%, branch >= 75%. If any step in P9 rewrote files or failed, restart the loop from P9-T1.
- [x] [P9-T6] Verify coverage delta and no-regression-on-changed-lines by comparing the baseline (P0-T6) and final (P9-T5) numbers and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/coverage-delta.md`.
  - Acceptance: Artifact reports baseline line%/branch%, post-change line%/branch%, and new/changed-code line%/branch%; confirms no regression on changed lines and that thresholds (line >= 85%, branch >= 75%) hold. If any required coverage value is unavailable, the outcome is remediation-required (not PASS).
- [x] [P9-T7] Verify each of the seven acceptance criteria in `user-story.md` maps to implementing tasks/tests and write `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/other/acceptance-criteria-map.md`.
  - Acceptance: Artifact contains the mapping below, each row citing the satisfying task IDs and test files, with `Timestamp:` and an overall PASS/REMEDIATION-REQUIRED verdict:
    - AC1 (mailboxSettings returns valid TZ + working hours from config, defaults UTC/Mon-Fri/09:00-17:00): P3-T1, P3-T2, P4-T2, P4-T4.
    - AC2 (getSchedule returns deterministic free/busy from CLI-fetched calendar data, computed via injected `TimeProvider`, never wall-clock): P4-T1, P4-T3, P4-T5, P4-T7, P6-T3.
    - AC3 (`IHostAdapterClient` and `HostAdapterHttpClient` expose both new methods returning the two envelopes; additive): P2-T1, P2-T2, P5-T1, P5-T2, P5-T3.
    - AC4 (`HostAdapterSchedulingService` two methods implemented by delegation; their stubs removed; `SendMailAsync` stub remains; pipeline consumes them): P6-T1, P6-T2, P6-T3, P7-T3.
    - AC5 (three DTOs relocated to `OpenClaw.HostAdapter.Contracts`; references updated; contract round-trip tests pass): P1-T1..P1-T7, P7-T1, P7-T2.
    - AC6 (free/busy uses `TimeProvider`; tests use `FakeTimeProvider` and mock the data source `IHostAdapterProcessRunner` on the HostAdapter side and `IHostAdapterClient` on the Core side; no temp files; no real Outlook/network): P4-T5, P4-T7, P6-T3.
    - AC7 (line >= 85%, branch >= 75% on changed code; no regression on changed lines): P9-T5, P9-T6.

## Test Plan

- Unit: `FreeBusyProjection` (HostAdapter), `HostAdapterSchedulingService` delegation (Core), `HostAdapterHttpClient` path construction (Core), `MailboxSettingsOptions` parsing via route tests.
- Integration / endpoint: `HostAdapterEndpointTests` (mailboxSettings and getSchedule routes via `HostAdapterTestWebApplicationFactory` with `IHostAdapterProcessRunner` stub), `HostAdapterValidationTests` (getSchedule window validation).
- Contract / schema: `SchedulingDtoContractTests` extended with `ApiEnvelope<MailboxSettingsDto>` and `ApiEnvelope<FreeBusyScheduleDto>` JSON round-trips.
- Determinism: free/busy tests use `FakeTimeProvider`-derived window values; no wall-clock, no `Thread.Sleep`/`Task.Delay`/`Start-Sleep`, no temporary files; data sources mocked (`IHostAdapterProcessRunner` HostAdapter side, `IHostAdapterClient` Core side).
- Coverage evidence:
  - Baseline: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/baseline/test-coverage.md`
  - Post-change: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/final-test-coverage.md`
  - Comparison: `docs/features/active/hostadapter-mailboxsettings-getschedule-74/evidence/qa-gates/coverage-delta.md`

## Open Questions / Notes

- The research artifact's Option B (Core-side `CoreCacheRepository` computation) is intentionally not implemented; Design A (HostAdapter-side computation via `IHostAdapterProcessRunner`, two HTTP routes) is the locked, operator-approved design and is the only design this plan implements.
- D1 busy-status rule: `BusyStatus != 0` is busy; null is treated as busy (conservative). Tentative (1) and outOfOffice (3) are therefore treated as busy in the Stage-0 grid.
- No new package dependencies and no new `ProjectReference` edges are introduced.
