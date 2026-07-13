# user-story.md Acceptance-Criteria Verification Map — Issue #146

Timestamp: 2026-07-12T22-10
Source: docs/features/active/2026-07-11-message-to-event-linkage-146/user-story.md (## Acceptance Criteria, 8 items)
All 8 criteria marked [x]; each is supported by a test, gate artifact, or code assertion.

| # | Criterion (abridged) | Supporting evidence |
|---|---|---|
| 1 | Pipeline resolves linked event via new RPC, new HostAdapter route, and new client method | BridgeContracts.cs const; MessageEventRoute.cs route; IHostAdapterClient.GetEventForMessageAsync; PipeRpcWorkerEventForMessageTests + HostAdapterMessageEventRouteTests + HostAdapterHttpClientSchedulingTests |
| 2 | Linkage joins stored LinkedGlobalAppointmentId to events.global_appointment_id, newest instance for recurring | CacheRepository.EventForMessage.cs (ORDER BY start_utc DESC LIMIT 1); CacheRepositoryEventForMessageTests recurring-series newest-instance |
| 3 | HostAdapterSchedulingService calls the new client method (not GetEventAsync) and returns mapped event | HostAdapterSchedulingService.cs; HostAdapterSchedulingServiceLinkageTests InvokesLinkageMethod_NotGetEvent + LinkedHit_ReturnsMappedEvent |
| 4 | A linked hit causes SchedulingWorker to use the resolved event and skip the window fallback | SchedulingWorker.Pipeline.cs (fallback guarded by `meetingEvent is null`); SchedulingWorkerFallbackTests RunCycle_LinkedHit_SkipsCalendarViewWindowFallback |
| 5 | Unlinked message -> clean null via ok:true/data:null/200, no 400/404; pipeline continues via fallback, no error telemetry | PipeRpcWorkerEventForMessageTests success-null; HostAdapterMessageEventRouteTests Route_should_return_200_with_data_null; HostAdapterSchedulingServiceLinkageTests OkNull_ReturnsNull; existing fallback tests |
| 6 | Malformed message bridge id -> HTTP 400 INVALID_REQUEST distinct from null path; not-ready -> 409 | HostAdapterMessageEventRouteTests Route_should_map_downstream_invalid_request_to_400 + Route_should_return_409_when_the_bridge_is_not_ready; PipeRpcWorkerEventForMessageTests malformed-id |
| 7 | Both HttpClient and Graph implement the method consistently with the null contract; satisfy parity | HostAdapterHttpClient.cs + GraphHostAdapterClient.Messages.cs; CloudGraphContractParityTests (parity + Graph null-contract) |
| 8 | Coverage on changed C# code meets thresholds (line >= 85%, branch >= 75%) no regression, MSTest+Moq+FluentAssertions | evidence/qa-gates/coverage-delta-2026-07-12T22-10.md (PASS); all tests authored with MSTest + Moq + FluentAssertions |

Outcome: all 8 user-story.md acceptance criteria verified and checked off.
