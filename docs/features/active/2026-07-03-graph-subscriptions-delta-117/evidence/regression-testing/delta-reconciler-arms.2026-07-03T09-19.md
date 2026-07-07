# Regression Evidence — GraphDeltaReconciler.ParseDeltaPage Exact-Gate Arms (Remediation Cycle 1, Issue #117, fix item 4 / CR-117-02)

Timestamp: 2026-07-03T09-19
Command: dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~GraphDeltaReconciler"
EXIT_CODE: 0
Output Summary:
- Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11 (OpenClaw.Core.Tests.dll)
- Three new tests pass alongside all 8 existing reconciler tests (walk/resume in `GraphDeltaReconcilerTests.cs`; recovery matrix in `GraphDeltaReconcilerRecoveryTests.cs`):
  - `Reconcile_page_without_a_value_property_upserts_nothing_and_completes_the_walk` (in `GraphDeltaReconcilerTests.cs`, now 219 lines): covers the false arm of the line-229 value-present-and-array gate; asserts zero upserts and a successful walk persisting the deltaLink.
  - `Reconcile_removed_entry_without_an_id_is_skipped_with_debug_log_and_no_upsert` (in `GraphDeltaReconcilerRecoveryTests.cs`, now 407 lines): covers both id-fallback arms at lines 236-238 (id property absent AND id JSON null); asserts two Debug skips using `"(unknown)"` and no upsert.
  - `Reconcile_unparseable_entry_inside_value_fails_with_transport_failure_and_a_failed_ingest_run` (in `GraphDeltaReconcilerRecoveryTests.cs`): asserts the `JsonException` -> `TRANSPORT_FAILURE` envelope mapping (Warning log carries the code) and a failed `delta_reconcile` ingest run.

## Deviation record (P3-T2 target file and P3-T3 payload)

1. P3-T2/P3-T3 were placed in `GraphDeltaReconcilerRecoveryTests.cs` (plan-permitted alternative target) to reuse that file's private `ReadIngestRunsAsync` helper and the existing `CapturingLogger`/removed-entry patterns instead of duplicating them (`general-code-change.md` reusability). Both files remain under 500 lines (219 and 407).
2. The plan's P3-T3 scenario (literal `null` entry inside `value` reaching the `?? throw new JsonException` arm at `GraphDeltaReconciler.cs` line 243-245) is structurally unreachable: `element.TryGetProperty("@removed", ...)` at line 233 requires an Object-kind element and throws `System.InvalidOperationException` on a Null-kind element BEFORE the deserialize arm is reached. Verified by a failing first run of the literal-null variant (InvalidOperationException escaped `ParseSuccess`, which catches only `JsonException`/`GraphMappingException`). The test was adjusted to the nearest reachable payload producing the same mapped outcome: an entry `{ "id": 123 }` whose deserialization throws `JsonException`, exercising the TRANSPORT_FAILURE mapping and the failed ingest run exactly as the task's assertions require. The line-245 `??` null-coalescing arm is dead code behind the object-kind guard and remains 1/2 instrumented; the file's branch coverage still rises well above the 75.00% zero-margin gate (verified at P5-T4).
3. Follow-up flagged for the exit re-audit: a Graph delta page whose `value` contains a literal `null` currently escapes as an unhandled `InvalidOperationException` from the walk (no failure envelope, no failed ingest run). Fixing this requires a `GraphDeltaReconciler` production change, which is outside this cycle's permitted diff scope.
