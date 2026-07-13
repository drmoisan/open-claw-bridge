# spec.md Acceptance-Criteria Verification Map — Issue #146

Timestamp: 2026-07-12T22-10
Source: docs/features/active/2026-07-11-message-to-event-linkage-146/spec.md (## Acceptance Criteria, 18 items)
All 18 criteria marked [x]; each is supported by a test, gate artifact, or code assertion.

| # | Criterion (abridged) | Supporting evidence |
|---|---|---|
| 1 | BridgeMethods.GetEventForMessage const + in All allow-list | src BridgeContracts.cs (const `get_event_for_message` + All); BridgeContractsCoverageTests Bridge_methods_all_should_contain_get_event_for_message_verb |
| 2 | MessageDto.LinkedGlobalAppointmentId positional-last, null default, non-breaking, covered | src BridgeContracts.cs; BridgeContractsCoverageTests (field default null + `with` set); build succeeded (no positional caller break) |
| 3 | messages linked_global_appointment_id column via guarded-ALTER, idempotent | CacheRepository.Schema.cs (MessageFieldColumns entry + DDL); CacheRepositoryMigrationIdempotencyTests InitializeAsync_should_add_linked_appointment_column_on_pre_146_messages_schema |
| 4 | IBridgeRepository.GetEventForMessageAsync decode+load+join on global_appointment_id, newest instance | CacheRepository.EventForMessage.cs; CacheRepositoryEventForMessageTests (linked hit, recurring newest-instance) |
| 5 | RPC handler Success(EventDto) linked / Success(null) unlinked, never Failure(NOT_FOUND) | PipeRpcWorker.EventForMessage.cs; PipeRpcWorkerEventForMessageTests (success-event, success-null, absent-row null) |
| 6 | RPC handler Failure(INVALID_REQUEST) for malformed id | PipeRpcWorker.EventForMessage.cs; PipeRpcWorkerEventForMessageTests Handler_should_return_invalid_request_for_a_malformed_message_bridge_id |
| 7 | MailBridge.Client get-event-for-message verb forwards required id | Client Program.cs Build arm; MailBridgeProgramTests Build_WhenCommandIsGetEventForMessage_WithId/_WithMissingId |
| 8 | HostAdapter route GET /users/{id}/messages/{messageId}/event, ready-bridge + TryGetBridgeId gated | MessageEventRoute.cs; HostAdapterMessageEventRouteTests (route 200/409) |
| 9 | HostAdapterCommandBuilder.BuildGetEventForMessage | HostAdapterCommandBuilder.cs; HostAdapterMessageEventRouteTests BuildGetEventForMessage_should_produce_the_verb_with_the_id_option |
| 10 | Null-tolerant projector: ok/null->200 data:null; ok/event->200 data:event | HostAdapterEventProjector.cs; HostAdapterMessageEventRouteTests (projector null/object; route data:null and data:event) |
| 11 | IHostAdapterClient.GetEventForMessageAsync declared with keyword-style optional params | IHostAdapterClient.cs (signature matches GetEventAsync) |
| 12 | Both HttpClient and Graph implement it; parity; null contract; no NOT_SUPPORTED | HostAdapterHttpClient.cs, GraphHostAdapterClient.Messages.cs; CloudGraphContractParityTests ContractParity_BothImplementations_ExposeGetEventForMessageAsync + GetEventForMessageFlow_GraphBackend_HonorsTheNullContract; HostAdapterHttpClientSchedulingTests; GraphHostAdapterClientMessagesTests |
| 13 | HostAdapterSchedulingService invokes GetEventForMessageAsync (not GetEventAsync), guard | HostAdapterSchedulingService.cs; HostAdapterSchedulingServiceLinkageTests (linked hit, ok-null, invoked-method/not-GetEvent) |
| 14 | Unlinked -> clean null in Core, no 400/404, worker degrades to fallback; linked hit skips fallback | SchedulingWorkerFallbackTests RunCycle_LinkedHit_SkipsCalendarViewWindowFallback + existing lookup-miss fallback tests; HostAdapterSchedulingServiceLinkageTests OkNull_ReturnsNull |
| 15 | Malformed id -> HTTP 400 distinct from null path; not-ready -> 409 | HostAdapterMessageEventRouteTests Route_should_map_downstream_invalid_request_to_400 + Route_should_return_409_when_the_bridge_is_not_ready |
| 16 | Changed-code line >= 85%, branch >= 75%, no regression, no exclusion | evidence/qa-gates/coverage-delta-2026-07-12T22-10.md (PASS) |
| 17 | Every changed/added file under 500-line cap | evidence/qa-gates/filesize-cap-2026-07-12T22-10.md (PASS) |
| 18 | Tests MSTest+Moq+FluentAssertions, FakeTimeProvider, in-memory SQLite/seam fakes, no temp files | All new test files use MSTest [TestClass]/[TestMethod] + FluentAssertions + Moq; repository tests use in-memory shared-cache SQLite; COM tests use hand-written reflection doubles; no temp files; no wall-clock reads |

Outcome: all 18 spec.md acceptance criteria verified and checked off.
