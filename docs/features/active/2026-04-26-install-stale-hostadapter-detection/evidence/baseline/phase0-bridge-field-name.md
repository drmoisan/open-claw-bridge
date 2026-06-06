# Phase 0 Bridge State On-Wire Field Name

Timestamp: 2026-04-26T22-55

Determination: The on-wire JSON property path that expresses the bridge state in a `/v1/status` response body is **`data.state`** (camelCase).

Evidence:

1. `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs:64-72` — `BridgeStatusDto` is declared as a `public sealed record` with positional parameters and no `[JsonPropertyName(...)]` attributes. The first parameter is `string State`. With ASP.NET minimal-API JSON defaults the property serializes using camelCase.

2. `src/OpenClaw.HostAdapter/Program.cs:396` — the `/v1/status` handler returns the response via `Results.Json(result.Envelope, statusCode: result.StatusCode)`. `Results.Json` uses ASP.NET Core's default `HttpJsonOptions`, which apply `JsonSerializerDefaults.Web` (camelCase property naming) when no override is registered.

3. `src/OpenClaw.HostAdapter/HostAdapterProcessRunner.cs:19` — the adapter's own deserialization options confirm the same default: `private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);`. No `Program.cs` call to `ConfigureHttpJsonOptions` overrides the response naming policy (grep confirms zero matches).

4. `src/OpenClaw.HostAdapter/HostAdapterResponses.cs:78-96` — `BridgeNotReady<T>` constructs an `ApiEnvelope<T>` whose `Data` is the `BridgeStatusDto` (or whatever `T` is). For `/v1/status` the `Data` payload is `BridgeStatusDto` itself, so the not-ready states (`bridgeStatus.State == BridgeState.starting` or `BridgeState.waiting_for_outlook`, per `Program.cs:444-455`) appear at JSON path `data.state` in the response body.

5. `src/OpenClaw.HostAdapter/Program.cs:444-455` — `IsBridgeNotReady` reads `bridgeStatus.State` against `BridgeState.starting` and `BridgeState.waiting_for_outlook`. The values returned to operators are therefore the lowercase enum names `starting` and `waiting_for_outlook` (matched case-insensitively in C#; PowerShell preflight will mirror the case-insensitive comparison).

P2-T4 helper rule (`Assert-HostAdapterBridgeReadyPreflight`): treat `data.state` (string) as the bridge-state field. Reject when missing/empty or when value (case-insensitive) is `starting` or `waiting_for_outlook`. Accept otherwise (HTTP 200 + non-empty `data.state`).

P6-T7/P6-T8 fixtures: use `data.state` as the JSON key in test envelopes.
