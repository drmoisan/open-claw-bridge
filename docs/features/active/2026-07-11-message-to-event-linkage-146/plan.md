# Plan — message-to-event-linkage (Issue #146)

- Feature: `docs/features/active/2026-07-11-message-to-event-linkage-146/`
- Work Mode: full-feature
- AC sources: `spec.md` (18 acceptance criteria) AND `user-story.md` (8 acceptance criteria)
- Language: C# (MSTest + Moq + FluentAssertions; CsCheck; NetArchTest; Microsoft.Extensions.TimeProvider.Testing)
- Tier map: OpenClaw.Core = T1, OpenClaw.HostAdapter = T1; OpenClaw.MailBridge.Contracts = T2, OpenClaw.HostAdapter.Contracts = T2, OpenClaw.MailBridge = T2; OpenClaw.MailBridge.Client = T3
- Coverage policy (uniform): line >= 85%, branch >= 75%; no regression on changed lines; no production file excluded from coverage

## Toolchain (authoritative)

- Format check: `csharpier check .`
- Format apply (per-task gate): `csharpier format .`
- Lint / type-check (build with analyzers + nullable-as-error): `dotnet build OpenClaw.MailBridge.sln`
- Test with coverage: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
- Architecture-boundary tests (NetArchTest) run inside the test projects and are exercised by the test command above.

Note on CSharpier command form: this repository has no local dotnet-tool manifest; CSharpier is the global 1.x CLI, which uses subcommands. Format tasks therefore use `csharpier check .` / `csharpier format .`, not the `dotnet csharpier` driver. This corrects the `csharpier .` form supplied in the delegation toolchain.

## Evidence conventions (non-overridable)

- All evidence artifacts resolve under `docs/features/active/2026-07-11-message-to-event-linkage-146/evidence/<kind>/`.
- Baseline evidence: `evidence/baseline/`. QA-gate / final-QC evidence: `evidence/qa-gates/`. Issue/AC mirrors: `evidence/issue-updates/`.
- Each command-step evidence artifact is a markdown file containing: `Timestamp:` (ISO-8601 `yyyy-MM-ddTHH-mm`), `Command:`, `EXIT_CODE:`, `Output Summary:`.
- Raw command intermediates (coverage `coverage.cobertura.xml`, TRX, build logs) are retained under `artifacts/csharp/`; the summarizing markdown that cites their numeric results lives under `evidence/<kind>/`. `artifacts/csharp/` is a non-evidence intermediate store, not an evidence path.
- No `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/coverage/`, `artifacts/evidence/`, or `artifacts/regression-testing/` path is used for evidence output.

## File-size cap constraints (500 lines)

- `OutlookScanner.cs` is at its cap: new scanner logic goes into a scanner partial, not `OutlookScanner.cs`.
- `PipeRpcWorker.cs` (~438) is near the cap: the new dispatch handler goes into a `PipeRpcWorker` partial.
- `CacheRepository.cs` (~480) is near the cap: the new repository resolution method goes into a `CacheRepository` partial (or `.Readers.cs`), not `CacheRepository.cs`.

### Phase 0 - Policy Reads and C# Baseline Capture

- [x] [P0-T1] Read the repository policy files in the required order and record the read in `evidence/baseline/phase0-instructions-read.md`.
  - AC: Artifact exists with `Timestamp:`, `Policy Order:`, and an explicit file list covering `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/quality-tiers.md`, and `.claude/rules/csharp.md`, in that order.
- [x] [P0-T2] Capture baseline format state by running `csharpier check .` and record `evidence/baseline/baseline-format-<yyyy-MM-ddTHH-mm>.md`.
  - AC: Artifact records `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, and `Output Summary:` stating pass/fail and count of files needing formatting.
- [x] [P0-T3] Capture baseline build state by running `dotnet build OpenClaw.MailBridge.sln` and record `evidence/baseline/baseline-build-<yyyy-MM-ddTHH-mm>.md`; retain the raw build log under `artifacts/csharp/`.
  - AC: Artifact records `Timestamp:`, `Command: dotnet build OpenClaw.MailBridge.sln`, `EXIT_CODE:`, and `Output Summary:` stating warning/error counts.
- [x] [P0-T4] Capture baseline test-with-coverage state by running `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and record `evidence/baseline/baseline-test-coverage-<yyyy-MM-ddTHH-mm>.md`; retain the raw `coverage.cobertura.xml` and TRX under `artifacts/csharp/`.
  - AC: Artifact records `Timestamp:`, `Command:` (exact), `EXIT_CODE:`, and `Output Summary:` including numeric baseline line-coverage % and branch-coverage % read from the cobertura report, plus passed/failed test counts.

