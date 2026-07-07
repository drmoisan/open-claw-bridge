# Coverage Comparison — Baseline vs. Post-Change (issue #119, P5-T6)

Timestamp: 2026-07-06T23-21
Baseline source: `evidence/baseline/csharp-test-coverage.2026-07-06T22-45.md`
Post-change source: `evidence/qa-gates/csharp-test-coverage.2026-07-06T23-21.md`
Thresholds: line >= 85%, branch >= 75%, no regression on changed lines.

## OpenClaw.Core assembly (module under change)

| Metric | Baseline (P0-T5) | Post-change (P5-T5) | Delta |
|---|---|---|---|
| Line coverage | 93.73% (2588/2761) | 94.61% (3035/3208) | +0.88 pts |
| Branch coverage | 85.25% (682/800) | 86.08% (742/862) | +0.83 pts |

Both metrics improved; neither regressed. Both remain above the uniform thresholds.

## New / changed-code coverage

| File | Baseline | Post-change | Changed-line verdict |
|---|---|---|---|
| `SendOnBehalfAuthorizer.cs` (new) | n/a (new file) | line 100%, branch 87.5% | PASS — new code covered above thresholds |
| `GraphAdapterOptionsValidator.cs` | line 100%, branch 100% | line 100%, branch 100% | PASS — no regression |
| `GraphHostAdapterClient.SendMail.cs` | line 100%, branch 100% | line 100%, branch 100% | PASS — no regression |
| `GraphAdapterOptions.cs` | options bag (not separately reported) | options bag (not separately reported) | PASS — the added property initializer executes on every options construction (binding, validator, and authorizer paths) |

Notes:
- `SendOnBehalfAuthorizer.cs` reaches 100% line coverage; branch coverage is 87.5%. The one
  uncovered branch is the defensive `entry is not null` short-circuit inside the allowlist
  membership loop (a null-guard on a collection that the configuration binder never populates
  with null in practice). This is above the 75% branch threshold.
- The three changed logic files (`Validator`, `SendMail`, `Authorizer`) are all at 100% line
  coverage, so every changed executable line is covered.

## Verdict

PASS. Line >= 85% (94.61%), branch >= 75% (86.08%), no regression on changed lines, and
new-code coverage for `SendOnBehalfAuthorizer.cs` is line 100% / branch 87.5%.
