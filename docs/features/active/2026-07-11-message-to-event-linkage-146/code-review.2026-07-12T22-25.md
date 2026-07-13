# Code Review — Issue #146 (message-to-event-linkage)

- Timestamp: 2026-07-12T22-25
- Reviewer: feature-review
- Feature branch: `feature/message-to-event-linkage-146`
- Base (merge-base): `origin/epic/openclaw-runtime-remediation-integration`
- Scope: full branch diff (51 files; +1967 / -97), C# only
- Overall verdict: PASS

## Load-Bearing Invariant: Unlinked-Message Null Contract (end-to-end)

Independently traced across all three layers; the contract is preserved.

1. RPC layer — `src/OpenClaw.MailBridge/PipeRpcWorker.EventForMessage.cs`:
   - Malformed message bridge id -> `RpcResponse.Failure(req.Id, BridgeErrorCodes.InvalidRequest, ...)` (maps to HTTP 400).
   - Decodable but unlinked (ordinary mail, absent row, no matching event) -> `RpcResponse.Success(req.Id, null)`. Never `Failure(NOT_FOUND)`.
   - Linked -> `RpcResponse.Success(req.Id, ResponseShaper.ShapeEvent(evt, settings))`, matching the existing `get_event` shaping path.
   - Repository (`CacheRepository.EventForMessage.cs`) returns `null` for a null/absent linkage key or a key that matches no event; it also defensively returns `null` (not throw) on a malformed id.

2. HostAdapter route — `src/OpenClaw.HostAdapter/MessageEventRoute.cs` + `HostAdapterEventProjector.cs`:
   - Uses the null-tolerant projector `ProjectNullableEvent`, which maps a JSON-null payload element to a `null` `EventDto` instead of throwing a `JsonException` (which the process runner would surface as a 502 TRANSPORT_FAILURE). This is the specific hazard called out in the spec, and it is correctly avoided.
   - Route order: request id -> `RequireReadyBridgeAsync<EventDto?>` (409 when not ready) -> `TryGetBridgeId` (validation) -> `BuildGetEventForMessage` -> `ExecuteAsync<EventDto?>` with the null-tolerant projector -> `ToHttpResult`. This mirrors the existing message route.
   - Verified by `HostAdapterMessageEventRouteTests`: 200/data:null, 200/event, 400 (downstream INVALID_REQUEST), 409 (not-ready gate fires before the CLI verb is invoked).

3. Core consumer — `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`:
   - Rewired from `GetEventAsync(messageId, ct)` (the messageId-as-eventId stand-in) to `hostAdapterClient.GetEventForMessageAsync(messageId, cancellationToken: ct)`.
   - Applies the `{ Ok: true, Data: not null }` guard, returning `mapper.MapEvent(envelope.Data)` on a linked hit and `null` otherwise.
   - Verified by `HostAdapterSchedulingServiceLinkageTests` using `MockBehavior.Strict`: `GetEventForMessageAsync` called `Times.Once`, `GetEventAsync` called `Times.Never` — a direct regression guard against reverting to the stand-in.

Conclusion: an unlinked message yields `ok:true` / `data:null` / HTTP 200 at the route and `null` in Core, so `SchedulingWorker` degrades to the calendar-view fallback. A malformed id is a clean HTTP 400 distinct from degradation. The invariant holds end to end.

## Correctness and Design