### Phase 1 - MailBridge.Contracts: RPC method const and MessageDto linkage field

- [ ] [P1-T1] Add `public const string GetEventForMessage` to `BridgeMethods` and add the same value to the `BridgeMethods.All` allow-list in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`.
  - AC: The const exists and its value is present in `All`; a method name absent from `All` is still rejected by `PipeRpcWorker.BuildResponseAsync` (unchanged behavior preserved).
- [ ] [P1-T2] Append a nullable `string? LinkedGlobalAppointmentId` field to the `MessageDto` record in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` as the last positional parameter with a `null` default.
  - AC: `MessageDto` compiles with the new field positional-last and defaulting to `null`; all existing positional callers still compile without change (non-breaking).
- [ ] [P1-T3] Extend `tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs` to assert the new `BridgeMethods.GetEventForMessage` const/`All` membership and the new `MessageDto.LinkedGlobalAppointmentId` field default.
  - AC: New assertions fail if the const is removed from `All` or the field default is not `null`; tests pass against the Phase 1 implementation.
- [ ] [P1-T4] Run the C# toolchain loop for Phase 1: `csharpier format .`, then `dotnet build OpenClaw.MailBridge.sln`, then `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; restart from format if any step changes files or fails.
  - AC: Format, build (0 errors), and tests all pass in a single clean pass.

### Phase 2 - MailBridge: migration, repository join, RPC handler, scanner seam, CLI verb

- [ ] [P2-T1] Add `linked_global_appointment_id TEXT NULL` to the `messages` DDL and to the `MessageFieldColumns` guarded-ALTER array consumed by `MigrateMessagesSchemaAsync` in `src/OpenClaw.MailBridge/CacheRepository.Schema.cs`.
  - AC: A fresh database creates the column; running the migration against a database that already has the column does not error or duplicate it (idempotent).
- [ ] [P2-T2] Wire the new column into INSERT/UPSERT of the `messages` row in `src/OpenClaw.MailBridge/CacheRepository.cs` (writing `LinkedGlobalAppointmentId`, `NULL` when absent), keeping the file under 500 lines by moving overflow into a `CacheRepository` partial if needed.
  - AC: An inserted meeting message persists its `LinkedGlobalAppointmentId`; ordinary mail persists `NULL`; `CacheRepository.cs` remains under 500 lines.
- [ ] [P2-T3] Read the new column back in the message reader in `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` so `MessageDto.LinkedGlobalAppointmentId` round-trips.
  - AC: Reading a persisted message returns the stored linkage key (or `null`) on `MessageDto.LinkedGlobalAppointmentId`.
- [ ] [P2-T4] Add `Task<EventDto?> GetEventForMessageAsync(string messageBridgeId, CancellationToken cancellationToken = default)` to `IBridgeRepository` and implement it in a new `src/OpenClaw.MailBridge/CacheRepository.EventForMessage.cs` partial: decode the message bridge id, load the message row, read its linked key, and resolve via `SELECT ... FROM events WHERE global_appointment_id = $key ORDER BY start_utc DESC LIMIT 1`.
  - AC: A linked message returns the matching `EventDto`; an unlinked message, an absent message row, and a key that matches no event each return `null`; a recurring series returns the newest instance by `start_utc DESC`; the new file is under 500 lines.
- [ ] [P2-T5] Add a nullable linked-key member (e.g. `string? LinkedGlobalAppointmentId`) to the `IMessageSource` seam in `src/OpenClaw.MailBridge/IMessageSource.cs`.
  - AC: The seam exposes the linked-key member with no COM types on its surface (architecture-boundary rule preserved).
- [ ] [P2-T6] Implement the linked-key read fail-soft in `src/OpenClaw.MailBridge/ComMessageSource.cs` via `GetAssociatedAppointment` then `GlobalAppointmentID`, releasing the appointment wrapper in a `finally` block; return `null` on any failure or for non-meeting items.
  - AC: A meeting item yields the appointment `GlobalAppointmentID`; a non-meeting item or a COM failure yields `null` with the wrapper released; no exception escapes the read.
