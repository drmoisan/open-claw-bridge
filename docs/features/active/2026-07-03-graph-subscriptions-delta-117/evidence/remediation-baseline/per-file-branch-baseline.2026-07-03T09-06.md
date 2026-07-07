# Remediation Baseline — Per-File Branch Coverage for Finding B-117-01 (Cycle 1, Issue #117)

Timestamp: 2026-07-03T09-06
Command: python parse_cobertura.py artifacts/csharp/remediation-baseline-117 (dedupe duplicate class entries per file+line within each report; max-pool per-file detail across reports; scratchpad parser, throwaway)
EXIT_CODE: 0
Output Summary:

| File | Line | Branch | Gate 75% |
|---|---|---|---|
| src/OpenClaw.Core/CloudSync/GraphSubscriptionManager.cs | 55/55 = 100.00% | 2/4 = 50.00% | FAIL (untaken arms at lines 315, 320) |
| src/OpenClaw.Core/CoreCacheRepository.Subscriptions.cs | 9/9 = 100.00% | 1/2 = 50.00% | FAIL (untaken arm at line 152) |
| src/OpenClaw.Core/CloudSync/GraphDeltaReconciler.cs | 88/88 = 100.00% | 12/16 = 75.00% | AT gate, zero margin (partial arms at lines 229, 235, 243) |

- All three per-file values match the reviewer's parsed figures exactly (expected 100.00%/50.00%, 100.00%/50.00%, and 75.00% respectively).
- Finding B-117-01 reproduces at cycle start: both named files are below the uniform 75% per-file instrumented-branch gate.
- Worker files (NotificationDispatchWorker.cs 17/17 line, SubscriptionRenewalWorker.cs 8/8 line, DeltaReconciliationWorker.cs 7/7 line) currently report no instrumented branches; their async loop bodies are uninstrumented under the pre-existing CompilerGenerated runsettings exclusion.
- Source reports: `artifacts/csharp/remediation-baseline-117/**/coverage.cobertura.xml` (3 reports from the P0-T4 run).
