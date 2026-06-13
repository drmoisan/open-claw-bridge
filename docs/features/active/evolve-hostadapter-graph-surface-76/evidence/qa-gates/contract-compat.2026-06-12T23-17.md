# Contract / Schema Compatibility Check (T2 IHostAdapterClient)

Timestamp: 2026-06-12T23-17

This is a breaking change to the HostAdapter HTTP surface and the `IHostAdapterClient` T2 contract (path-and-query reshaping to Microsoft Graph-shaped routes). Per `.claude/rules/quality-tiers.md`, a T1/T2 contract breaking change requires a major version bump and a compatibility check.

## IHostAdapterClient member parity (C# signatures unchanged)

`git diff HEAD src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` shows only XML-doc (`///`) lines changed; no signature line was added or removed. All six members are retained with identical signatures:

| Member | Signature retained | Notes |
|---|---|---|
| `GetStatusAsync(requestId, ct)` | yes | -> `ApiEnvelope<BridgeStatusDto>` |
| `ListMessagesAsync(sinceUtc, limit, requestId, ct)` | yes | -> `ApiEnvelope<ItemsResponse<MessageDto>>` |
| `GetMessageAsync(bridgeId, requestId, ct)` | yes | -> `ApiEnvelope<MessageDto>` |
| `ListMeetingRequestsAsync(sinceUtc, limit, requestId, ct)` | yes | RETAINED per D1; -> `ApiEnvelope<ItemsResponse<MessageDto>>` |
| `ListCalendarWindowAsync(startUtc, endUtc, limit, requestId, ct)` | yes | -> `ApiEnvelope<ItemsResponse<EventDto>>` |
| `GetEventAsync(bridgeId, requestId, ct)` | yes | -> `ApiEnvelope<EventDto>` |

## HostAdapterHttpClient implementation parity

`src/OpenClaw.Core/HostAdapterHttpClient.cs` implements every interface member and emits the Graph-shaped wire routes:

- `GetStatusAsync` -> `status`
- `ListMessagesAsync` -> `users/{id}/messages?$filter=receivedDateTime ge {iso}&$top={limit}`
- `GetMessageAsync` -> `users/{id}/messages/{escaped messageId}`
- `ListMeetingRequestsAsync` -> `users/{id}/messages?$filter=meetingMessageType ne null and receivedDateTime ge {iso}&$top={limit}`
- `ListCalendarWindowAsync` -> `users/{id}/calendarView?startDateTime={iso}&endDateTime={iso}&$top={limit}`
- `GetEventAsync` -> `users/{id}/events/{escaped eventId}`

The `{id}` segment is sourced from `options.HostAdapter.MailboxId` (default `"me"`). Verified by the updated `HostAdapterHttpClientTests.cs` (Core.Tests, all passing).

## DTO / envelope schema unchanged

`git diff --name-only HEAD` contains no `ApiEnvelope`, `ItemsResponse`, `MessageDto`, `EventDto`, `ApiMeta`, `ApiError`, or `BridgeStatusDto` file. These contract types are byte-for-byte unchanged. This is a path-and-query change only.

## Adapter version (major bump)

`<Version>1.0.0</Version>` is declared in `src/OpenClaw.HostAdapter/OpenClaw.HostAdapter.csproj`. `HostAdapterOptions.DefaultAdapterVersion` resolves to `1.0.0` (the getter formats the assembly version to the three-component major.minor.patch form). Verified by `HostAdapterVersionTests.DefaultAdapterVersion_should_report_1_0_0_from_the_assembly_version` (passing). This is the first major bump, signalling the breaking HTTP surface change reported through `meta.adapterVersion`.

## In-repo caller compilation and tests

All in-repo callers compile and pass:
- `HostAdapterHttpClient` (Core) — implements the interface; Core.Tests pass.
- `HostAdapterSchedulingService` (Core) — calls only `GetMessageAsync`, `GetEventAsync`, `ListCalendarWindowAsync` with unchanged signatures; no functional edit required (design note N3); `HostAdapterSchedulingServiceTests` pass.
- `MessagePollingWorker` and its tests — `ListMeetingRequestsAsync`/`ListMessagesAsync` mock setups remain valid (signatures unchanged); tests pass.

Result: PASS. Member parity confirmed, implementation confirmed, DTO/envelope unchanged, version 1.0.0 confirmed, all callers compile and pass.