- [ ] [P2-T7] Populate `LinkedGlobalAppointmentId` for meeting items in `OutlookScanner.NormalizeMessage` via a new scanner partial (e.g. `src/OpenClaw.MailBridge/OutlookScanner.Linkage.cs`), reading through the `IMessageSource` seam; ordinary mail yields `null`. Do not add lines to the capped `OutlookScanner.cs`.
  - AC: A normalized meeting message carries the linked key; ordinary mail carries `null`; `OutlookScanner.cs` line count is unchanged and the new partial is under 500 lines.
- [ ] [P2-T8] Set the sensitive-message default for the linked key in `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` (`NormalizeSensitiveMessage`) to `null` unless the issue-#18 never-ingest ordering confirms retention is safe.
  - AC: The sensitive-message path sets `LinkedGlobalAppointmentId` to `null` (safer default) and the decision is noted in a code comment referencing issue #18.
- [ ] [P2-T9] Add the `BridgeMethods.GetEventForMessage` switch arm and `HandleGetEventForMessageAsync` in a new `src/OpenClaw.MailBridge/PipeRpcWorker.EventForMessage.cs` partial: decode via `BridgeIdCodec.TryDecodeMessageId`, return `RpcResponse.Failure(id, INVALID_REQUEST, ...)` on decode failure, else call the repository and return `RpcResponse.Success(id, event-or-null)`. Do not add lines to the capped `PipeRpcWorker.cs`.
  - AC: A linked id returns `Success(id, EventDto)`; an unlinked/absent id returns `Success(id, null)` (never `Failure(NOT_FOUND)`); a malformed id returns `Failure(id, INVALID_REQUEST, ...)`; the new partial is under 500 lines.
- [ ] [P2-T10] Add a `get-event-for-message` verb to `src/OpenClaw.MailBridge.Client/Program.cs` in `Build`, mapping to `Req(id, BridgeMethods.GetEventForMessage, opts, "id")`, mirroring `get-event`.
  - AC: The verb forwards the required `id` option to `BridgeMethods.GetEventForMessage`; a missing `id` is rejected as required.
- [ ] [P2-T11] Add MSTest + FluentAssertions tests under `tests/OpenClaw.MailBridge.Tests/` covering: repository linked hit, ordinary-mail null, no-matching-event null, absent-message-row null, recurring newest-instance selection (in-memory SQLite); handler success-event / success-null / malformed-id via `BuildResponseAsync`; migration idempotency (extend `CacheRepositoryMigrationIdempotencyTests.cs`); message-field round-trip (extend `CacheRepositoryMessageFieldsTests.cs`); and `ComMessageSource` fail-soft (hand-written COM doubles, following `ComMessageSourceTests`).
  - AC: All added tests pass; each new/modified test file is under 500 lines; no temporary files and no wall-clock reads/sleeps are used.
- [ ] [P2-T12] Run the C# toolchain loop for Phase 2: `csharpier format .`, then `dotnet build OpenClaw.MailBridge.sln`, then `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; restart from format if any step changes files or fails.
  - AC: Format, build (0 errors), and tests all pass in a single clean pass.

### Phase 3 - HostAdapter: route, command builder, null-tolerant projector

- [ ] [P3-T1] Add `BuildGetEventForMessage(string bridgeId)` to `src/OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs`, mirroring `BuildGetEvent`/`BuildGetMessage` (`CreateBaseStartInfo("get-event-for-message")` + `AddOption("id", bridgeId)`).
  - AC: The builder produces a `ProcessStartInfo` invoking the `get-event-for-message` verb with the `id` option set to the supplied bridge id.
- [ ] [P3-T2] Add a null-tolerant `EventDto` projector helper for `ExecuteAsync` in `src/OpenClaw.HostAdapter/` (e.g. `HostAdapterEventProjector.cs`) that returns `null` when the JSON element is `JsonValueKind.Null` and otherwise defers to `DeserializePayload<EventDto>`.
  - AC: The projector returns `null` for a JSON null element without throwing and returns a deserialized `EventDto` for an object element.
