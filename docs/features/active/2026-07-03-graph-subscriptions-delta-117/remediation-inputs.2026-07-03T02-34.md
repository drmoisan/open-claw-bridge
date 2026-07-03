# Remediation Inputs: graph-subscriptions-delta (#117) — Cycle Entry 2026-07-03T02-34

- **Produced by:** feature-review agent (review at branch head `f6aea4d646dd79d3be3cc74f61f31ad5c56b52a3`)
- **Base:** `main` @ merge-base `402703c6c18d0131128696147443ef9f41110f3c` (origin/main)
- **Source audit artifacts:**
  - `docs/features/active/2026-07-03-graph-subscriptions-delta-117/policy-audit.2026-07-03T02-34.md` (finding B-117-01, Blocking; Sections 5 and 8)
  - `docs/features/active/2026-07-03-graph-subscriptions-delta-117/code-review.2026-07-03T02-34.md` (Findings Table: 2 Blocking rows, CR-117-02/03/04 Minor)
  - `docs/features/active/2026-07-03-graph-subscriptions-delta-117/feature-audit.2026-07-03T02-34.md` (AC-5 PARTIAL)
- **Reviewer coverage evidence:** fresh cobertura under `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/qa-gates/coverage-review/` (three reports; dedupe duplicate class entries per file+line before pooling)

## Trigger

Policy audit finding **B-117-01 (Blocking)**: two new production files measure below the uniform 75% per-file branch gate —

| File | Instrumented branch | Gate | Untaken arms |
|---|---|---|---|
| `src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs` | 2/4 = 50.00% | >= 75% | `ParseSubscription` line 315 (`?? throw new JsonException` for a body deserializing to JSON `null`) and line 320 (`IsNullOrWhiteSpace(wire.Id)` throw arm raising `GraphMappingException` for a response missing `id`) |
| `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` | 1/2 = 50.00% | >= 75% | `ReadSubscription` line 152 (`?? DateTimeOffset.MinValue` fallback for an unparseable stored `expiration_utc`) |

These are measured-and-uncovered arms with no test anywhere in the suite (not instrumentation exclusions). All other gates pass (pooled 92.83% line / 83.25% branch; every instrumented new file at 100.00% line; toolchain clean at branch head).

## Enumerated Fix List (remediation-required)

1. **Cover `ParseSubscription` missing-`id` fail-fast arm.**
   - File: new test in `tests/OpenClaw.Core.Tests/CloudSync/` (extend `GraphSubscriptionManagerTests.cs` or add a small edge-case class; keep files under 500 lines).
   - Behavior: drive `GraphSubscriptionManager.CreateAsync` (or `RenewAsync`) with a mocked `HttpMessageHandler` returning HTTP 200 with body `{}` (no `id`). Expected: failure envelope with code `INTERNAL_ERROR` (the executor's `GraphMappingException` mapping), and nothing persisted to `ISubscriptionStore`.
   - Verification: the new test passes; cobertura shows line 320's condition fully covered.

2. **Cover `ParseSubscription` null-deserialized-body arm.**
   - Same location. Drive `CreateAsync` with HTTP 200 body `null` (the four-character JSON literal). Expected: failure envelope with code `TRANSPORT_FAILURE` (the executor's `JsonException` mapping for unparseable success bodies), nothing persisted.
   - Verification: test passes; line 315's condition fully covered.

3. **Resolve the `ReadSubscription` MinValue-fallback arm (choose one; option (a) preferred per CR-117-04).**
   - (a) Fail-fast refactor: in `src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs` `ReadSubscription`, replace `?? DateTimeOffset.MinValue` with a throw of `InvalidOperationException` naming the subscription id and the `expiration_utc` column (the column is NOT NULL and always written via `RenderUtc`, so an unparseable value is data corruption). Add a directed test: open a second `SqliteConnection` on the same in-memory shared-cache connection string (pattern already used by `CoreCacheRepositorySubscriptionsTests.NewConnectionString`), INSERT a row with `expiration_utc = 'not-a-timestamp'`, call `GetSubscriptionAsync`, assert the specific exception.
   - (b) Minimal test-only option: same INSERT setup, assert the record round-trips with `ExpirationUtc == DateTimeOffset.MinValue`.
   - Verification: test passes; line 152's condition fully covered (or eliminated by the refactor).

4. **(Recommended, Minor CR-117-02 — same cycle) Pin the three exact-gate arms in `GraphDeltaReconciler.ParseDeltaPage`** so the file moves off the zero-margin 75.00%:
   - Page body without a `value` property (expect: zero upserts, walk proceeds to the deltaLink page and succeeds).
   - `@removed` entry without an `id` (expect: Debug skip using `"(unknown)"`, no upsert).
   - Entry literal `null` inside `value` (expect: failure envelope via the `JsonException` mapping and a failed `delta_reconcile` ingest run).

5. **(Recommended, Minor CR-117-03 — same cycle, one line per file) Broaden the worker loop catch filters** in `NotificationDispatchWorker.cs`, `SubscriptionRenewalWorker.cs`, and `DeltaReconciliationWorker.cs` from `catch (Exception ex) when (ex is not OperationCanceledException)` to `catch (Exception ex) when (!stoppingToken.IsCancellationRequested)` so a non-stop-token `OperationCanceledException` (e.g. `HttpClient.Timeout` `TaskCanceledException`, unmapped by `GraphRequestExecutor`) cannot terminate the hosted service. Add or extend one worker test where the inner call throws `TaskCanceledException` while the stop token is not cancelled; assert the loop logs Warning and continues. (The executor-side timeout mapping is a follow-up issue, out of scope here — `GraphRequestExecutor` is not in this branch's diff.)

### Exit verification commands

```bash
csharpier check .
dotnet build OpenClaw.MailBridge.sln
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
# Re-parse per-file branch coverage from fresh cobertura (dedupe duplicate class entries):
# GraphSubscriptionManager.cs and CoreCacheRepository.Subscriptions.cs must each report >= 75% instrumented branch;
# GraphDeltaReconciler.cs should report > 75% if item 4 is done. Pooled gates must remain >= 85% line / >= 75% branch.
```

## Do Not Do

- No scope creep: do not modify `GraphRequestExecutor`, the webhook processor, the queue, the schema, or any behavior other than the optional item-3(a)/item-5 changes named above.
- No policy weakening: do not add coverage exclusions, suppressions, or runsettings changes; do not relax or delete any existing test or assertion.
- No silent skips: every fix item above must land with its verification command output recorded as evidence under `docs/features/active/2026-07-03-graph-subscriptions-delta-117/evidence/<kind>/` (canonical path; never `artifacts/...`).
- No temp files, no wall-clock APIs, no live network in any new test; stay on MSTest + FluentAssertions + Moq (+ CsCheck where a genuine property applies); keep every file under 500 lines.
- Do not add new acceptance criteria or edit AC text; AC-5's existing check-off is re-confirmed at re-audit, not re-edited during execution.

## Handoff

Per `remediation-handoff-atomic-planner`, the orchestrator authors the cycle's `remediation-plan.<entry-ts>.md` via `atomic-planner` (plan shape per `atomic-plan-contract`), runs the executor preflight sub-loop, executes task-by-task, and re-delegates to feature-review for the exit re-audit (`policy-audit`/`code-review`/`feature-audit` at the exit timestamp). Exit condition: B-117-01 closed (both files >= 75% per-file branch), toolchain clean, `blocking_count == 0`.
