# graph-backed-adapter - Plan

- **Issue:** #115
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T19-38
- **Status:** Ready for Preflight
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata; `spec.md` and `user-story.md` both present and authoritative)

## Required References

Policy reading order (per `.claude/skills/policy-compliance-order/SKILL.md`):

1. `CLAUDE.md` / auto-loaded `.claude/rules/` standing instructions
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/quality-tiers.md`
7. `.github/instructions/csharp-code-change.instructions.md` and `.github/instructions/csharp-unit-test.instructions.md`

**All work must comply with these policies; do not duplicate their content here.**

Authoritative feature documents:

- `docs/features/active/2026-07-02-graph-backed-adapter-115/issue.md` (6 AC)
- `docs/features/active/2026-07-02-graph-backed-adapter-115/spec.md` (design decisions D1-D12; endpoint, error-matrix, and mapping tables)
- `docs/features/active/2026-07-02-graph-backed-adapter-115/user-story.md`

## Global Constraints (apply to every task)

- **Diff scope (confined):** `src/OpenClaw.Core/CloudGraph/**` (all new), `src/OpenClaw.Core/Program.cs` (one backend-selection conditional block only), `tests/OpenClaw.Core.Tests/CloudGraph/**` (all new), and this feature folder. Zero changes to `src/OpenClaw.Core/Agent/**`, `src/OpenClaw.HostAdapter/**`, `src/OpenClaw.HostAdapter.Contracts/**`, `src/OpenClaw.MailBridge*/**`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`, `docker-compose*`.
- **Toolchain loop (per phase batch):** `csharpier format .` then `csharpier check .` (global tool 1.3.0; not `dotnet csharpier`), `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Restart the loop from formatting if any step fails or changes files; a phase is complete only when all steps pass in a single pass.
- **Determinism:** no live Graph calls; all HTTP through mocked `HttpMessageHandler` (reuse the `FakeHttpHandler` pattern, `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs:606`); all delays through the injected `TimeProvider` via the `Task.Delay(delay, timeProvider, ct)` overload; tests advance `FakeTimeProvider` only; no temp files — recorded Graph payloads are in-repo raw-string constants in the test assembly.
- **File size:** every new production and test file <= 500 lines.
- **Test stack:** MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` + NetArchTest (repo's actual stack; `InternalsVisibleTo("OpenClaw.Core.Tests")` allows `internal` production types in tests).
- **Evidence:** all evidence artifacts under `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/<kind>/` (canonical scheme; non-canonical `artifacts/` evidence paths are prohibited). Raw command intermediates (TRX, coverage XML) go to `artifacts/csharp/`. `<ts>` in artifact names is the ISO-8601 `yyyy-MM-ddTHH-mm` execution timestamp.
- **Interface-declaration sequencing:** `GraphHostAdapterClient` is authored as a partial class *without* the `: IHostAdapterClient` declaration until all nine members exist (Phase 5), so every phase batch builds green. Phase tests call the concrete methods directly; Phase 5 adds the declaration and the compiler proves completeness.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture and Policy Compliance

- [x] [P0-T1] Read the policy documents in the Required References order (items 1-7) and write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read
  - Acceptance: artifact exists with all three fields populated before any Phase 1 work begins
- [x] [P0-T2] Verify full-feature document preconditions: `issue.md` contains `- Work Mode: full-feature` and an explicit `## Acceptance Criteria` section, and `spec.md` + `user-story.md` exist in `docs/features/active/2026-07-02-graph-backed-adapter-115/`; record the check in `evidence/baseline/phase0-instructions-read.md` (append a `Mode Verification:` section)
  - Acceptance: appended section names the three files, the mode marker value, and a pass/fail verdict; fail closed (stop and report) on any mismatch
- [x] [P0-T3] Capture the C# formatting baseline: run `csharpier check .` from repo root and write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/csharp-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists with all four fields; `Output Summary:` states pass/fail and any offending file count
- [x] [P0-T4] Capture the C# build/analyzer/nullable baseline: run `dotnet build OpenClaw.MailBridge.sln` and write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/csharp-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists with all four fields; `Output Summary:` records warning/error counts (expected 0/0 on clean baseline)
- [x] [P0-T5] Capture the C# test and coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`, copy raw TRX/coverage output under `artifacts/csharp/`, and write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/baseline/csharp-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric baseline line-coverage percent and branch-coverage percent and total pass/fail test counts
  - Acceptance: artifact exists with all four fields and numeric coverage headline values (no placeholders); raw intermediates present under `artifacts/csharp/`

### Phase 1 — GraphAdapterOptions, Validator, and Architecture Boundary Suite

- [x] [P1-T1] Create `src/OpenClaw.Core/CloudGraph/GraphAdapterOptions.cs`: options bag for section `OpenClaw:GraphAdapter` with the eleven keys and defaults from spec "Inputs / Outputs" (`Enabled=false`, `PrincipalMailboxUpn=""`, `AssistantMailboxUpn=""`, `BaseUrl="https://graph.microsoft.com/v1.0/"`, `PreferredTimeZone="UTC"`, `PageSize=50`, `MaxPages=10`, `MaxAttempts=4`, `BaseDelaySeconds=1`, `MaxDelaySeconds=30`, `AvailabilityViewIntervalMinutes=30`), XML docs, mirroring `src/OpenClaw.Core/CloudAuth/CloudAuthOptions.cs` conventions
  - Acceptance: file compiles, <= 500 lines, namespace `OpenClaw.Core.CloudGraph`
- [x] [P1-T2] Create `src/OpenClaw.Core/CloudGraph/GraphAdapterOptionsValidator.cs`: pure static validator returning a list of error strings, enforcing (only when `Enabled == true`) non-whitespace UPNs, absolute https `BaseUrl`, `PageSize` 1-1000, `MaxPages >= 1`, `MaxAttempts` 1-10, `BaseDelaySeconds > 0`, `MaxDelaySeconds >= BaseDelaySeconds`, `AvailabilityViewIntervalMinutes` 5-1440; mirrors `src/OpenClaw.Core/CloudAuth/CloudAuthOptionsValidator.cs`
  - Acceptance: file compiles, <= 500 lines; `Enabled == false` always yields zero errors
- [x] [P1-T3] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphAdapterOptionsValidatorTests.cs`: MSTest cases for disabled-passes-regardless, enabled-with-defaults fails on empty UPNs, each bound violation produces exactly one named error (both UPNs, non-https/relative BaseUrl, each numeric bound low/high edge), and a fully valid enabled configuration passes
  - Acceptance: all new tests pass; every validator rule has at least one negative case
- [x] [P1-T4] Create `tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphArchitectureBoundaryTests.cs` implementing the three D12 rules: (1) `OpenClaw.Core.CloudGraph` depends on no `OpenClaw.MailBridge.*` namespace except `OpenClaw.MailBridge.Contracts` (dependency-inspection technique from `tests/OpenClaw.Core.Tests/Agent/AgentArchitectureBoundaryTests.cs` `DeterministicSurface_DoesNotDependOnHostAdapterHostImplementation`, line 77); (2) `OpenClaw.Core.CloudGraph` has no dependency on `Microsoft.Office.Interop.Outlook` or `System.Runtime.InteropServices`; (3) `OpenClaw.Core.Agent` (including `Runtime`) has no dependency on `OpenClaw.Core.CloudGraph`
  - Acceptance: all three tests pass against the Phase 1 types and remain part of every subsequent phase's test run
- [x] [P1-T5] Run the mandatory C# toolchain loop for the Phase 1 batch (`csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`), restarting from formatting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 2 — Request Pipeline: Wire Models, Executor, Auth, Retry/Backoff, Error Matrix

- [x] [P2-T1] Create `src/OpenClaw.Core/CloudGraph/GraphWireModels.cs`: internal `System.Text.Json` records deserializing only the `$select`-listed Graph fields (D1/D4) — OData list page (`value`, `@odata.nextLink`), Graph error body (`error.code`, `error.message`), message record (spec MessageDto `$select` list + `@odata.type` + `meetingMessageType`), event record (spec EventDto `$select` list), mailboxSettings record (`timeZone`, `workingHours`), and getSchedule response record (`value[].scheduleItems[].status/start/end`)
  - Acceptance: file compiles, <= 500 lines, all records `internal`
- [x] [P2-T2] Create `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs`: internal shared pipeline taking an `HttpRequestMessage` factory (request rebuilt per attempt), `IAppTokenProvider`, `TimeProvider`, `GraphAdapterOptions`, `ILogger` — per attempt sets `Authorization: Bearer` from `IAppTokenProvider.GetTokenAsync(ct)` and `client-request-id` (caller-supplied requestId when non-blank, else `Guid.NewGuid().ToString()`); retries only 429/502/503/504 up to `MaxAttempts` with `Retry-After` (delta-seconds or HTTP-date evaluated against `timeProvider.GetUtcNow()`) taking precedence over exponential fallback `BaseDelay * 2^(attempt-1)` capped at `MaxDelay`, delays via `Task.Delay(delay, timeProvider, ct)`; maps failures per the full D5 matrix (incl. additive `THROTTLED` on 429 exhaustion, `TRANSPORT_FAILURE` retryable on 502/503/504 exhaustion and network exceptions, `CONFIGURATION_ERROR` on `TokenAcquisitionException`, Graph `error.code` passthrough into `ApiError.BridgeErrorCode`); synthesizes envelopes with `ApiMeta(requestId, "cloudgraph", null)`; logs `warning` on retries and `error` on terminal failures, never tokens or bodies. If the file approaches 500 lines, extract the D5 mapping into `src/OpenClaw.Core/CloudGraph/GraphErrorMapper.cs` (pure static)
  - Acceptance: compiles; each source file <= 500 lines; no banned APIs (no `Task.Delay` without `TimeProvider`, no `DateTime.UtcNow`)
- [x] [P2-T3] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorTests.cs`: handler-level tests (Moq or `FakeHttpHandler` pattern) verifying `Authorization: Bearer` value sourced from a mocked `IAppTokenProvider` on every attempt, `client-request-id` present and echoed into `ApiMeta.RequestId`, generated requestId when caller passes null/blank, request factory invoked once per attempt (fresh `HttpRequestMessage` each attempt), success envelope shape `(true, data, ApiMeta(requestId, "cloudgraph", null), null)`, and 2xx-with-unparseable-body -> `TRANSPORT_FAILURE` with `Retryable = false`
  - Acceptance: all new tests pass with no wall-clock waits
- [x] [P2-T4] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorRetryTests.cs` using `FakeTimeProvider` exclusively: `Retry-After` delta-seconds honored exactly; `Retry-After` HTTP-date honored relative to `FakeTimeProvider.GetUtcNow()`; `Retry-After` takes precedence over exponential fallback; exponential fallback yields 1s/2s/4s with default options; delay capped at `MaxDelaySeconds`; 429-then-success recovers with success envelope; 429 exhaustion after `MaxAttempts` returns failure envelope with `Code = "THROTTLED"`, `Retryable = true`, requestId in `ApiMeta`, attempt count in message; 502/503/504 exhaustion returns `TRANSPORT_FAILURE` with `Retryable = true`; time advances only via `FakeTimeProvider.Advance`
  - Acceptance: all new tests pass; total test wall time confirms no real sleeps (suite remains fast)
- [x] [P2-T5] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphRequestExecutorErrorMatrixTests.cs` covering every remaining D5 row: `TokenAcquisitionException` -> `CONFIGURATION_ERROR` not retryable; 400 -> `INVALID_REQUEST`; 401 and 403 -> `UNAUTHORIZED`; 404 -> `NOT_FOUND`; 500 -> `INTERNAL_ERROR`; `HttpRequestException` -> `TRANSPORT_FAILURE` retryable; unexpected status (e.g., 418) -> `INTERNAL_ERROR`; Graph `error.code` string (e.g., `ErrorItemNotFound`) preserved in `ApiError.BridgeErrorCode`
  - Acceptance: one test per matrix row, all passing
- [x] [P2-T6] Run the mandatory C# toolchain loop for the Phase 2 batch (same four commands as P1-T5), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 3 — Read Endpoints: Messages, CalendarView, Event (Requests + Mapping)

- [x] [P3-T1] Create `src/OpenClaw.Core/CloudGraph/GraphMessageMapper.cs`: pure static mapper from the internal Graph message wire record to `MessageDto` per the spec "Data & State" MessageDto table — importance/sensitivity/meetingMessageType string-to-int maps (inverses of `SchedulingDtoMapper`), `ItemKind` from `@odata.type`, `Unread = !isRead`, OR-5 recipient JSON (`[{"name":"...","email":"..."}]`) for `ToJson`/`CcJson`, `ProtectedFieldsAvailable=true`/`IsRedacted=false`, `MessageClass=null`; missing required `id` fails fast (throws or returns an error signal the client maps to `INTERNAL_ERROR`); missing optional fields map to `null` deterministically
  - Acceptance: compiles, <= 500 lines, no I/O and no mutation
- [x] [P3-T2] Create `src/OpenClaw.Core/CloudGraph/GraphEventMapper.cs`: pure static mapper from the internal Graph event wire record to `EventDto` per the spec EventDto table — `start`/`end` dateTime+timeZone to UTC, `showAs` and `responseStatus.response` int maps, sensitivity map (`private` -> 2), `IsRecurring = type != "singleInstance"`, attendee partitioning by `type` (`required`/`optional`/`resource`) into OR-5 JSON, `ICalUId`/`SeriesMasterId`/`LastModifiedDateTime`/`Categories`/`IsOrganizer`/`IsOnlineMeeting`/`AllowNewTimeProposals`/`BodyFull` from `body.content`, `GlobalAppointmentId=null`, `MeetingStatus=null`, `SensitivityLabel=null`; missing required `id`/`start`/`end` fails fast
  - Acceptance: compiles, <= 500 lines, no I/O and no mutation
- [x] [P3-T3] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphPayloadFixtures.cs`: recorded Graph-v1.0-shaped JSON payloads as `internal const string` raw-string literals — message list page 1 with `@odata.nextLink` + page 2 without, an `#microsoft.graph.eventMessage` message with `meetingMessageType`, a plain mail message with all optional fields absent, an event with `sensitivity: "private"`, `seriesMasterId`, `type: "occurrence"`, and attendees of all three types, a `singleInstance` event, mailboxSettings with `timeZone` + `workingHours`, a getSchedule response containing `busy`/`oof`/`tentative`/`free`/`workingElsewhere` scheduleItems, and Graph error bodies (`ErrorItemNotFound`, `TooManyRequests`); no file I/O, no temp files
  - Acceptance: compiles, <= 500 lines (split into `GraphPayloadFixtures.Events.cs` partial if needed, each <= 500 lines)
- [x] [P3-T4] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphMessageMapperTests.cs`: recorded-payload mapping tests asserting every parity-minimum-set `MessageDto` field (bold rows of the spec table) plus `Sensitivity`, `Unread`, `HasAttachments`, `ItemKind`; the full `meetingMessageType` vocabulary (`meetingRequest`->0 ... `none`/absent->null); missing-optional-field defaults from the sparse fixture; missing `id` fail-fast behavior
  - Acceptance: every field row of the spec MessageDto table has an assertion; all tests pass
- [x] [P3-T5] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphMessageMapperPropertyTests.cs`: CsCheck properties — importance/sensitivity/meetingMessageType mappings round-trip against `SchedulingDtoMapper`'s inverse maps (`src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`), and generated recipient lists serialized to OR-5 JSON survive `SchedulingDtoMapper.ParseAttendees` round-trip
  - Acceptance: at least one property test per pure mapping function in `GraphMessageMapper`; failing seeds reproducible (CsCheck default reporting)
- [x] [P3-T6] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphEventMapperTests.cs`: recorded-payload mapping tests asserting every parity-minimum-set `EventDto` field including `Sensitivity` `private` -> 2, `ICalUId`, `SeriesMasterId`, `IsRecurring` for `occurrence` vs `singleInstance`, attendee-type partitioning including `resource`, `Categories`, boolean flags, `LastModifiedDateTime`, `BodyFull`; missing `start`/`end`/`id` fail-fast behavior
  - Acceptance: every field row of the spec EventDto table has an assertion; all tests pass
- [x] [P3-T7] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphEventMapperPropertyTests.cs`: CsCheck properties — `showAs`, `responseStatus`, and sensitivity enum maps round-trip; generated attendee lists partitioned by type produce OR-5 JSON that survives `ParseAttendees` round-trip with no cross-partition leakage
  - Acceptance: at least one property test per pure mapping function in `GraphEventMapper`; all pass
- [x] [P3-T8] Create `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.cs`: `internal sealed partial class GraphHostAdapterClient` (no `IHostAdapterClient` declaration yet — added in P5-T2) with constructor injecting `HttpClient`, `IOptions<GraphAdapterOptions>`, `IAppTokenProvider`, `TimeProvider`, `ILogger<GraphHostAdapterClient>`, shared URL/`$select` composition helpers, Prefer-header application (`Prefer: outlook.timezone="{PreferredTimeZone}"`, `Prefer: outlook.body-content-type="text"` on read routes), and the shared `GraphRequestExecutor` wiring
  - Acceptance: compiles, <= 500 lines
- [x] [P3-T9] Create `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Messages.cs`: `ListMessagesAsync` (D3 paging: `$top = min(limit, PageSize)`, `$filter=receivedDateTime ge {iso8601}`, `$orderby=receivedDateTime desc`, spec `$select` list, `@odata.nextLink` following bounded by `limit`/`MaxPages` with `warning` log on truncation-by-MaxPages, results truncated to `limit`), `GetMessageAsync` (`/users/{p}/messages/{id}` URL-escaped, spec `$select`), and `ListMeetingRequestsAsync` (same server query as list; D10 client-side filter `@odata.type == "#microsoft.graph.eventMessage"`, paging until `limit` meeting messages or bounds hit; `meetingMessageType` included in `$select` per the D10 primary form)
  - Acceptance: compiles, <= 500 lines; success paths map through `GraphMessageMapper`
- [x] [P3-T10] Create `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Calendar.cs` with the two read members: `ListCalendarWindowAsync` (`/users/{p}/calendarView?startDateTime=...&endDateTime=...&$top=...` + spec event `$select`, D3 paging) and `GetEventAsync` (`/users/{p}/events/{id}` URL-escaped + spec event `$select`); `GetMailboxSettingsAsync`/`GetFreeBusyAsync` are added to this file in Phase 4
  - Acceptance: compiles, <= 500 lines including the Phase 4 additions (plan for headroom)
- [x] [P3-T11] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientMessagesTests.cs`: handler-level request-shape tests for members 2-4 — exact URL/query composition (`$filter`, `$orderby`, `$top`, `$select` pinned to the spec field list incl. `meetingMessageType`), GET method, bearer header, `client-request-id`, both Prefer headers; multi-page `@odata.nextLink` accumulation; truncation at `limit`; `MaxPages` bound with warning log asserted; meeting-request client-side `eventMessage` filter (mixed page yields only meeting messages)
  - Acceptance: all tests pass; the shipped D10 `$select` form is pinned by an explicit assertion (verification note recorded in Open Questions)
- [x] [P3-T12] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientCalendarTests.cs`: handler-level request-shape tests for members 5-6 — `calendarView` URL with `startDateTime`/`endDateTime`/`$top`/`$select`, event-get URL with escaped id, GET method, headers as above; recorded-payload responses map to `EventDto` items with parity fields spot-asserted; empty calendarView page yields empty `ItemsResponse` success
  - Acceptance: all tests pass
- [x] [P3-T13] Run the mandatory C# toolchain loop for the Phase 3 batch (same four commands as P1-T5), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 4 — Scheduling Endpoints: MailboxSettings, GetSchedule, Status Substitute

- [x] [P4-T1] Create `src/OpenClaw.Core/CloudGraph/GraphSchedulingMapper.cs`: pure static mappers — mailboxSettings wire record to `MailboxSettingsDto` (`TimeZoneId` <- `timeZone`, `WorkingDays` <- `workingHours.daysOfWeek`, `WorkingHoursStart`/`End` <- `workingHours.startTime`/`endTime`) and getSchedule response to `FreeBusyScheduleDto` (`MailboxUpn` <- `{p}`; `BusyIntervals` from `value[0].scheduleItems` where `status` is `busy`/`oof`/`tentative` per D11, mapped to `BusyIntervalDto(Start, End)` UTC; empty window -> empty list, not an error)
  - Acceptance: compiles, <= 500 lines, no I/O
- [x] [P4-T2] Add `GetMailboxSettingsAsync` (GET `/users/{p}/mailboxSettings?$select=timeZone,workingHours`) and `GetFreeBusyAsync` (POST `/users/{p}/calendar/getSchedule` with JSON body `{ schedules: ["{p}"], startTime: { dateTime, timeZone: "UTC" }, endTime: { dateTime, timeZone: "UTC" }, availabilityViewInterval: <options> }` per the spec API example) to `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Calendar.cs`
  - Acceptance: compiles, file still <= 500 lines
- [x] [P4-T3] Add `GetStatusAsync` (D2 status substitute) to `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.cs`: probe `GET /users/{p}/mailboxSettings?$select=timeZone` through the shared executor; probe success -> success envelope with `BridgeStatusDto(State: "ready", Mode: "graph", OutlookConnected: true, CacheStale: false, StaleReason: null, LastInboxScanUtc: null, LastCalendarScanUtc: null)`; probe failure -> failure envelope with the mapped `ApiError` (no fabricated healthy status)
  - Acceptance: compiles, file still <= 500 lines
- [x] [P4-T4] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphSchedulingMapperTests.cs`: recorded-payload tests — mailboxSettings maps all four DTO fields; getSchedule maps `busy`/`oof`/`tentative` items to intervals and excludes `free`/`workingElsewhere` (D11); empty scheduleItems yields empty `BusyIntervals`; plus a CsCheck property test asserting for generated status strings that an item appears in `BusyIntervals` iff its status is in {busy, oof, tentative}
  - Acceptance: all tests pass; property test present for the D11 partition function
- [x] [P4-T5] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSchedulingTests.cs`: handler-level tests — mailboxSettings request shape (URL, `$select`, GET, headers); getSchedule POST body JSON matches the spec example field-for-field (schedules array, start/end dateTime+timeZone, `availabilityViewInterval` from options), camelCase serialization; recorded responses map to the DTOs
  - Acceptance: all tests pass; getSchedule body asserted via parsed-JSON structural comparison
- [x] [P4-T6] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientStatusTests.cs`: probe request shape (`/users/{p}/mailboxSettings?$select=timeZone`); probe success -> `ready`/`graph`/`OutlookConnected=true`/`CacheStale=false` snapshot in success envelope; probe failure (404 and 503-after-exhaustion) -> failure envelope with mapped error and no fabricated status
  - Acceptance: all tests pass
- [x] [P4-T7] Run the mandatory C# toolchain loop for the Phase 4 batch (same four commands as P1-T5), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 5 — SendMail Endpoint and Interface Completion

- [x] [P5-T1] Create `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.SendMail.cs`: `SendMailAsync` posting to `/users/{a}/sendMail` with the D7 body — pass through the `SendMailRequest` wire shape (`src/OpenClaw.HostAdapter.Contracts/MailContracts.cs`), inject `message.from.emailAddress.address = {p}` only when `{p} != {a}`, serialize camelCase (`JsonSerializerDefaults.Web`); Graph `202 Accepted` (empty body) -> `ApiEnvelope<object?>(true, null, meta, null)`; failures map through the shared D5 pipeline
  - Acceptance: compiles, <= 500 lines
- [x] [P5-T2] Add the `: IHostAdapterClient` declaration to `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.cs` and build `dotnet build OpenClaw.MailBridge.sln`
  - Acceptance: build exits 0, proving all nine `IHostAdapterClient` members are implemented (AC-1 completeness gate)
- [x] [P5-T3] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientSendMailTests.cs`: handler-level tests — POST URL uses the assistant mailbox `{a}`; body contains `from` = principal when `{p} != {a}` and omits `from` when `{p} == {a}` (structural JSON assertions matching the spec API example); `saveToSentItems` passthrough; 202 -> `ok: true, data: null`; endpoint-level error mapping samples (400 -> `INVALID_REQUEST`, 401 -> `UNAUTHORIZED`, 429 exhaustion -> `THROTTLED` retryable with `TooManyRequests` in `BridgeErrorCode`)
  - Acceptance: all tests pass
- [x] [P5-T4] Run the mandatory C# toolchain loop for the Phase 5 batch (same four commands as P1-T5), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 6 — DI Opt-In Wiring and Composition-Root Selection

- [x] [P6-T1] Create `src/OpenClaw.Core/CloudGraph/GraphServiceCollectionExtensions.cs`: public `AddGraphHostAdapterClient(this IServiceCollection, IConfiguration)` per D8 — argument null guards, internally calls `AddCloudAuth` (`src/OpenClaw.Core/CloudAuth/CloudAuthServiceCollectionExtensions.cs`), binds `GraphAdapterOptions` from `OpenClaw:GraphAdapter` with `.Validate(...GraphAdapterOptionsValidator...)` and `.ValidateOnStart()`, registers `AddHttpClient<IHostAdapterClient, GraphHostAdapterClient>` with `BaseAddress` from `GraphAdapterOptions.BaseUrl`
  - Acceptance: compiles, <= 500 lines, XML docs on the public member
- [x] [P6-T2] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphServiceCollectionExtensionsTests.cs`: registration tests — resolving `IHostAdapterClient` from a provider built with valid enabled configuration yields `GraphHostAdapterClient`; options values bind from configuration keys; `IAppTokenProvider` is registered (AddCloudAuth invoked); invalid options (missing UPN, http BaseUrl) fail at options validation (fail-closed); null-argument guards throw
  - Acceptance: all tests pass; patterned on `tests/OpenClaw.Core.Tests/CloudAuth/CloudAuthServiceCollectionExtensionsTests.cs`
- [x] [P6-T3] [expect-fail] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphBackendSelectionTests.cs` with two composition-root tests via `CoreTestWebApplicationFactory` (`tests/OpenClaw.Core.Tests/CoreTestWebApplicationFactory.cs`): (a) default path — `OpenClaw:GraphAdapter:Enabled` absent resolves `IHostAdapterClient` to `HostAdapterHttpClient` (expected to pass now); (b) opt-in path — `Enabled=true` plus valid Graph/CloudAuth configuration resolves `GraphHostAdapterClient` (expected to FAIL before P6-T4 because `Program.cs` has no selection block). Run the file and write the failing-run evidence to `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/regression-testing/graph-backend-selection-fail-before.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` naming the failing test
  - Acceptance: test (a) passes, test (b) fails, and the fail-before artifact exists with all four fields
- [x] [P6-T4] Update `src/OpenClaw.Core/Program.cs` with the single backend-selection conditional block: when `OpenClaw:GraphAdapter:Enabled` is `true` call `builder.Services.AddGraphHostAdapterClient(builder.Configuration)`; otherwise execute the existing `AddHttpClient<IHostAdapterClient, HostAdapterHttpClient>` registration (currently `src/OpenClaw.Core/Program.cs:48`) byte-for-byte unchanged; no other `Program.cs` edits
  - Acceptance: `git diff src/OpenClaw.Core/Program.cs` shows only the conditional wrapper and the new call; default-path registration lambda text unchanged
- [x] [P6-T5] Rerun `tests/OpenClaw.Core.Tests/CloudGraph/GraphBackendSelectionTests.cs`; both tests pass; append the passing-run record (`Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`) to `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/regression-testing/graph-backend-selection-fail-before.<ts>.md`
  - Acceptance: both tests green; artifact contains the fail-before and pass-after records
- [x] [P6-T6] Run the mandatory C# toolchain loop for the Phase 6 batch (same four commands as P1-T5), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 7 — Contract-Parity Suite

- [x] [P7-T1] Create `tests/OpenClaw.Core.Tests/CloudGraph/CloudGraphContractParityTests.cs`: drive `HostAdapterSchedulingService` (`src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`) with `GraphHostAdapterClient` backed by a mocked handler returning recorded Graph payloads from `GraphPayloadFixtures`, reproducing representative expectations from `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` (mailbox-settings flow, free/busy flow, calendar-window flow, and a failure-propagation flow); assertions compare the Runtime-visible outcomes (mapped domain values), not transport details
  - Acceptance: all parity tests pass; file <= 500 lines (split a `CloudGraphContractParityTests.Messages.cs` partial if needed)
- [x] [P7-T2] Verify zero production-code drift outside the allowed diff scope: run `git diff --name-only` against the branch base and confirm no files under `src/OpenClaw.Core/Agent/`, `src/OpenClaw.HostAdapter/`, `src/OpenClaw.HostAdapter.Contracts/`, `src/OpenClaw.MailBridge/`, `src/OpenClaw.MailBridge.Contracts/`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`, or any `docker-compose*` file are modified; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/other/diff-scope-verification.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing the full changed-file set
  - Acceptance: artifact exists; changed-file set is confined to `src/OpenClaw.Core/CloudGraph/**`, `src/OpenClaw.Core/Program.cs`, `tests/OpenClaw.Core.Tests/CloudGraph/**`, and the feature folder
- [x] [P7-T3] Run the mandatory C# toolchain loop for the Phase 7 batch (same four commands as P1-T5), restarting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 8 — Final QA, Coverage Comparison, and Untouched-Surface Verification

- [x] [P8-T1] Verify the 500-line cap: measure line counts of every new/modified file under `src/OpenClaw.Core/CloudGraph/` and `tests/OpenClaw.Core.Tests/CloudGraph/` plus `src/OpenClaw.Core/Program.cs`; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/file-size-cap.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (max line count and per-file table)
  - Acceptance: artifact exists; every file <= 500 lines
- [x] [P8-T2] Verify test hygiene: search `tests/OpenClaw.Core.Tests/CloudGraph/` for live-endpoint usage and temp-file APIs (patterns: `graph.microsoft.com` outside fixture URL strings used as `BaseAddress` for the mocked handler, `HttpClientHandler`, `GetTempFileName`, `GetTempPath`, `File.Write`, `Thread.Sleep`, `Task.Delay(` without a `TimeProvider` argument, `DateTime.UtcNow`, `DateTime.Now`); write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/test-hygiene.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists; zero violations (or each match explained as a mocked-handler `BaseAddress` literal)
- [x] [P8-T3] Final QA formatting gate: run `csharpier format .` then `csharpier check .`; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/csharp-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; if `format` changed any file, restart the Phase 8 QA loop from this task after recording the change
  - Acceptance: artifact exists; `csharpier check .` exits 0
- [x] [P8-T4] Final QA build gate (lint + nullable + analyzers): run `dotnet build OpenClaw.MailBridge.sln`; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/csharp-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; on failure, remediate and restart the Phase 8 QA loop from P8-T3
  - Acceptance: artifact exists; build exits 0 with zero warnings/errors
- [x] [P8-T5] Final QA test + coverage gate (includes architecture-boundary, unit, property, and contract-parity suites): run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; copy raw TRX/coverage to `artifacts/csharp/`; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/csharp-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric post-change line and branch coverage percents and pass/fail counts; on failure, remediate and restart the Phase 8 QA loop from P8-T3
  - Acceptance: artifact exists with numeric coverage values; all tests pass
- [x] [P8-T6] Produce the coverage-comparison artifact: compare P0-T5 baseline against P8-T5 post-change coverage and compute new-code coverage for `src/OpenClaw.Core/CloudGraph/**` and the changed `Program.cs` lines from the coverage XML; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/coverage-comparison.<ts>.md` reporting baseline coverage, post-change coverage, and new/changed-code coverage, with a threshold verdict (line >= 85%, branch >= 75%, no regression on changed lines)
  - Acceptance: artifact exists with all three numeric sections; verdict PASS only when all thresholds hold — otherwise the plan outcome is remediation-required, never PASS
- [x] [P8-T7] Produce the untouched-surface verification artifact: rerun the P7-T2 diff-scope check at final head, additionally asserting (a) the `Program.cs` default-path registration text is unchanged (diff hunk inspection), (b) `docker-compose` files show zero diff, and (c) `GraphBackendSelectionTests` default-path test passed in the P8-T5 run; write `docs/features/active/2026-07-02-graph-backed-adapter-115/evidence/qa-gates/untouched-surface.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists; all three assertions hold (AC-5 evidence)
- [x] [P8-T8] Reconcile the plan checklist against evidence on disk: mark every completed task, confirm every evidence-producing task's artifact exists with complete schema fields, and record the reconciliation summary (task-to-artifact table) at the bottom of this plan's Open Questions / Notes section
  - Acceptance: no checked task lacks its named artifact; any gap flips the outcome to INCOMPLETE

## Test Plan

- **Unit:** validator rules (P1-T3); executor auth/headers/envelope (P2-T3); retry/backoff with `FakeTimeProvider` (P2-T4); full D5 error matrix (P2-T5); mapper field tables incl. fail-fast and missing-optional defaults (P3-T4, P3-T6, P4-T4); per-endpoint request shapes, paging, meeting filter, status substitute, sendMail body variants (P3-T11, P3-T12, P4-T5, P4-T6, P5-T3); DI registration and fail-closed validation (P6-T2).
- **Property-based (CsCheck):** enum-map round-trips vs `SchedulingDtoMapper` inverses and OR-5 attendee/recipient JSON `ParseAttendees` round-trips (P3-T5, P3-T7); D11 busy-status partition (P4-T4).
- **Architecture:** D12 rules 1-3 via NetArchTest/dependency inspection (P1-T4), run in every phase's test pass.
- **Contract parity:** `HostAdapterSchedulingService` flows against `GraphHostAdapterClient` with recorded payloads (P7-T1).
- **Composition root:** default-path and opt-in selection with fail-before/pass-after evidence (P6-T3/P6-T5).
- **Integration:** none required — no live Graph calls anywhere; live-tenant verification is explicitly out of scope (F17).
- **Coverage evidence:** baseline `evidence/baseline/csharp-test-coverage.<ts>.md` (P0-T5); post-change `evidence/qa-gates/csharp-test-coverage.<ts>.md` (P8-T5); comparison `evidence/qa-gates/coverage-comparison.<ts>.md` (P8-T6). Raw intermediates in `artifacts/csharp/`.

## Open Questions / Notes

- **D10 verification item (only externally unverifiable shape):** whether Graph v1.0 accepts `meetingMessageType` in `$select` on the base `/messages` collection. The plan ships the primary form (`meetingMessageType` in `$select`, pinned by P3-T11). If implementation-time verification shows rejection, apply the spec's fallback (drop it from `$select`; populate `MeetingMessageType` only on `GetMessageAsync` of an `eventMessage`), update the P3-T11 pin accordingly, and record the decision here.
  - **P3-T11 verification note (2026-07-02):** the shipped form is the D10 primary form — `meetingMessageType` remains in the message `$select` list. Pinned by explicit literal assertions in `GraphHostAdapterClientMessagesTests` (`ExpectedMessageSelect` constant; `ListMeetingRequests_FiltersToEventMessagesClientSide` additionally asserts `meetingMessageType` is present in `$select`). No implementation-time evidence of rejection was encountered (all tests are handler-mocked; live verification remains out of scope per F17), so the fallback was not applied.
- **CSharpier command form:** this repo uses the global `csharpier` 1.3.0 executable (`csharpier format .` / `csharpier check .`); there is no local tool manifest, so `dotnet csharpier` is not used despite the wording in `.claude/rules/csharp.md`.
- **Test stack note:** MSTest + FluentAssertions + Moq + CsCheck is the repository's actual stack (per spec and existing `tests/OpenClaw.Core.Tests/`), notwithstanding `.claude/rules/csharp.md`'s xUnit/NSubstitute wording.
- **Interface sequencing:** `GraphHostAdapterClient` gains the `: IHostAdapterClient` declaration only at P5-T2 so every phase batch builds green; the compiler is the completeness gate for all nine members.
- **`GraphErrorMapper.cs` split (P2-T2) and fixture/parity partials (P3-T3, P7-T1)** are pre-authorized contingencies for the 500-line cap; no other new files are in scope. (Outcome: none of the split contingencies were needed — largest file is 344 lines.)

### P8-T8 Reconciliation Summary (2026-07-02T20-57)

All 57 plan tasks are checked; every evidence-producing task's artifact exists on disk with the full `Timestamp:`/`Command:`/`EXIT_CODE:`/`Output Summary:` schema. Task-to-artifact table:

| Task | Artifact | Status |
|---|---|---|
| P0-T1 | `evidence/baseline/phase0-instructions-read.md` | present, complete |
| P0-T2 | `evidence/baseline/phase0-instructions-read.md` (`Mode Verification:` section, verdict PASS) | present, complete |
| P0-T3 | `evidence/baseline/csharp-format.2026-07-02T20-04.md` | present, complete |
| P0-T4 | `evidence/baseline/csharp-build.2026-07-02T20-04.md` | present, complete |
| P0-T5 | `evidence/baseline/csharp-test-coverage.2026-07-02T20-04.md` (+ raw XMLs in `artifacts/csharp/baseline-2026-07-02T20-04/`) | present, complete, numeric coverage |
| P1-T1..P1-T4 | source/test files (no artifact required); verified by P1-T5 loop | complete |
| P1-T5 | toolchain single pass (467 Core tests green) | complete |
| P2-T1..P2-T5 | source/test files; verified by P2-T6 loop | complete |
| P2-T6 | toolchain single pass (495 Core tests green) | complete |
| P3-T1..P3-T12 | source/test files; D10 pin note recorded above; verified by P3-T13 loop | complete |
| P3-T13 | toolchain single pass (569 Core tests green) | complete |
| P4-T1..P4-T6 | source/test files; verified by P4-T7 loop | complete |
| P4-T7 | toolchain single pass (594 Core tests green) | complete |
| P5-T1..P5-T3 | source/test files; P5-T2 completeness gate = `dotnet build` exit 0 with `: IHostAdapterClient` | complete |
| P5-T4 | toolchain single pass (602 Core tests green) | complete |
| P6-T1, P6-T2, P6-T4 | source/test files + `git diff src/OpenClaw.Core/Program.cs` (conditional wrapper only) | complete |
| P6-T3 | `evidence/regression-testing/graph-backend-selection-fail-before.2026-07-02T20-45.md` (fail-before record, EXIT_CODE 1, failing test named) | present, complete |
| P6-T5 | same artifact, pass-after record appended (EXIT_CODE 0, both tests green) | present, complete |
| P6-T6 | toolchain single pass (611 Core tests green) | complete |
| P7-T1 | `CloudGraphContractParityTests.cs`; verified by P7-T3 loop | complete |
| P7-T2 | `evidence/other/diff-scope-verification.2026-07-02T20-50.md` | present, complete |
| P7-T3 | toolchain single pass (616 Core tests green) | complete |
| P8-T1 | `evidence/qa-gates/file-size-cap.2026-07-02T20-52.md` (max 344 <= 500) | present, complete |
| P8-T2 | `evidence/qa-gates/test-hygiene.2026-07-02T20-52.md` (zero violations; 3 benign literals explained) | present, complete |
| P8-T3 | `evidence/qa-gates/csharp-format.2026-07-02T20-53.md` (check exit 0) | present, complete |
| P8-T4 | `evidence/qa-gates/csharp-build.2026-07-02T20-53.md` (0 warnings / 0 errors) | present, complete |
| P8-T5 | `evidence/qa-gates/csharp-test-coverage.2026-07-02T20-53.md` (1063 passed; pooled 92.34% line / 83.16% branch; raw XMLs in `artifacts/csharp/final-2026-07-02T20-53/`) | present, complete, numeric coverage |
| P8-T6 | `evidence/qa-gates/coverage-comparison.2026-07-02T20-53.md` (verdict PASS; new-code 99.71% line / 90.65% branch) | present, complete |
| P8-T7 | `evidence/qa-gates/untouched-surface.2026-07-02T20-56.md` (all three assertions hold) | present, complete |
| P8-T8 | this section | complete |

Acceptance criteria: all 6 AC checked off in `issue.md`, `spec.md`, and `user-story.md` (identical AC set in the three files; full-feature mode sources are `spec.md` + `user-story.md`, with `issue.md` mirrored per the delegation directive). Outcome: COMPLETE — no gaps.
