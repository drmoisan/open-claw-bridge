# organizer-reschedule - Plan

- **Issue:** #128
- **Parent (optional):** epic `docs/features/epics/openclaw-vision/epic-plan.md` (Epic D, feature F18, wave 4)
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T07-30
- **Status:** Ready for Preflight
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata; `spec.md` and `user-story.md` both present and authoritative)

## Required References

Policy reading order (per `.claude/skills/policy-compliance-order/SKILL.md`):

1. `CLAUDE.md` / auto-loaded `.claude/rules/` standing instructions
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/quality-tiers.md`

**All work must comply with these policies; do not duplicate their content here.**

Authoritative feature documents (full-feature mode: `spec.md` + `user-story.md` are the acceptance-criteria sources):

- `docs/features/active/2026-07-07-organizer-reschedule-128/spec.md` (AC-1..AC-9; gate truth table; evaluation order; wire contract)
- `docs/features/active/2026-07-07-organizer-reschedule-128/user-story.md` (AC-1..AC-9; scenarios)
- `docs/features/active/2026-07-07-organizer-reschedule-128/issue.md` (mode marker)
- Research: `docs/features/active/2026-07-07-organizer-reschedule-128/research/2026-07-07T07-35-organizer-reschedule.research.md` (authoritative file-change list, seam map, D5 matrix, rejected alternatives)

## Global Constraints (apply to every task)

- **Diff scope (confined):** production adds `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs` and `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs`; production modifies `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`, `src/OpenClaw.Core/HostAdapterHttpClient.cs`, `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`, `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`, `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs`, `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`, `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs`, `src/OpenClaw.Core/Agent/SentActionKey.cs`; test changes confined to `tests/OpenClaw.Core.Tests/**` — test ADDs: `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs`, `tests/OpenClaw.Core.Tests/HostAdapterHttpClientRescheduleTests.cs`, `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleTests.cs`, `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleIntentPropertyTests.cs`, plus the optional 500-line-cap split siblings `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventErrorTests.cs` (P1-T4 fallback), `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceRescheduleTests.cs` (P2-T3 fallback), and `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleEdgeTests.cs` (P3-T5 fallback); test MODIFIES: `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs` (P2-T3, unless the split fallback applies) and the mechanical P3-T2 mock additions to pre-existing `SchedulingWorker*Tests` files; `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` (616 lines, pre-existing 500-line-cap violation) MUST remain unmodified — the local-adapter negative test is a test ADD in the new sibling file; plus this feature folder. Zero changes to `src/OpenClaw.Core/Program.cs` (`ISeriesMoveHistory` already registered), `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs`, `src/OpenClaw.Core/Agent/OneOnOneMoveGuard.cs`, `src/OpenClaw.Core/Agent/Models/NormalizedMeetingContext.cs`, `src/OpenClaw.Core/Agent/Contracts/ActionAuditRecord.cs`, `src/OpenClaw.Core/Agent/Contracts/PurviewActivityLogProjection.cs` (documented fallback covers the new action type), `src/OpenClaw.Core/CloudGraph/GraphRequestExecutor.cs`, `src/OpenClaw.Core/CloudGraph/SendOnBehalfAuthorizer.cs`, and `quality-tiers.yml`. No new dependencies. No schema/migration changes.
- **Toolchain loop (per phase batch):** `csharpier format .` then `csharpier check .` (global tool 1.3.0; not `dotnet csharpier`), `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as errors), `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (includes the NetArchTest architecture-boundary suites). Restart the loop from formatting if any step fails or changes files; a phase is complete only when all steps pass in a single pass.
- **Fail-closed invariant:** null/missing event, non-organizer, missing original times, zero proposed slots, guard block, gate off, local-backend `NOT_SUPPORTED`, and any failure envelope or exception all result in no Graph write, no `series_moves` row, and no dedupe row. Any ambiguity resolves to "no write".
- **Determinism:** all time via injected `TimeProvider` (`FakeTimeProvider` in tests); `DateTime.Now`/`DateTime.UtcNow` banned; no `Thread.Sleep`/`Task.Delay`/wall-clock waits in tests; retry-exhaustion tests advance simulated time; no live Graph calls anywhere — all HTTP through the shared `FakeHttpHandler` with base address `https://graph.example.test/v1.0/`; no temporary files.
- **Test stack:** MSTest + FluentAssertions + Moq (+ CsCheck for property tests, `MockBehavior.Strict` `IAppTokenProvider`), per repository-actual convention noted in the research (not the xUnit/NSubstitute wording in `.claude/rules/csharp.md`).
- **File size:** every new or modified production and test file <= 500 lines.
- **Architecture (No-COM):** pure decision logic stays host-neutral; the Graph HTTP call lives only in the new `CloudGraph` partial behind `IHostAdapterClient`; no domain-to-adapter reference; existing NetArchTest boundary tests must pass unmodified.
- **Evidence:** all evidence artifacts under `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/<kind>/` (canonical scheme per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`; non-canonical `artifacts/` evidence paths are prohibited). Raw command intermediates (TRX, coverage XML, build logs) go to `artifacts/csharp/`. `<ts>` in artifact names is the ISO-8601 `yyyy-MM-ddTHH-mm` execution timestamp.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture and Policy Compliance

- [x] [P0-T1] Read the policy documents in the Required References order (items 1-5) and write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read
  - Acceptance: artifact exists with all three fields populated before any Phase 1 work begins
- [x] [P0-T2] Verify full-feature document preconditions: `docs/features/active/2026-07-07-organizer-reschedule-128/issue.md` contains `- Work Mode: full-feature`, and `spec.md` + `user-story.md` exist in the feature folder with `## Acceptance Criteria` sections listing AC-1..AC-9; record the check in `evidence/baseline/phase0-instructions-read.md` (append a `Mode Verification:` section)
  - Acceptance: appended section names the three files, the mode marker value, and a pass/fail verdict; fail closed (stop and report) on any mismatch
- [x] [P0-T3] Capture the C# formatting baseline: run `csharpier check .` from repo root and write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/baseline/csharp-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists with all four fields; `Output Summary:` states pass/fail and any offending file count
- [x] [P0-T4] Capture the C# build/analyzer/nullable baseline: run `dotnet build OpenClaw.MailBridge.sln` and write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/baseline/csharp-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists with all four fields; `Output Summary:` records warning/error counts (expected 0/0 on clean baseline)
- [x] [P0-T5] Capture the C# test, architecture-boundary, and coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (this run includes the NetArchTest boundary suites), copy raw TRX/coverage output under `artifacts/csharp/`, and write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/baseline/csharp-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric baseline line-coverage percent, numeric baseline branch-coverage percent, and total pass/fail test counts
  - Acceptance: artifact exists with all four fields and numeric coverage headline values (no placeholders); raw intermediates present under `artifacts/csharp/`

### Phase 1 — Adapter Seam: UpdateEventTimesAsync (IHostAdapterClient member 10)

- [x] [P1-T1] Update `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`: add the additive member `Task<ApiEnvelope<EventDto>> UpdateEventTimesAsync(string bridgeId, DateTimeOffset newStartUtc, DateTimeOffset newEndUtc, string? requestId = null, CancellationToken cancellationToken = default)` with XML docs stating the Graph wire route (`PATCH /users/{principal}/events/{id}`), the start/end-only update scope (the body structurally cannot carry `body`/`subject`/`location`/`attendees`), and the local-backend fail-closed `NOT_SUPPORTED` behavior
  - Acceptance: file compiles once both implementations (P1-T2, P1-T3) exist, <= 500 lines; no existing member signature changed
- [x] [P1-T2] Create `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs`: implement `UpdateEventTimesAsync` as `PATCH users/{Principal}/events/{Uri.EscapeDataString(bridgeId)}` through the shared `GraphRequestExecutor` (inheriting bearer-token acquisition, `client-request-id` propagation from `requestId`, 429/502/503/504 retry with `Retry-After` precedence via injected `TimeProvider`, and the D5 error matrix); request body serialized camelCase via `GraphRequestExecutor.JsonOptions` with exactly two top-level properties `start` and `end`, each a dateTimeTimeZone pair with `dateTime` rendered from the UTC instant using the invariant seconds-precision `"s"` format and `timeZone` = `"UTC"` (mirror the `SchedulingDateTime` helper precedent in `GraphHostAdapterClient.Calendar.cs`); no `Prefer` headers; 200 body mapped via `GraphEventMapper.Map` (unparseable 2xx -> `TRANSPORT_FAILURE`; mapping gap -> `INTERNAL_ERROR`; no fabricated data)
  - Acceptance: file compiles, <= 500 lines, namespace/partial matches `GraphHostAdapterClient.SendMail.cs` precedent; no change to `GraphRequestExecutor.cs`
- [x] [P1-T3] Update `src/OpenClaw.Core/HostAdapterHttpClient.cs`: implement `UpdateEventTimesAsync` as a fail-closed synthesized failure envelope — `ApiError` code `NOT_SUPPORTED`, `Retryable: false`, message stating the local HostAdapter backend has no calendar-write route and organizer reschedule requires the Graph adapter — performing no HTTP I/O and no token acquisition
  - Acceptance: file compiles, <= 500 lines; the method issues zero `HttpClient` invocations
- [x] [P1-T4] Create `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs`: mocked-Graph contract suite using the established `FakeHttpHandler` pattern (`MockBehavior.Strict` `IAppTokenProvider`, `FakeTimeProvider`, base address `https://graph.example.test/v1.0/`) covering: (a) method `PATCH` and URL-escaped principal route (assert `AbsolutePath` per the sendMail test precedent); (b) headers — bearer auth, `client-request-id` equal to the supplied `requestId`, `Content-Type: application/json`, no `Prefer` header; (c) exact body shape via structural `JsonDocument` assertions — exactly `start` and `end` dateTimeTimeZone pairs (UTC, seconds precision) and no other top-level properties, in particular no `body`, `subject`, `location`, or `attendees`; (d) 200 + updated-event JSON maps via `GraphEventMapper.Map` to `ApiEnvelope<EventDto>` `ok: true` with `Start`/`End` reflecting the response; (e) D5 error samples — 400 -> `INVALID_REQUEST`, 403 with Graph `error.code` `ErrorAccessDenied` -> `UNAUTHORIZED` with `BridgeErrorCode` passthrough, 404 -> `NOT_FOUND`, all non-retryable; (f) 429 retry exhaustion driven by `FakeTimeProvider.Advance` -> `THROTTLED` with `Retry-After` precedence; (g) unparseable 2xx body -> `TRANSPORT_FAILURE` and missing required event fields -> `INTERNAL_ERROR`; if the suite approaches the 500-line cap, split the D5 error/parse-failure samples (e)-(g) into a sibling file `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventErrorTests.cs`
  - Acceptance: all new tests pass; each of (a)-(g) has at least one named test; each resulting test file <= 500 lines
- [x] [P1-T5] Create `tests/OpenClaw.Core.Tests/HostAdapterHttpClientRescheduleTests.cs` (new sibling file mirroring the existing `HostAdapterHttpClientSchedulingTests.cs`/`HostAdapterHttpClientSendMailTests.cs` split; references the shared `FakeHttpHandler` defined in `HostAdapterHttpClientTests.cs`): `UpdateEventTimesAsync` on the local Stage-0 adapter returns `Ok == false` with `Error.Code == "NOT_SUPPORTED"` and `Error.Retryable == false`, with the mocked handler invoked zero times
  - Acceptance: both assertions (envelope contract and zero HTTP invocations) pass in named tests; new file <= 500 lines; `HostAdapterHttpClientTests.cs` remains unmodified
- [x] [P1-T6] Run the mandatory C# toolchain loop for the Phase 1 batch (`csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`), restarting from formatting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass; existing architecture-boundary suites pass unmodified

### Phase 2 — Service Seam: RescheduleEventAsync (ISchedulingService)

- [x] [P2-T1] Update `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`: add the additive member `Task RescheduleEventAsync(string eventId, DateTimeOffset newStartUtc, DateTimeOffset newEndUtc, string? correlationId = null, CancellationToken ct = default)` with XML docs mirroring the `SendMailAsync` seam shape (returns `Task`, not the updated DTO)
  - Acceptance: file compiles once P2-T2 exists, <= 500 lines; no existing member changed
- [x] [P2-T2] Update `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`: implement `RescheduleEventAsync` mirroring `SendMailAsync` — guard-clause the event id (fail fast on null/empty), delegate to `hostAdapterClient.UpdateEventTimesAsync(eventId, newStartUtc, newEndUtc, requestId: correlationId, ct)`, and on a non-`Ok` envelope throw `InvalidOperationException($"Organizer reschedule failed: {code}: {message}")`; client exceptions propagate unwrapped
  - Acceptance: file compiles, <= 500 lines; no change to any existing method body
- [x] [P2-T3] Extend `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceTests.cs`: unit cases — delegation passes `eventId`/`newStartUtc`/`newEndUtc` through to `UpdateEventTimesAsync` unchanged; `correlationId` forwards as `requestId`; a non-`Ok` envelope throws `InvalidOperationException` whose message contains the error code and message; a null/empty event id fails fast without calling the client; if adding these four cases pushes the file over the 500-line cap, place the new reschedule cases in a new sibling file `tests/OpenClaw.Core.Tests/Agent/Runtime/HostAdapterSchedulingServiceRescheduleTests.cs`
  - Acceptance: all four new named tests pass; each touched/created test file <= 500 lines
- [x] [P2-T4] Run the mandatory C# toolchain loop for the Phase 2 batch (same four commands as P1-T6), restarting from formatting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass

### Phase 3 — Worker Orchestration: SchedulingWorker.Reschedule

- [x] [P3-T1] Update `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs` (append consts `Rescheduled = "rescheduled"`, `RescheduleFailed = "reschedule_failed"`, `RescheduleDisabled = "reschedule_disabled"`, `RescheduleBlocked = "reschedule_blocked"`) and `src/OpenClaw.Core/Agent/SentActionKey.cs` (append const `OrganizerReschedule = "organizer-reschedule"`, colon-free)
  - Acceptance: both files compile, <= 500 lines each; no existing constant value changed
- [x] [P3-T2] Update `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs`: add `ISeriesMoveHistory seriesMoveHistory` to the primary constructor (already DI-registered; no `Program.cs` change), and add the mechanical `Mock<ISeriesMoveHistory>` to every existing `SchedulingWorker*Tests` builder in `tests/OpenClaw.Core.Tests/Agent/Runtime/` so the full suite compiles and passes with zero behavioral test edits
  - Acceptance: solution builds; all pre-existing worker tests pass; `git diff` of pre-existing worker test files shows only constructor-argument/mock-field additions
- [x] [P3-T3] Create `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs`: (a) pure `internal static` intent-computation helper — eligible iff hydrated `meetingEvent` is non-null, `context.IsOrganizer == true`, `Start`/`End` non-null, `context.EventId` non-empty, and at least one proposed slot exists; target start = first proposed slot's start; duration preserved (`End - Start`); no intent -> return silently with no audit row; (b) pure `internal static` reschedule ActingFlags snapshot helper producing `CalendarWriteEnabled=<bool>;EnableOrganizerReschedule=<bool>` (the existing `BuildActingFlags` is not widened); (c) reschedule audit-record builder populating `EventId`, `OriginalStartUtc`/`OriginalEndUtc`/`NewStartUtc`/`NewEndUtc`, the correlation id, and action type `SentActionKey.OrganizerReschedule`; (d) orchestration in the spec's exact evaluation order — move-guard consult before the flag gate (`OneOnOneMoveGuard.ResolveSeriesKey`, `ISeriesMoveHistory.GetMovedOccurrenceStartsAsync`, occurrence starts from calendar-view data filtered by `SeriesMasterId` else empty list, `OneOnOneMoveGuard.CanMove`; blocked -> audit `reschedule_blocked`, no write regardless of flags), then `CalendarWritePolicy.OrganizerRescheduleAllowed` gate (off -> log intended move old->new at Information and audit `reschedule_disabled` with the four time columns populated; no Graph call, no write-path token acquisition, no `series_moves` row, no dedupe row), then dedupe (`SentActionKey.Build(mailbox, messageId, SentActionKey.OrganizerReschedule)`; hit -> audit `dedupe_skipped`, return), then write (`schedulingService.RescheduleEventAsync(eventId, newStartUtc, newEndUtc, correlationId, ct)`; on exception audit `reschedule_failed` with `ErrorDetail` durable before rethrow, no bookkeeping), then post-write bookkeeping in order — audit `rescheduled`, `seriesMoveHistory.RecordMoveAsync(seriesKey, originalStartUtc, timeProvider.GetUtcNow(), ct)` with the pre-move occurrence start, `sentActionStore.RecordAsync(dedupeKey, ...)`; one GUID correlation id per evaluation forwarded as the adapter request id; audit writes via the existing `WriteAuditSafelyAsync`
  - Acceptance: file compiles, <= 500 lines, host-neutral (no `CloudGraph` reference); evaluation order matches spec Behavior steps 1-6 exactly
- [x] [P3-T4] Update `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`: thread the hydrated `meetingEvent` (`SchedulingEventDto?`) into `ProposeAndActAsync`, and replace the trailing `!CalendarWriteEnabled` stub block (including its `"CalendarWriteEnabled is false; not writing the calendar"` log line) with a call into the P3-T3 reschedule-evaluation method; `NormalizedMeetingContext` is not widened
  - Acceptance: file compiles, <= 500 lines; no other pipeline stage (hydrate/normalize/triage/classify/send) modified
- [x] [P3-T5] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleTests.cs`: worker unit suite (MSTest + Moq + `FakeTimeProvider`) covering: (a) the four-row gate truth table — only `CalendarWriteEnabled=true` + `EnableOrganizerReschedule=true` produces exactly one `RescheduleEventAsync` call; the other three rows produce zero service calls, zero `RecordMoveAsync` calls, zero dedupe records, and a `reschedule_disabled` audit; (b) dry-run detail — `reschedule_disabled` audit carries all four time columns and the reschedule ActingFlags snapshot; (c) guard block — a `ONE_ON_ONE` intent whose move history violates the rolling-six/previous-week rule audits `reschedule_blocked` with no write even with both flags on, proving guard-before-gate ordering; (d) success — exactly one `rescheduled` audit (action type `organizer-reschedule`, four time columns, correlation id), then `RecordMoveAsync(seriesKey, originalStartUtc, ...)` with the pre-move start, then dedupe record, in that order; (e) failure — service throw yields a `reschedule_failed` audit with `ErrorDetail` before the exception propagates, and no `RecordMoveAsync` or dedupe record; (f) dedupe hit — second evaluation of the same message audits `dedupe_skipped` and issues no service call; (g) no-intent silence rows — null event, non-organizer, missing `Start`/`End`, empty `EventId`, and zero slots each produce no audit row and no service call; (h) send-path isolation — an evaluated send in the same run still persists the unmodified `BuildActingFlags` string `SendEnabled=<bool>;CalendarWriteEnabled=<bool>`; if the file approaches the 500-line cap, split rows (f)-(h) into `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleEdgeTests.cs`
  - Acceptance: all new tests pass; each of (a)-(h) has at least one named test; each test file <= 500 lines
- [x] [P3-T6] Create `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingWorkerRescheduleIntentPropertyTests.cs`: CsCheck property tests satisfying the T1 >= 1-property-per-new-pure-function obligation — (1) duration preservation: for all valid original intervals and proposed slots, `newEnd - newStart == originalEnd - originalStart`; (2) eligibility monotonicity: removing the event, the organizer bit, the times, the event id, or all slots from any eligible input never yields an intent; (3) flags-snapshot round-trip: for all boolean pairs the snapshot string parses back to the input pair
  - Acceptance: all three properties pass with reproducible seeds (CsCheck default reporting); file <= 500 lines
- [x] [P3-T7] Verify the send-path regression surface: confirm via `git diff` that all pre-existing `SchedulingWorker*Tests` files contain only the P3-T2 mechanical mock additions, that `SchedulingWorker.Audit.cs` and its `BuildActingFlags` are textually unmodified, and that the full test run includes passing send-path audit/dedupe suites; write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/regression-testing/send-path-regression-surface.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing the verified files
  - Acceptance: artifact exists with all four fields; zero non-mechanical modifications to pre-existing send-path tests; `SchedulingWorker.Audit.cs` unchanged
- [x] [P3-T8] Run the mandatory C# toolchain loop for the Phase 3 batch (same four commands as P1-T6), restarting from formatting on any failure or file change
  - Acceptance: all steps exit 0 in a single uninterrupted pass; architecture-boundary suites pass with no domain-to-adapter reference introduced

### Phase 4 — Live-Verification Runbook and Human-Interaction Record

- [ ] [P4-T1] Author `docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md` (tracked deliverable for AC-9; the orchestrator may delegate authoring to the human-exception-runbook agent, but this plan owns verifying the file exists with the required content) covering, in order: (1) granting the `Calendars.ReadWrite` application permission and obtaining tenant-admin consent (cross-reference existing RBAC/consent runbooks; do not duplicate procedures); (2) enabling `OpenClaw__AgentPolicy__CalendarWriteEnabled` and `OpenClaw__AgentPolicy__EnableOrganizerReschedule` in a real deployment; (3) observing a live organizer-owned event move — the calendar change, the `rescheduled` audit row with the four time columns, and the `series_moves` row; (4) disabling the flags after verification (the global kill switch shuts the path off independently); F11 HI-1 precedent for record shape
  - Acceptance: runbook exists at the exact path with all four sections; professional tone per `.claude/rules/tonality.md`
- [x] [P4-T2] Write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/other/human-interaction-record.<ts>.md` documenting the live-tenant verification `human_interaction` requirement for the orchestrator checkpoint: requirement description (live verification cannot be automated — no Azure/Exchange credentials in this environment or CI), `response: "exception"`, and `runbook_path: docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md`, satisfying the `.claude/rules/orchestrator-state.md` invariant that an `exception` carries a non-empty `runbook_path`
  - Acceptance: artifact exists with `Timestamp:`, the requirement text, the `response` value, and the `runbook_path`; the orchestrator (not this plan's executor) records the entry in `artifacts/orchestration/orchestrator-state.json`

### Phase 5 — Final QA, Coverage Comparison, AC Check-off, and Reconciliation

- [x] [P5-T1] Verify the 500-line cap: measure line counts of every new/modified production file (the 2 adds and 8 modifies listed in Global Constraints) and every new/modified test file under `tests/OpenClaw.Core.Tests/` — `CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs`, `HostAdapterHttpClientRescheduleTests.cs`, `Agent/Runtime/HostAdapterSchedulingServiceTests.cs`, `Agent/Runtime/SchedulingWorkerRescheduleTests.cs`, `Agent/Runtime/SchedulingWorkerRescheduleIntentPropertyTests.cs`, the mechanically-touched pre-existing `SchedulingWorker*Tests` files, and, when created under their split fallbacks, `CloudGraph/GraphHostAdapterClientRescheduleEventErrorTests.cs`, `Agent/Runtime/HostAdapterSchedulingServiceRescheduleTests.cs`, and `Agent/Runtime/SchedulingWorkerRescheduleEdgeTests.cs` (the unmodified `HostAdapterHttpClientTests.cs` is excluded — its pre-existing 616-line violation is out of scope and the file is not touched); write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/file-size-cap.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (per-file line-count table and maximum)
  - Acceptance: artifact exists; every listed file <= 500 lines
- [x] [P5-T2] Verify test hygiene: search the touched test files for banned APIs and live-endpoint usage (patterns: `Thread.Sleep`, `Task.Delay(`, `DateTime.UtcNow`, `DateTime.Now`, `GetTempFileName`, `GetTempPath`, `File.Write`, `HttpClientHandler`, `graph.microsoft.com`); write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/test-hygiene.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
  - Acceptance: artifact exists; zero violations (or each match individually justified as a non-executing literal)
- [x] [P5-T3] Final QA formatting gate: run `csharpier format .` then `csharpier check .`; write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/csharp-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; if `format` changed any file, record the change and restart the Phase 5 QA loop from this task
  - Acceptance: artifact exists; `csharpier check .` exits 0
- [x] [P5-T4] Final QA build gate (lint + nullable + analyzers): run `dotnet build OpenClaw.MailBridge.sln`; write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/csharp-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`; on failure, remediate and restart the Phase 5 QA loop from P5-T3
  - Acceptance: artifact exists; build exits 0 with zero warnings/errors
- [x] [P5-T5] Final QA test + coverage gate (includes architecture-boundary, unit, property, and contract suites): run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; copy raw TRX/coverage to `artifacts/csharp/`; write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/csharp-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric post-change line and branch coverage percents and pass/fail counts; on failure, remediate and restart the Phase 5 QA loop from P5-T3
  - Acceptance: artifact exists with numeric coverage values (no placeholders); all tests pass
- [x] [P5-T6] Produce the coverage-comparison artifact: compare the P0-T5 baseline against the P5-T5 post-change coverage and compute changed/new-code coverage for `GraphHostAdapterClient.RescheduleEvent.cs`, `SchedulingWorker.Reschedule.cs`, and the changed lines of the 8 modified production files from the coverage XML; write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/coverage-comparison.<ts>.md` reporting baseline coverage, post-change coverage, and new/changed-code coverage, with a threshold verdict (line >= 85%, branch >= 75%, no regression on changed lines)
  - Acceptance: artifact exists with all three numeric sections; verdict PASS only when all thresholds hold — otherwise the plan outcome is remediation-required, never PASS
- [x] [P5-T7] Verify zero production-code drift outside the allowed diff scope: run `git diff --name-only` against the branch base and confirm the changed-file set is confined to the Global Constraints diff scope, with zero changes to the enumerated prohibited files (`Program.cs`, `CalendarWritePolicy.cs`, `OneOnOneMoveGuard.cs`, `NormalizedMeetingContext.cs`, `ActionAuditRecord.cs`, `PurviewActivityLogProjection.cs`, `GraphRequestExecutor.cs`, `SendOnBehalfAuthorizer.cs`, `quality-tiers.yml`); write `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/other/diff-scope-verification.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` listing the full changed-file set
  - Acceptance: artifact exists; changed-file set matches the Global Constraints diff scope exactly (any deviation documented and justified or remediated)
- [x] [P5-T8] Verify AC-1 (gate truth table) against the P3-T5(a) truth-table tests and check off the AC-1 checkbox in both `docs/features/active/2026-07-07-organizer-reschedule-128/spec.md` and `docs/features/active/2026-07-07-organizer-reschedule-128/user-story.md`
  - Acceptance: named passing tests cited; AC-1 checked `[x]` in both files
- [x] [P5-T9] Verify AC-2 (flag-off no-behavior-change) against the P3-T5(a)/(b)/(h) tests and the P3-T7 regression-surface artifact and check off the AC-2 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: named passing tests and the regression-surface artifact cited; AC-2 checked `[x]` in both files
- [x] [P5-T10] Verify AC-3 (move-guard block, guard-before-gate) against the P3-T5(c) tests and check off the AC-3 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: named passing tests cited; AC-3 checked `[x]` in both files
- [x] [P5-T11] Verify AC-4 (successful write: wire shape, correlation id, `rescheduled` audit, `RecordMoveAsync` pre-move start, dedupe record) against the P1-T4(a)-(d) contract tests and the P3-T5(d) worker tests and check off the AC-4 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: named passing tests cited; AC-4 checked `[x]` in both files
- [x] [P5-T12] Verify AC-5 (fail-closed on adapter/Graph error, D5 matrix, local `NOT_SUPPORTED` with zero I/O) against the P1-T4(e)-(g), P1-T5, P2-T3, and P3-T5(e) tests and check off the AC-5 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: named passing tests cited; AC-5 checked `[x]` in both files
- [x] [P5-T13] Verify AC-6 (idempotency / dedupe skip) against the P3-T5(f) tests and check off the AC-6 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: named passing tests cited; AC-6 checked `[x]` in both files
- [x] [P5-T14] Verify AC-7 (mocked-Graph contract suite exists with the required coverage areas) against `tests/OpenClaw.Core.Tests/CloudGraph/GraphHostAdapterClientRescheduleEventTests.cs` (P1-T4) and check off the AC-7 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: file exists with named tests for each required area; AC-7 checked `[x]` in both files
- [x] [P5-T15] Verify AC-8 (quality gates: coverage thresholds, property-test density, architecture boundaries) against the P5-T5/P5-T6 coverage artifacts, the P3-T6 property tests, and the green architecture-boundary suites and check off the AC-8 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: numeric coverage verdict PASS, all three properties passing, boundary suites green; AC-8 checked `[x]` in both files
- [ ] [P5-T16] Verify AC-9 (runbook exists; `human_interaction` exception record with `runbook_path` produced for the orchestrator) against the P4-T1 runbook and P4-T2 record and check off the AC-9 checkbox in both `spec.md` and `user-story.md`
  - Acceptance: both artifacts exist at their exact paths; AC-9 checked `[x]` in both files
- [x] [P5-T17] Reconcile the plan checklist against evidence on disk: mark every completed task, confirm every evidence-producing task's artifact exists with complete schema fields, verify each acceptance criterion in the Acceptance Criteria Mapping table below traces to a completed task, and record the reconciliation summary (task-to-artifact table plus AC verdict column) at the bottom of this plan's Open Questions / Notes section
  - Acceptance: no checked task lacks its named artifact; every AC row has a completed implementing/verifying task; any gap flips the outcome to INCOMPLETE

## Acceptance Criteria Mapping

Spec and user-story AC (identical AC-1..AC-9 sets; both files are check-off targets) map to plan tasks as follows:

| AC | Summary | Implementing / verifying tasks |
|---|---|---|
| AC-1 | Gate truth table: exactly one PATCH on true/true; zero writes and zero write-path token acquisitions on the other three rows | P3-T3, P3-T5 (a), P5-T8 |
| AC-2 | Flag-off no-behavior-change: `reschedule_disabled` audit with four time columns; no Graph/`series_moves`/dedupe writes; send path and its `ActingFlags` unchanged | P3-T3, P3-T4, P3-T5 (a)(b)(h), P3-T7, P5-T9 |
| AC-3 | Move-guard block before the flag gate; `reschedule_blocked`, no write even with both flags on | P3-T3, P3-T5 (c), P5-T10 |
| AC-4 | Successful write: wire shape (bearer, `client-request-id`, exact start/end body), `rescheduled` audit, pre-move `RecordMoveAsync`, dedupe record | P1-T2, P1-T4 (a)-(d), P3-T3, P3-T5 (d), P5-T11 |
| AC-5 | Fail-closed errors: D5 matrix with `error.code` passthrough, 429/5xx retry exhaustion, `reschedule_failed` with no bookkeeping, local `NOT_SUPPORTED` zero-I/O | P1-T2, P1-T3, P1-T4 (e)-(g), P1-T5, P2-T2, P2-T3, P3-T5 (e), P5-T12 |
| AC-6 | Idempotency: re-evaluation after success audits `dedupe_skipped`, no Graph request | P3-T3, P3-T5 (f), P5-T13 |
| AC-7 | Mocked-Graph contract suite exists (`FakeHttpHandler`; method/URL/headers, exact body incl. absent-property guardrail, 200 mapping, D5 samples, 429 exhaustion under `FakeTimeProvider`) | P1-T4, P5-T14 |
| AC-8 | Quality gates: line >= 85% / branch >= 75%, no changed-line regression, >= 1 property per new pure function, architecture boundaries hold | P0-T5, P3-T6, P1-T6, P2-T4, P3-T8, P5-T1..P5-T6, P5-T15 |
| AC-9 | Live-verification runbook exists; `human_interaction` exception with `runbook_path` recorded for the orchestrator | P4-T1, P4-T2, P5-T16 |

## Test Plan

- **Contract (host-service boundary, mocked Graph):** `GraphHostAdapterClientRescheduleEventTests.cs` — PATCH method/route, headers, exact two-property body with absent-property guardrail (`JsonDocument` structural assertions), 200 -> `EventDto` mapping, D5 error samples with `BridgeErrorCode` passthrough, 429 exhaustion under `FakeTimeProvider`, unparseable/mapping-gap 2xx (P1-T4).
- **Adapter smoke negative:** local Stage-0 `NOT_SUPPORTED` fail-closed with zero HTTP invocations, in the new sibling file `HostAdapterHttpClientRescheduleTests.cs` — a test ADD; `HostAdapterHttpClientTests.cs` (616 lines, pre-existing cap violation) remains unmodified (P1-T5).
- **Service seam:** delegation, correlation-id forwarding, failure-throw contract, id guard clause (P2-T3).
- **Worker unit:** four-row gate truth table, dry-run detail, guard block, success ordering (audit -> move history -> dedupe), failure fail-closed, dedupe skip, no-intent silence, send-path `ActingFlags` isolation (P3-T5).
- **Property-based (CsCheck, T1 obligation):** duration preservation, eligibility monotonicity, flags-snapshot round-trip (P3-T6).
- **Architecture:** existing NetArchTest boundary suites pass unmodified — the new partial stays in `CloudGraph`; the worker partial stays host-neutral (P1-T6, P3-T8, P5-T5).
- **Regression:** pre-existing worker tests carry only mechanical mock additions; `SchedulingWorker.Audit.cs` / `BuildActingFlags` textually unmodified (P3-T7).
- **Integration:** deliberately none in CI — live-tenant verification is the recorded `human_interaction` exception (P4-T1, P4-T2).
- **Coverage evidence:** baseline `evidence/baseline/csharp-test-coverage.<ts>.md` (P0-T5); post-change `evidence/qa-gates/csharp-test-coverage.<ts>.md` (P5-T5); comparison `evidence/qa-gates/coverage-comparison.<ts>.md` (P5-T6). Raw intermediates in `artifacts/csharp/`.

## Open Questions / Notes

- **CSharpier command form:** this repo uses the global `csharpier` 1.3.0 executable (`csharpier format .` / `csharpier check .`); there is no local tool manifest, so the `dotnet csharpier` driver form supplied in the delegation prompt is replaced with the repository-actual command form.
- **Test stack note:** MSTest + FluentAssertions + Moq + CsCheck + `FakeTimeProvider` + `FakeHttpHandler` is the repository's actual stack (per the research and F13/F15 precedent), notwithstanding `.claude/rules/csharp.md`'s xUnit/NSubstitute wording.
- **Mutation testing (AC-8 adjacent):** Stryker.NET mutation score >= 75% on the changed T1 surface runs in the pre-merge/nightly pipeline per `.claude/rules/general-code-change.md` ("Mutation testing and golden tests run in pre-merge or nightly pipelines, not the per-commit loop"); it is not a task in this per-commit plan. The truth-table and fail-closed branches are the mutation-sensitive surface, so P3-T5 asserts both the action taken and the actions not taken.
- **Purview projection:** the optional explicit `MapActionType` case for `organizer-reschedule` is out of scope; the documented fallback (`UnknownActivity`/`Unknown`) covers the new action type without a change (research §1.5).
- **`SendOnBehalfAuthorizer` non-interaction:** the reschedule PATCH targets the principal's own calendar under app-only `Calendars.ReadWrite`; no mailbox representation occurs, so the allowlist is intentionally not consulted (spec Non-Goals). Reviewers must not flag its absence on this path.
- **Additive dry-run output:** today's pipeline computes no reschedule intent, so the dry-run log line and `reschedule_disabled` audit rows are new, additive output; "no behavior change" (AC-2) is scoped to outbound side effects — no Graph request, no write-path token acquisition, no `series_moves` row, no dedupe row — plus byte-identity of the existing send path's persisted `ActingFlags`.
- **Orchestrator checkpoint:** the `human_interaction` requirement (P4-T2) is recorded in `artifacts/orchestration/orchestrator-state.json` by the orchestrator; the executor produces the evidence record only.
- **Plan-path continuity:** this file (`plan.2026-07-07T07-30.md`) is the single canonical plan path; all preflight revisions update it in place with no timestamped siblings.
- **Preflight revision 1 (500-line-cap corrections, applied in place):** (1) P1-T5 converted from a modify of the 616-line `HostAdapterHttpClientTests.cs` (pre-existing cap violation; now explicitly unmodified) to a test ADD of the sibling `HostAdapterHttpClientRescheduleTests.cs`; (2) P2-T3 gained a split fallback to `HostAdapterSchedulingServiceRescheduleTests.cs` with a per-file cap acceptance; (3) P1-T4 gained a split fallback moving samples (e)-(g) to `GraphHostAdapterClientRescheduleEventErrorTests.cs` with a per-file cap acceptance. Global Constraints test scope, the Test Plan adapter-smoke line, and the P5-T1 enumerated file set were updated to match.

## Reconciliation Summary (P5-T17, 2026-07-07T04-01)

Split fallbacks actually applied during execution: P2-T3 -> `HostAdapterSchedulingServiceRescheduleTests.cs` (the 480-line `HostAdapterSchedulingServiceTests.cs` was left unmodified rather than extended); P3-T5 -> `SchedulingWorkerRescheduleEdgeTests.cs` (rows d-h). P1-T4 needed no split (348 lines); the `GraphHostAdapterClientRescheduleEventErrorTests.cs` fallback sibling was therefore not created.

### Task-to-artifact reconciliation

| Task | Status | Evidence / proof |
|---|---|---|
| P0-T1/T2 | done | evidence/baseline/phase0-instructions-read.md |
| P0-T3 | done | evidence/baseline/csharp-format.2026-07-07T04-01.md (EXIT 0) |
| P0-T4 | done | evidence/baseline/csharp-build.2026-07-07T04-01.md (0/0) |
| P0-T5 | done | evidence/baseline/csharp-test-coverage.2026-07-07T04-01.md (line 99.25%, branch 92.21%) |
| P1-T1..T5 | done | src + tests adds/mods; compiled and tested |
| P1-T6 | done | phase-1 loop clean; 868 Core tests pass |
| P2-T1..T3 | done | ISchedulingService + HostAdapterSchedulingService + HostAdapterSchedulingServiceRescheduleTests.cs |
| P2-T4 | done | phase-2 loop clean; 874 Core tests pass |
| P3-T1..T6 | done | ActionAuditResultCode/SentActionKey/SchedulingWorker ctor + Reschedule partial + Pipeline + worker/property tests |
| P3-T7 | done | evidence/regression-testing/send-path-regression-surface.2026-07-07T04-01.md (Audit.cs unchanged; mock-only test diffs) |
| P3-T8 | done | phase-3 loop clean; 892 Core tests pass, arch suites green |
| P4-T1 | OUTSTANDING | runbook not authored — orchestrator-owned handoff to the human-exception-runbook specialist |
| P4-T2 | done | evidence/other/human-interaction-record.2026-07-07T04-01.md |
| P5-T1 | done | evidence/qa-gates/file-size-cap.2026-07-07T04-01.md (max 457) |
| P5-T2 | done | evidence/qa-gates/test-hygiene.2026-07-07T04-01.md (0 violations) |
| P5-T3 | done | evidence/qa-gates/csharp-format.2026-07-07T04-01.md (EXIT 0) |
| P5-T4 | done | evidence/qa-gates/csharp-build.2026-07-07T04-01.md (0/0) |
| P5-T5 | done | evidence/qa-gates/csharp-test-coverage.2026-07-07T04-01.md (line 99.27%, branch 92.24%; 893 Core pass) |
| P5-T6 | done | evidence/qa-gates/coverage-comparison.2026-07-07T04-01.md (verdict PASS) |
| P5-T7 | done | evidence/other/diff-scope-verification.2026-07-07T04-01.md (scope exact; 0 prohibited-file changes) |
| P5-T8..T15 | done | AC-1..AC-8 checked in spec.md + user-story.md |
| P5-T16 | OUTSTANDING | AC-9 not checked — blocked on the P4-T1 runbook (orchestrator handoff) |
| P5-T17 | done | this summary |

### AC verdict

| AC | Verdict | Implementing/verifying tasks |
|---|---|---|
| AC-1 | PASS | P3-T3, P3-T5(a), P5-T8 |
| AC-2 | PASS | P3-T3/T4, P3-T5(a)(b)(h), P3-T7, P5-T9 |
| AC-3 | PASS | P3-T3, P3-T5(c), P5-T10 |
| AC-4 | PASS | P1-T2, P1-T4(a-d), P3-T3, P3-T5(d), P5-T11 |
| AC-5 | PASS | P1-T2/T3, P1-T4(e-g), P1-T5, P2-T2/T3, P3-T5(e), P5-T12 |
| AC-6 | PASS | P3-T3, P3-T5(f), P5-T13 |
| AC-7 | PASS | P1-T4, P5-T14 |
| AC-8 | PASS | P0-T5, P3-T6, P5-T1..T6, P5-T15 |
| AC-9 | OUTSTANDING | runbook (P4-T1) is an orchestrator-owned handoff; P4-T2 record exists. AC-9 checks off once the runbook file exists. |

Outcome: 8 of 9 AC delivered and verified. The remaining item (AC-9 / P4-T1 / P5-T16) is a single orchestrator-owned handoff — authoring the live-verification runbook — after which AC-9 checks off in both AC source files. All code, tests, quality gates, and evidence for AC-1..AC-8 are complete and green in a single clean toolchain pass.
