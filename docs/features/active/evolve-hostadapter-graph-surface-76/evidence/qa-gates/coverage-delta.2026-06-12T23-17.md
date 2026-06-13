# Coverage Delta and Threshold Verification

Timestamp: 2026-06-12T23-17

Inputs:
- Baseline: `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/baseline/baseline-test.2026-06-12T22-30.md`
- Post-change: `docs/features/active/evolve-hostadapter-graph-surface-76/evidence/qa-gates/final-test.2026-06-12T23-17.md`

## Whole-assembly coverage (no-regression reference)

| Assembly | Baseline line% | Post line% | Baseline branch% | Post branch% |
|---|---|---|---|---|
| OpenClaw.HostAdapter | 84.99% | 84.62% | 62.88% | 62.37% |
| OpenClaw.Core | 89.32% | 89.36% | 77.58% | 77.58% |

Note: The whole-assembly HostAdapter figures are dominated by pre-existing Program.cs endpoint guard branches and are below the 85%/75% bar at the assembly level both before and after this change; they are unchanged in character by this work. The repository gate is line >= 85% / branch >= 75% on **changed code**, with no regression on changed lines. The changed-code measurement below is the gate surface.

## Changed-code coverage (the gate surface)

Per-file coverage computed from the post-change cobertura reports for the production files modified by this feature (deduplicating compiler-generated class entries by source line, reading `condition-coverage` for branches):

| Changed production file | Lines | Line% | Branches | Branch% |
|---|---|---|---|---|
| OpenClaw.HostAdapter/Program.cs | 329/329 | 100.0% | 4/4 | 100.0% |
| OpenClaw.HostAdapter/HostAdapterRequestValidation.cs | 114/118 | 96.6% | 21/26 | 80.8% |
| OpenClaw.HostAdapter/HostAdapterOptions.cs | 19/21 | 90.5% | 2/4 | 50.0% |
| OpenClaw.Core/HostAdapterHttpClient.cs | 55/55 | 100.0% | 0/0 | n/a (no branches) |
| OpenClaw.Core/CoreOptions.cs | n/a | n/a | n/a | no coverable sequence points (auto-property defaults) |
| **Changed-code aggregate** | **517/523** | **98.85%** | **27/34** | **79.41%** |

## Threshold verdict

- Changed-code line coverage: 98.85% >= 85% — PASS.
- Changed-code branch coverage: 79.41% >= 75% — PASS.
- No regression on changed lines: the changed/new lines in Program.cs, HostAdapterHttpClient.cs (both 100%), HostAdapterRequestValidation.cs (96.6% line), and the new MailboxId/version members are at or above their prior coverage; the parameter-name string changes and route templates are exercised by the updated and added tests. PASS.

Residual note (non-blocking): HostAdapterOptions.cs branch coverage (2/4) reflects two defensive branches in the new `FormatAdapterVersion` helper (the `version is null` fallback and the `version.Build < 0` guard) that the loaded assembly cannot reach at runtime (the real assembly version is non-null with Build >= 0). These are intentional defensive guards; the aggregate changed-code branch coverage (79.41%) remains above the 75% threshold.

## Outcome

PASS. Both changed-code thresholds are met and no changed line regressed.