- [ ] [P3-T3] Register the route `GET /users/{id}/messages/{messageId}/event` in `src/OpenClaw.HostAdapter/Program.cs` (or a scheduling-routes partial) following the `GET /users/{id}/messages/{messageId}` pattern: request id, `RequireReadyBridgeAsync<EventDto>` gate, `TryGetBridgeId` validation, `HostAdapterCommandBuilder.BuildGetEventForMessage`, `processRunner.ExecuteAsync<EventDto>(cmd, requestId, bridge, nullTolerantProjector, ct)`, then `ToHttpResult`.
  - AC: The route is registered and returns `ok:true`/`data:event`/200 for a linked event, `ok:true`/`data:null`/200 for an unlinked result (not 502), 400 `INVALID_REQUEST` for a malformed id, and 409 for bridge-not-ready; the touched file stays under 500 lines.
- [ ] [P3-T4] Add MSTest + Moq tests under `tests/OpenClaw.HostAdapter.Tests/` (extend `HostAdapterEndpointTests.cs`, `HostAdapterProcessRunnerTests.cs`, and command-builder/mapping tests) covering: ok RPC-null -> 200 `data:null`; ok RPC-event -> 200 `data:event`; malformed id -> 400; not-ready -> 409; command-builder verb/option; and projector null-tolerance.
  - AC: All added tests pass with a fake `IHostAdapterProcessRunner` (no child process spawned); each test file is under 500 lines.
- [ ] [P3-T5] Run the C# toolchain loop for Phase 3: `csharpier format .`, then `dotnet build OpenClaw.MailBridge.sln`, then `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; restart from format if any step changes files or fails.
  - AC: Format, build (0 errors), and tests all pass in a single clean pass.

### Phase 4 - HostAdapter.Contracts client method and both Core implementations

- [ ] [P4-T1] Declare `Task<ApiEnvelope<EventDto>> GetEventForMessageAsync(string bridgeId, string? requestId = null, CancellationToken cancellationToken = default)` on `IHostAdapterClient` in `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs`, with keyword-style optional params matching `GetEventAsync`.
  - AC: The interface method is declared with the exact signature; the solution does not yet build until both implementations are added (bound by parity).
- [ ] [P4-T2] Implement `GetEventForMessageAsync` in `src/OpenClaw.Core/HostAdapterHttpClient.cs` as a real `SendAsync<EventDto>($"users/{id}/messages/{Uri.EscapeDataString(bridgeId)}/event", ...)`, honoring the null contract.
  - AC: The method issues an HTTP GET to the linked-event route, deserializes a 200 body into `ApiEnvelope<EventDto>` (including `Data == null`), and returns it without throwing on the null case.
- [ ] [P4-T3] Implement `GetEventForMessageAsync` in `GraphHostAdapterClient` (extend `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.Messages.cs` or add a partial) consistently with the null contract, returning an `ok:true`/`data:null` envelope rather than a `NOT_SUPPORTED` error for the read path.
  - AC: The Graph implementation returns a structurally valid `ApiEnvelope<EventDto>` (event or null) and never emits `NOT_SUPPORTED` for this read; the touched file stays under 500 lines.
- [ ] [P4-T4] Add MSTest + Moq tests under `tests/OpenClaw.Core.Tests/` covering `HostAdapterHttpClient` URL construction (extend `HostAdapterHttpClientSchedulingTests.cs`), the Graph implementation (extend `CloudGraph/GraphHostAdapterClientMessagesTests.cs`), and the parity gate (`CloudGraph/CloudGraphContractParityTests.cs`).
  - AC: `CloudGraphContractParityTests` recognizes the new method on both implementations and passes; URL-construction and Graph null-contract tests pass; each test file is under 500 lines.
- [ ] [P4-T5] Run the C# toolchain loop for Phase 4: `csharpier format .`, then `dotnet build OpenClaw.MailBridge.sln`, then `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; restart from format if any step changes files or fails.
  - AC: Format, build (0 errors), and tests all pass in a single clean pass.

### Phase 5 - Core rewire: HostAdapterSchedulingService

