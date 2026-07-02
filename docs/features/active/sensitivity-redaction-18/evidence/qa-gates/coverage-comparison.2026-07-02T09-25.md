# Final QA Gate — Coverage Comparison (P5-T5)

Timestamp: 2026-07-02T09-25
Command: comparison of `evidence/baseline/dotnet-test-coverage.2026-07-02T08-58.md` (P0-T5) against `evidence/qa-gates/final-test-coverage.2026-07-02T09-25.md` (P5-T3); per-file/changed-line analysis of `artifacts/csharp/post-change-final/coverage.mailbridge.cobertura.xml` cross-referenced with `git diff -U0`
EXIT_CODE: 0
Output Summary: All four thresholds PASS. Details below.

## 1. Baseline coverage (P0-T5, 2026-07-02T08-58)

| Scope | Line | Branch |
|---|---|---|
| OpenClaw.MailBridge | 93.08% (1413/1518) | 86.92% (399/459) |
| OpenClaw.Core | 89.62% (1503/1677) | 78.44% (342/436) |
| OpenClaw.HostAdapter | 87.70% (1113/1269) | 67.19% (170/253) |
| Pooled | 90.26% (4029/4464) | 79.36% (911/1148) |

## 2. Post-change coverage (P5-T3, 2026-07-02T09-25)

| Scope | Line | Branch |
|---|---|---|
| OpenClaw.MailBridge (changed package) | 93.58% (1533/1638) | 87.31% (413/473) |
| OpenClaw.Core | 89.62% (1503/1677) | 78.44% (342/436) |
| OpenClaw.HostAdapter | 87.70% (1113/1269) | 67.19% (170/253) |
| Pooled | 90.51% (4149/4584) | 79.60% (925/1162) |

## 3. Changed-file / new-code coverage (MailBridge Cobertura, per-file)

| Changed production file | Line coverage | Changed-line status |
|---|---|---|
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | 137/151 (90.73%) | Changed lines 361-368 and 396 (per `git diff -U0`) are all covered; the 14 uncovered lines (35-43 public-constructor COM delegation, 91-93, 448-449 fail-soft branches) are pre-existing and untouched by this change |
| `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` (new) | 109/109 (100.00%) | All new lines covered |
| `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` | 65/65 (100.00%) | All changed lines covered |
| `src/OpenClaw.MailBridge/ResponseShaper.cs` | 54/54 (100.00%) | All changed lines covered |

## Verdicts

| Threshold | Requirement | Result | Verdict |
|---|---|---|---|
| (a) Line coverage >= 85% | pooled and changed package | Pooled 90.51%; MailBridge 93.58% | PASS |
| (b) Branch coverage >= 75% | pooled and changed package | Pooled 79.60%; MailBridge 87.31% | PASS |
| (c) No regression vs baseline | every scope | MailBridge +0.50 line / +0.39 branch; Core and HostAdapter byte-identical to baseline (untouched); pooled +0.25 line / +0.24 branch | PASS |
| (d) Changed lines covered | all 4 changed production files | 3 files at 100%; `OutlookScanner.cs` changed lines fully covered, uncovered lines pre-existing | PASS |

Note: OpenClaw.HostAdapter's package branch value (67.19%) is below 75% but is byte-identical to the pre-change baseline, contains no changed lines in this feature, and does not regress; the pooled branch value and the changed-package value both satisfy the threshold. Overall verdict: PASS.
