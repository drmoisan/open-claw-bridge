# Coverage Comparison — Baseline vs Post-Change

Timestamp: 2026-07-02T16-29

## Comparison Inputs

- Baseline Cobertura (Phase 0, OpenClaw.Core.Tests run): `artifacts/csharp/d7cde8c7-5b88-484b-8831-5ce1586dd13e/coverage.cobertura.xml` (plus HostAdapter `artifacts/csharp/c081d4a4-7a3c-49a7-845c-819da868146e/coverage.cobertura.xml` and MailBridge `artifacts/csharp/9a77f964-ff54-448b-bf6f-3698a5e72054/coverage.cobertura.xml`); summary in `evidence/baseline/baseline-test-coverage.2026-07-02T16-17.md`.
- Post-change Cobertura (Phase 3, OpenClaw.Core.Tests run): `artifacts/csharp/8c96ad60-49d1-47f1-9c9d-cbadd02f4d66/coverage.cobertura.xml` (plus HostAdapter `artifacts/csharp/4a67323f-a51e-4665-b22e-2b9334fc7ef9/coverage.cobertura.xml` and MailBridge `artifacts/csharp/343c8e25-c41b-419f-a581-06c80922ef6f/coverage.cobertura.xml`); summary in `evidence/qa-gates/final-qa-test-coverage.2026-07-02T16-28.md`.

## Numeric Comparison

| Scope | Baseline line | Post-change line | Baseline branch | Post-change branch |
|---|---|---|---|---|
| OpenClaw.Core (module touched) | 90.97% (1753/1927) | 91.00% (1761/1935) | 80.81% (417/516) | 80.96% (421/520) |
| OpenClaw.HostAdapter | 87.70% (1113/1269) | 87.70% (1113/1269) | 67.19% (170/253) | 67.19% (170/253) |
| OpenClaw.MailBridge | 93.58% (1533/1638) | 93.58% (1533/1638) | 88.16% (417/473) | 88.16% (417/473) |
| Aggregate | 91.00% (4399/4834) | 91.02% (4407/4842) | 80.84% (1004/1242) | 80.90% (1008/1246) |

## Changed-Line Coverage (per changed production file)

- `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs` (new): line-rate 1.0, branch-rate 1.0 — 8/8 instrumented lines covered, 0 missed, all 4 branch outcomes (2 per `&&` predicate) covered. Every added production line is covered.
- `src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs` (+2 auto-properties): the file contains only compiler-generated auto-property accessors and is excluded from instrumentation by `mailbridge.runsettings` (`ExcludeByAttribute` includes `CompilerGeneratedAttribute`); it is absent from both the baseline and post-change Cobertura denominators, so there is no changed-line coverage denominator for it and no regression is possible. Behavioral coverage of the two added properties is direct: the defaults test, three binding tests, the 8-row truth table, and all three CsCheck properties read/write both properties (`tests/OpenClaw.Core.Tests/Agent/CalendarWritePolicyTests.cs`, `CalendarWritePolicyPropertyTests.cs`).
- `src/OpenClaw.Core/appsettings.json`: configuration sample, not executable code; no coverage denominator.

## Threshold Verdicts

- Post-change line coverage >= 85%: PASS (Core 91.00%; aggregate 91.02%).
- Post-change branch coverage >= 75%: PASS for the touched module (Core 80.96%) and aggregate (80.90%). Note: OpenClaw.HostAdapter branch coverage is 67.19% both before and after — a pre-existing baseline condition in an untouched module, bit-identical to baseline (no regression introduced by this change).
- No reduction in coverage for changed lines: PASS (Core line and branch both increased: +0.03pp line, +0.15pp branch; the only instrumented changed lines are the 8 new CalendarWritePolicy lines, all covered).
- 100% covered changed production lines: PASS (8/8 instrumented; AgentPolicyOptions additions have no instrumentable lines and are behaviorally covered as documented above).

Output Summary: PASS. Post-change coverage meets thresholds on the touched module and aggregate, no regression on any changed line, and the new production file is 100% line- and branch-covered.
