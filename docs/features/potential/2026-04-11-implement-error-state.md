---
title: "implement-error-state - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-45"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# implement-error-state (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The `BridgeState` enum in `BridgeContracts.cs` defines five lifecycle states: `starting`, `waiting_for_outlook`, `ready`, `degraded`, and `error`. The first four are set by code at [OutlookScanner.cs:91](src/OpenClaw.MailBridge/OutlookScanner.cs#L91), [OutlookScanner.cs:177](src/OpenClaw.MailBridge/OutlookScanner.cs#L177), and [BridgeStateStore.cs:84](src/OpenClaw.MailBridge/BridgeStateStore.cs#L84). The `error` state is never set by any code path — it is effectively dead code (deviation #15, Low).

The design intent is that `error` represents an unrecoverable fault — a condition from which the bridge cannot recover on its own and where continued operation would be unsafe or meaningless. The canonical example from the audit is a pipe ACL construction failure. `PipeRpcWorker.BuildPipeSecurity` can throw `InvalidOperationException` (if the primary user SID cannot be resolved) or `IdentityNotMappedException` (if the `openclaw-svc` account does not exist). These exceptions propagate through `CreateServer()` into `ExecuteAsync`, and from there through the `BackgroundService` host infrastructure. There is no code path that catches them and transitions the bridge to `error` state before the process terminates.

Without an `error` state transition, a client polling `status` during or just before a catastrophic failure receives either a stale state from the last successful poll or a pipe-connect failure. It cannot distinguish between `degraded` (operational but impaired) and a terminal fault. Operators and monitoring scripts cannot take differentiated action.

## Proposed Behavior

**Define the semantic boundary between `degraded` and `error`**

`degraded` means: the bridge is running, serving RPC requests, but the most recent scan cycle failed. The cache may be stale. Outlook may be temporarily unavailable. Recovery is expected on the next scan cycle.

`error` means: the bridge has encountered an unrecoverable condition. It cannot serve requests or self-recover. Operator intervention is required. The bridge process may still be alive for the duration of a graceful shutdown, but no further scan cycles will be attempted.

**Add a `MarkError` method to `BridgeStateStore`**

```csharp
public void MarkError(string reason)
{
    CacheStale = true;
    StaleReason = reason;
    State = BridgeState.error;
}
```

This follows the same pattern as `MarkCacheStale` and `MarkOutlookUnavailable`.

**Catch unrecoverable exceptions in `PipeRpcWorker.ExecuteAsync` and call `MarkError`**

`PipeRpcWorker` already holds a `BridgeStateStore state` reference. Wrap the `CreateServer()` call in a `try/catch` block. On `InvalidOperationException` or `IdentityNotMappedException` (both thrown by `BuildPipeSecurity`), call `state.MarkError(...)` with a redacted reason string, log the event at `Error` level, then re-throw to allow the host to terminate:

```csharp
NamedPipeServerStream server;
try
{
    server = CreateServer();
}
catch (Exception ex) when (ex is InvalidOperationException or IdentityNotMappedException)
{
    state.MarkError("pipe_acl_failure");
    logger.LogError(ex, "Unrecoverable pipe ACL failure. Bridge entering error state.");
    throw;
}
```

Re-throwing ensures the `BackgroundService` host continues to shut down the process cleanly, rather than swallowing the fault and spinning in a loop.

**Note on timing** — because the bridge process is likely to terminate shortly after the `error` transition (the host tears down on an unhandled `BackgroundService` exception), the `error` state will only be visible to clients that poll `status` during the narrow window between the state write and process exit. This is acceptable; the state is primarily useful for log correlation and for monitoring scripts that inspect the bridge's stored state rather than polling it live.

No changes are required to the RPC dispatch layer or to `BridgeStatusDto` — `State` is already serialized as its string name, so `error` will appear correctly in status responses.

## Acceptance Criteria (early draft)

- [ ] `BridgeStateStore` has a `MarkError(string reason)` method that sets `State = BridgeState.error`, `CacheStale = true`, and `StaleReason = reason`.
- [ ] `PipeRpcWorker.ExecuteAsync` catches `InvalidOperationException` and `IdentityNotMappedException` from `CreateServer()`, calls `state.MarkError("pipe_acl_failure")`, logs at `Error` level, and re-throws.
- [ ] The `error` string value appears correctly in the `state` field of a `status` RPC response after `MarkError` is called (no serialization changes needed; verified by existing response shaping path).
- [ ] A unit test covers `MarkError`: after calling it, `store.State == BridgeState.error`, `store.CacheStale == true`, and `store.StaleReason == reason`.
- [ ] A unit test covers `PipeRpcWorker` behavior when `CreateServer()` throws: the exception is re-thrown after `MarkError` is called on the state store (verifiable using the existing `ExcludeFromCodeCoverage` workaround or a testable overload of `CreateServer`).
- [ ] No existing unit tests are broken by the addition of `MarkError`.

## Constraints & Risks

- `PipeRpcWorker.CreateServer()` is marked `[ExcludeFromCodeCoverage]` (as is `BuildPipeSecurity`). The existing test suite cannot exercise the pipe ACL path directly. The unit test for the `PipeRpcWorker` error-state transition will need to use the `AccountSidResolver` injection point (already present as `internal static Func<string, SecurityIdentifier> AccountSidResolver`) to simulate a failure, or test the behavior by calling the methods through a test-accessible path. Alternatively, the `state.MarkError` call can be extracted into a helper that is testable in isolation from the pipe creation.
- Re-throwing after `MarkError` is the correct behavior because swallowing the exception would leave `PipeRpcWorker.ExecuteAsync` in a loop that calls `CreateServer()` again immediately, producing an infinite exception loop. The re-throw exits the `ExecuteAsync` loop, which causes the `BackgroundService` host to call `StopAsync` on all services.
- The `error` state transition window (between state write and process exit) may be too short for a polling client to observe it. This is acceptable for the current architecture. If observable-before-exit `error` state is required in the future, the bridge would need a graceful shutdown hook that waits for at least one poll before exiting — that is out of scope for this item.
- `MarkError` should not clear `OutlookConnected` or the last-scan timestamps. Preserving them allows post-mortem inspection of when the bridge was last healthy before the fatal fault.

## Test Conditions to Consider

- [ ] Unit test: `BridgeStateStore.MarkError("pipe_acl_failure")` sets `State = BridgeState.error`, `CacheStale = true`, `StaleReason = "pipe_acl_failure"`.
- [ ] Unit test: `BridgeStateStore.MarkError` does not modify `LastInboxScanUtc`, `LastCalendarScanUtc`, or `OutlookConnected`.
- [ ] Unit test: `BridgeStateStore.SetState(BridgeState.error)` already works (the generic setter already accepts any enum value) — this confirms no enum-guard was accidentally introduced.
- [ ] Unit test (if testable path exists): when `PipeRpcWorker` construction of the pipe server throws `InvalidOperationException`, `state.State` transitions to `BridgeState.error` and the exception propagates outward.
- [ ] Confirm `BridgeStatusDto` serializes `State` as `"error"` string — verifiable via the existing RPC dispatch unit tests by seeding state store with `BridgeState.error` and asserting the response payload.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/implement-error-state/` folder from the template

