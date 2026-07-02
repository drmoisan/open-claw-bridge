# Reviewer Coverage Re-measurement (remediation cycle 1 re-audit R4, issue #18)

Timestamp: 2026-07-02T10-23
Command: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review-r4"` at branch head `82504ff12a8ccda9ac64d0535356769c8f1b01fa`, preceded by removal of any stale results in the target directory; three Cobertura reports parsed with per-line max pooling across duplicate partial-class entries (line hits and condition-coverage attributes).
EXIT_CODE: 0

## Test results

- 660 passed / 0 failed / 5 environment-gated skips (665 total): OpenClaw.MailBridge.Tests 347 passed (5 skips), OpenClaw.Core.Tests 213, OpenClaw.HostAdapter.Tests 100.
- Net +13 tests vs the pre-remediation head `d267c66` (647 passed): 6 edge-path scanner tests (`OutlookScannerSensitivityNormalizationEdgeTests`, 5 methods with one 2-row DataRow) and 7 invariant tests (`OutlookScannerRedactionInvariantTests`).

## Pooled coverage (three-report pool, per-line max on duplicates)

| Scope | Line | Branch |
|---|---|---|
| Feature baseline (executor, 2026-07-02T08-58) | 90.26% | 79.36% |
| Pre-remediation reference (reviewer, 2026-07-02T09-45) | 90.51% (4149/4584) | 79.60% (925/1162) |
| This re-audit (reviewer, 2026-07-02T10-23) | 90.51% (4149/4584) | 79.95% (929/1162) |

Identical to the executor's P3-T4 remediation verification (`coverage-remediation-verification.2026-07-02T10-11.md`): line 4149/4584, branch 929/1162. No regression; branch improved by 4 covered conditions.

## Per-changed-file line AND branch (reviewer-parsed, MailBridge Cobertura)

| File | Line | Branch | Verdict |
|---|---|---|---|
| `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` (NEW) | 100.00% (109/109) | 100.00% (14/14) | PASS (was 71.43% branch pre-remediation; all four previously uncovered conditions now covered) |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | 90.73% (137/151) | 90.00% (36/40) | PASS (uncovered lines 35-43, 91-93, 448-449 and partial conditions at 90/389/447/453 are pre-existing and untouched by this branch; changed lines covered) |
| `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` | 100.00% (65/65) | 100.00% (8/8) | PASS |
| `src/OpenClaw.MailBridge/ResponseShaper.cs` | 100.00% (54/54) | 100.00% (6/6) | PASS |

## Per-threshold verdicts

| Threshold | Value | Verdict |
|---|---|---|
| Pooled line >= 85% | 90.51% | PASS |
| Pooled branch >= 75% | 79.95% | PASS |
| New-file gate (`OutlookScanner.Redaction.cs`): line >= 85%, branch >= 75% | 100.00% / 100.00% | PASS |
| Modified files: line >= 85%, branch >= 75%, changed lines covered, no regression | see table above | PASS |
| No pooled regression vs pre-remediation (90.51% / 79.60%) | 90.51% / 79.95% | PASS |

**Overall verdict: PASS on every threshold.** The prior Blocking finding (new-file branch coverage 71.43% < 75%) is closed and independently re-verified.
