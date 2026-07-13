# Feature Audit — Issue #146 (message-to-event-linkage)

- Timestamp: 2026-07-12T22-25
- Reviewer: feature-review
- Feature branch: `feature/message-to-event-linkage-146`
- Base (merge-base): `origin/epic/openclaw-runtime-remediation-integration`
- Work mode: full-feature -> AC sources: `spec.md` (18 AC) AND `user-story.md` (8 AC); total 26
- Overall verdict: PASS

## Method

Each acceptance criterion was evaluated against the live branch diff, the committed tests, and the executor QA-gate evidence under `evidence/`. Every criterion in both source files was already checked `[x]` by the executor prior to this review; each is verified below and left checked where PASS. No box required a state change, and no PARTIAL/FAIL/UNVERIFIED criterion exists to uncheck.

## spec.md — Acceptance Criteria (18)

| # | Criterion (abridged) | Verdict | Evidence |
|---|---|---|---|
| S1 | `BridgeMethods.GetEventForMessage` const + `All` allow-list entry | PASS | `BridgeContracts.cs`: const `"get_event_for_message"` added and included in `All`. Allow-list gating verified by `PipeRpcWorker.BuildResponseAsync` dispatch arm. |
| S2 | `MessageDto.LinkedGlobalAppointmentId` nullable, positional-last, non-breaking, contract-covered | PASS | `BridgeContracts.cs`: appended after `MeetingMessageType` with `= null`. `BridgeContractsCoverageTests` (100% line/branch on contracts). |
| S3 | `messages` gains `linked_global_appointment_id TEXT NULL` via guarded ALTER; idempotent | PASS | `CacheRepository.Schema.cs`: DDL + `MessageFieldColumns` entry. `CacheRepositoryMigrationIdempotencyTests.InitializeAsync_should_add_linked_appointment_column_on_pre_146_messages_schema` + idempotency test. |
| S4 | `IBridgeRepository.GetEventForMessageAsync` decodes id, loads row, joins, newest instance | PASS | `CacheRepository.EventForMessage.cs`: decode guard, `ReadLinkedAppointmentKeyAsync`, `ResolveEventByGlobalAppointmentIdAsync` with `ORDER BY start_utc DESC LIMIT 1`. `CacheRepositoryEventForMessageTests` (6 tests incl. recurring newest-instance). |
| S5 | RPC returns `Success(EventDto)` linked / `Success(null)` unlinked; never `Failure(NOT_FOUND)` | PASS | `PipeRpcWorker.EventForMessage.cs`. `PipeRpcWorkerEventForMessageTests`: success-with-event, success-null, success-null-absent-row. |
| S6 | RPC returns `Failure(INVALID_REQUEST)` for malformed id | PASS | `PipeRpcWorker.EventForMessage.cs` decode-failure branch. `Handler_should_return_invalid_request_for_a_malformed_message_bridge_id`. |
| S7 | `get-event-for-message` CLI verb forwards required `id` | PASS | `MailBridge.Client/Program.cs`: `Req(id, BridgeMethods.GetEventForMessage, opts, "id")`. `MailBridgeProgramTests` (two verb tests). |
| S8 | HostAdapter route `GET /users/{id}/messages/{messageId}/event` registered, gated by ready-bridge + `TryGetBridgeId` | PASS | `MessageEventRoute.cs` + `Program.cs` `app.MapMessageEventRoute()`. `HostAdapterMessageEventRouteTests` (409 not-ready, 400 validation). |
| S9 | `HostAdapterCommandBuilder.BuildGetEventForMessage(bridgeId)` | PASS | `HostAdapterCommandBuilder.cs`. `BuildGetEventForMessage_should_produce_the_verb_with_the_id_option`. |
| S10 | Null-tolerant projector -> ok:true/data:null/200 (not 502); event->200 | PASS | `HostAdapterEventProjector.ProjectNullableEvent`. `ProjectNullableEvent_should_return_null_for_a_json_null_element`, `Route_should_return_200_with_data_null_for_an_unlinked_message`, `Route_should_return_200_with_event_for_a_linked_message`. |
| S11 | `IHostAdapterClient.GetEventForMessageAsync` declared, keyword-style optional params | PASS | `IHostAdapterClient.cs`: signature matches `GetEventAsync` (`string bridgeId, string? requestId = null, CancellationToken cancellationToken = default`). |
| S12 | Both `HostAdapterHttpClient` and `GraphHostAdapterClient` implement; parity; null contract; no NOT_SUPPORTED | PASS | `HostAdapterHttpClient.cs`, `GraphHostAdapterClient.Messages.cs`. `CloudGraphContractParityTests.ContractParity_BothImplementations_ExposeGetEventForMessageAsync` + `GetEventForMessageFlow_GraphBackend_HonorsTheNullContract`. |
| S13 | `HostAdapterSchedulingService.GetEventForMessageAsync` invokes the new client method (not `GetEventAsync`) + `{Ok:true,Data:not null}` guard | PASS | `HostAdapterSchedulingService.cs` rewire. `HostAdapterSchedulingServiceLinkageTests.GetEventForMessageAsync_InvokesLinkageMethod_NotGetEvent` (strict mock: linkage `Times.Once`, `GetEventAsync` `Times.Never`). |
| S14 | Unlinked -> clean null, no 400/404; `SchedulingWorker` degrades; linked hit skips window fallback | PASS | Core null mapping (`GetEventForMessageAsync_OkNull_ReturnsNull`); `SchedulingWorkerFallbackTests.RunCycle_LinkedHit_SkipsCalendarViewWindowFallback` and the window/empty-window fallback tests. |
| S15 | Malformed id -> HTTP 400 (INVALID_REQUEST) distinct from null path; bridge-not-ready -> 409 | PASS | `Route_should_map_downstream_invalid_request_to_400`, `Route_should_return_409_when_the_bridge_is_not_ready`. |
| S16 | Line >= 85% / branch >= 75% on changed code, no regression, no production file excluded | PASS | `coverage-delta-2026-07-12T22-10.md`: all changed/new files >= 85% line / >= 75% branch; every project >= baseline; only `[*.Tests]*` excluded. |
| S17 | Every changed/added file under 500-line cap; new logic in partials | PASS | `filesize-cap-2026-07-12T22-10.md`; max production 495 (`CacheRepository.cs`), max test 495 (`MailBridgeRuntimeTestDoubles.cs`); new logic in `*.EventForMessage.cs`/`*.Linkage.cs` partials. |
| S18 | Tests MSTest+Moq+FluentAssertions, injected `TimeProvider`/`FakeTimeProvider`, no wall-clock/sleeps, in-memory SQLite/seam fakes, no temp files | PASS | Confirmed by diff scan (no xUnit/NSubstitute/`Thread.Sleep`/`Task.Delay`/`DateTime.Now`); `FakeTimeProvider` in parity tests; in-memory SQLite `Mode=Memory;Cache=Shared`. |