- [ ] [P5-T1] Rewire `GetEventForMessageAsync` in `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (lines 33-45) to call `hostAdapterClient.GetEventForMessageAsync(messageId, cancellationToken: ct)` and apply the existing `envelope is { Ok: true, Data: not null } ? mapper.MapEvent(envelope.Data) : null` guard, removing the `GetEventAsync` forward and the deferred-work comment.
  - AC: The method invokes `GetEventForMessageAsync` (not `GetEventAsync`), returns the mapped `SchedulingEventDto` on a linked hit, and returns `null` on `ok:true`/`data:null`.
- [ ] [P5-T2] Add/extend MSTest + Moq tests under `tests/OpenClaw.Core.Tests/`: `Agent/Runtime/HostAdapterSchedulingServiceTests.cs` (data:null -> null, data:event -> mapped DTO, and verification that `GetEventForMessageAsync` is the invoked method) and `Agent/Runtime/SchedulingWorkerFallbackTests.cs` (a linked hit skips the calendar-view window fallback; a null result uses it). Use `FakeTimeProvider` where a clock is needed.
  - AC: All added tests pass; the invoked-method assertion fails if the code calls `GetEventAsync`; no wall-clock reads or sleeps; each test file is under 500 lines.
- [ ] [P5-T3] Run the C# toolchain loop for Phase 5: `csharpier format .`, then `dotnet build OpenClaw.MailBridge.sln`, then `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; restart from format if any step changes files or fails.
  - AC: Format, build (0 errors), and tests all pass in a single clean pass.

### Phase 6 - Final QC, Coverage Delta, and Acceptance-Criteria Checkoff

- [ ] [P6-T1] Run the final format gate `csharpier check .` and record `evidence/qa-gates/finalqc-format-<yyyy-MM-ddTHH-mm>.md`.
  - AC: Artifact records `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, and `Output Summary:` confirming 0 files need formatting.
- [ ] [P6-T2] Run the final build gate `dotnet build OpenClaw.MailBridge.sln` and record `evidence/qa-gates/finalqc-build-<yyyy-MM-ddTHH-mm>.md`; retain the raw build log under `artifacts/csharp/`.
  - AC: Artifact records `Timestamp:`, `Command: dotnet build OpenClaw.MailBridge.sln`, `EXIT_CODE:`, and `Output Summary:` confirming 0 errors and 0 nullable/analyzer warnings-as-errors.
- [ ] [P6-T3] Run the final test-with-coverage gate `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and record `evidence/qa-gates/finalqc-test-coverage-<yyyy-MM-ddTHH-mm>.md`; retain the raw `coverage.cobertura.xml` and TRX under `artifacts/csharp/`.
  - AC: Artifact records `Timestamp:`, `Command:` (exact), `EXIT_CODE:`, and `Output Summary:` including numeric post-change line-coverage %, branch-coverage %, and passed/failed test counts; all tests pass.
- [ ] [P6-T4] Compute and record the coverage delta and threshold verification in `evidence/qa-gates/coverage-delta-<yyyy-MM-ddTHH-mm>.md`, reporting baseline coverage (from P0-T4), post-change coverage (from P6-T3), and new/changed-code coverage for the changed files across the five projects.
  - AC: Artifact reports baseline line/branch %, post-change line/branch %, and new/changed-code line/branch %; confirms line >= 85%, branch >= 75%, and no regression on changed lines; if any threshold is unmet the outcome is recorded as remediation-required (not PASS).
- [ ] [P6-T5] Verify the 500-line file cap for every added or changed source and test file and record the result in `evidence/qa-gates/filesize-cap-<yyyy-MM-ddTHH-mm>.md`.
  - AC: Artifact lists each added/changed file with its line count and confirms all are under 500 lines, including `OutlookScanner.cs`, `PipeRpcWorker.cs`, and `CacheRepository.cs` (new logic confirmed in partials).
- [ ] [P6-T6] Check off the 18 `spec.md` acceptance criteria against verified evidence per the acceptance-criteria-tracking skill, updating the `## Acceptance Criteria` checkboxes in `docs/features/active/2026-07-11-message-to-event-linkage-146/spec.md` and mirroring the verification map to `evidence/qa-gates/spec-ac-checkoff-<yyyy-MM-ddTHH-mm>.md`.
  - AC: All 18 spec.md criteria are marked `[x]` only where a test, gate artifact, or code assertion supports them; the mirror artifact maps each criterion to its supporting evidence.
- [ ] [P6-T7] Check off the 8 `user-story.md` acceptance criteria against verified evidence per the acceptance-criteria-tracking skill, updating the `## Acceptance Criteria` checkboxes in `docs/features/active/2026-07-11-message-to-event-linkage-146/user-story.md` and mirroring the verification map to `evidence/qa-gates/user-story-ac-checkoff-<yyyy-MM-ddTHH-mm>.md`.
  - AC: All 8 user-story.md criteria are marked `[x]` only where supporting evidence exists; the mirror artifact maps each criterion to its supporting evidence.
