---
name: faketimeprovider-backgroundservice-advance
description: Single exact FakeTimeProvider.Advance after BackgroundService.StartAsync does not reliably fire Task.Delay(TimeProvider) timers; use advance-per-yield-iteration (repo AwaitWithTimeAdvance pattern)
metadata:
  type: feedback
---

When testing a `BackgroundService` whose loop awaits `Task.Delay(interval, timeProvider, ct)` with `FakeTimeProvider` (net10, Microsoft.Extensions.TimeProvider.Testing 10.6.0), a single exact `timeProvider.Advance(interval)` issued right after `await worker.StartAsync(...)` reliably FAILS to fire the timer — even after pre-yield loops — while a bare `Task.Delay(interval, fakeTp, ct)` created directly in the test fires fine on exact advance.

**Why:** Verified empirically in issue #117 (`DeltaReconciliationWorkerTests`): worker ExecuteTask stayed `WaitingForActivation` with zero requests after exact single Advance; the identical setup passed once advancement was performed in small steps per yield iteration. Root cause not fully isolated (likely timer registration ordering relative to the test-thread Advance), but the failure is consistent, not flaky, for delay-first worker loops.

**How to apply:** Use the repository's `AwaitWithTimeAdvance` pattern (see `GraphRequestExecutorRetryTests`): a bounded loop that does `timeProvider.Advance(smallStep); await Task.Yield();` until the observable condition holds. For "exactly one tick" assertions, keep the step small (e.g., 1 minute against a 60-minute interval) so the loop stops long before a second interval elapses. Never assert on a single exact-interval Advance against a hosted worker.