## user-story.md — Acceptance Criteria (8)

| # | Criterion (abridged) | Verdict | Evidence |
|---|---|---|---|
| U1 | Pipeline resolves linked event via new RPC + route + `IHostAdapterClient` method | PASS | End-to-end path present (S1, S8, S11, S13). |
| U2 | Linkage joins stored `LinkedGlobalAppointmentId` to `events.global_appointment_id`, newest instance for recurring | PASS | `CacheRepository.EventForMessage.cs` join + newest-instance test (S4). |
| U3 | `HostAdapterSchedulingService.GetEventForMessageAsync` calls new client method (not `GetEventAsync`), returns mapped event | PASS | S13; `GetEventForMessageAsync_LinkedHit_ReturnsMappedEvent`. |
| U4 | Linked hit -> `SchedulingWorker` uses resolved event, skips calendar-view fallback | PASS | `RunCycle_LinkedHit_SkipsCalendarViewWindowFallback`. |
| U5 | Unlinked -> clean null via ok:true/data:null/200, no 400/404, continues fallback, no error telemetry | PASS | S5, S10, S14; no new error telemetry on the null path (verified in diff — no logging additions on the unlinked branch). |
| U6 | Malformed id -> HTTP 400 (INVALID_REQUEST); bridge-not-ready -> 409 | PASS | S15. |
| U7 | Both `HostAdapterHttpClient` and `GraphHostAdapterClient` implement consistently; satisfy parity tests | PASS | S12. |
| U8 | Coverage line >= 85% / branch >= 75% on changed code, no regression, MSTest+Moq+FluentAssertions | PASS | S16, S18. |

## Baseline Relationship

The change is measured against `origin/epic/openclaw-runtime-remediation-integration`. The prior behavior (`GetEventForMessageAsync` forwarding `messageId` to `GetEventAsync`, always missing) is fully replaced by a dedicated linkage path. No regression on changed lines; every project's coverage is >= baseline.

## Check-Off Report

- `spec.md`: all 18 AC boxes were already `[x]` (checked by the executor at `spec-ac-checkoff-2026-07-12T22-10.md`). All 18 verified PASS; left checked. Newly checked by reviewer: 0.
- `user-story.md`: all 8 AC boxes were already `[x]` (`user-story-ac-checkoff-2026-07-12T22-10.md`). All 8 verified PASS; left checked. Newly checked by reviewer: 0.

## Acceptance Criteria Status

- Source: `docs/features/active/2026-07-11-message-to-event-linkage-146/spec.md`; `docs/features/active/2026-07-11-message-to-event-linkage-146/user-story.md`
- Total AC items: 26 (18 spec + 8 user-story)
- Checked off (delivered): 26
- Remaining (unchecked): 0
- Items remaining: none

## Summary

- Blocking findings: 0.
- Overall feature-audit verdict: **PASS.**