| Item | Assessment |
|---|---|
| Linkage join | Exact-string equality of `messages.linked_global_appointment_id` to `events.global_appointment_id`, newest instance via `ORDER BY start_utc DESC LIMIT 1` (matches `ListCalendarWindow` ordering). Correct and covered by `GetEventForMessage_should_select_the_newest_instance_for_a_recurring_series`. |
| Schema / migration | `linked_global_appointment_id TEXT NULL` added to the `CREATE TABLE` DDL and to the `MessageFieldColumns` guarded-ALTER array, so new and pre-existing databases converge. Idempotency and pre-146 add covered by `CacheRepositoryMigrationIdempotencyTests`. |
| INSERT/UPSERT/read wiring | Column added consistently to the INSERT column list, VALUES list, `ON CONFLICT DO UPDATE SET`, `AddMessageParameters` (with `DBNull` for null), and `ReadMessage`. Round-trip is complete. |
| Contract additivity | `MessageDto.LinkedGlobalAppointmentId` appended positional-last with `null` default; `BridgeMethods.GetEventForMessage` added to `All` allow-list. Non-breaking; consistent with prior #72/#73 additions. |
| Cloud parity | `GraphHostAdapterClient.GetEventForMessageAsync` returns a structurally valid `ok:true`/`data:null` envelope (no `NOT_SUPPORTED`), preserving the null contract on the cloud backend where linkage is a bridge-cache concept with no single Graph round-trip. `CloudGraphContractParityTests` gates both implementations via interface-map reflection and a Graph-backend null-contract flow test. |
| COM boundary | `ComMessageSource.ResolveLinkedGlobalAppointmentId` obtains the associated appointment fail-soft, reads `GlobalAppointmentID`, and releases the wrapper in `finally`. Only `object?` crosses internally; `string?` crosses the `IMessageSource` seam. No COM type leaks. |
| Sensitive-message path | `OutlookScanner.Redaction.cs` sets `LinkedGlobalAppointmentId: null` with an explicit comment tying the decision to the issue-#18 never-ingest ordering (protected-member access must not run for a Private/Confidential item). A redacted item therefore degrades to the fallback. This is a defensible, conservative default. |

## Error Handling and Logging

- Fail-fast where appropriate: malformed id at the RPC boundary returns an explicit `INVALID_REQUEST` failure rather than silently degrading.
- The two broad `catch` blocks (`ComMessageSource.ResolveLinkedGlobalAppointmentId`, and the defensive decode guard in the repository) are documented fail-soft boundaries at the COM/host edge, returning a clean `null` consistent with the graceful-degradation contract. They do not mask domain-logic errors.
- No new error telemetry is emitted on the unlinked path, matching the stated intent that a common unlinked case does not pollute error telemetry.

## File-Size Cap

All added/changed source and test files are under 500 lines. The three near-cap files received only minimal wiring (one switch arm in `PipeRpcWorker.cs`, one route-map call in `Program.cs`, INSERT/read wiring and a condensed interface doc in `CacheRepository.cs`), with the substantive new logic placed in new partials. Largest changed production file: `CacheRepository.cs` (495). Largest changed test file: `MailBridgeRuntimeTestDoubles.cs` (495), which extracted its linkage double into `MailBridgeRuntimeTestDoubles.Linkage.cs` to hold the cap.

## Test Quality and Determinism

- Framework: MSTest + Moq + FluentAssertions throughout; no xUnit/NSubstitute.
- Determinism: no `Thread.Sleep`/`Task.Delay`/wall-clock reads; `FakeTimeProvider` used in the Graph parity tests; `Guid.NewGuid()` used only to name isolated in-memory SQLite databases (test isolation), not in assertions.
- No temporary files: in-memory SQLite (`Mode=Memory;Cache=Shared`) and seam fakes only.
- Scenario completeness is strong: every seeded test condition in `spec.md` maps to at least one named test (repository resolution, handler dispatch, route envelope mapping, projector, source-seam fail-soft, migration idempotency, Core rewire, cloud parity).

## Coverage-Exclusion Policy

No production source file under `src/` is excluded from coverage. The only runsettings exclusion is `[*.Tests]*` (test assemblies). No Blocking coverage-exclusion finding.

## Observations (non-blocking, no action required)

1. `HostAdapterHttpClient.GetEventForMessageAsync` returns `SendAsync<EventDto>(...)` while the route projects `EventDto?`. On the wire the client deserializes the `ApiEnvelope` and the `Data` element is nullable, so a `data:null` response is handled; the Core guard checks `Data: not null` regardless. No defect. Recorded only to document that the generic argument difference between the route (`EventDto?`) and the client (`EventDto`) is intentional and consistent with the existing `GetEventAsync` shape.
2. The `GraphHostAdapterClient` implementation discards `bridgeId`/`cancellationToken` (`_ = bridgeId; _ = cancellationToken;`) by design, since the cloud path resolves to a null envelope without a round-trip. This is documented in the method's XML comment.

## Summary

- Blocking findings: 0.
- Overall code-review verdict: **PASS.**
