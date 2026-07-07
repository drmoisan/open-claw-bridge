# Final QA 05 — Coverage Delta and Threshold Verification (Issue #120)

Timestamp: 2026-07-06T23-32

Compares the Phase 0 baseline
(`evidence/baseline/phase0-baseline-04-dotnet-test-coverage.md`) against the Phase 6
post-change coverage (`evidence/qa-gates/final-qa-04-dotnet-test-coverage.md`), and reports
the per-new-file coverage from the post-change Cobertura report.

## OpenClaw.Core package coverage (baseline vs post-change)

| Metric | Baseline | Post-change | Delta | Threshold | Result |
|---|---|---|---|---|---|
| Line | 92.49% | 92.82% | +0.33 pp | >= 85% | PASS |
| Branch | 80.90% | 81.40% | +0.50 pp | >= 75% | PASS |

Both metrics increased; there is no coverage regression at the module level.

## Per-new-file / new-code coverage (post-change Cobertura, class line-rate/branch-rate)

Production files:
- `src/OpenClaw.Core/ScopeValidation/MailboxProbeOutcome.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidationResult.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryEvaluator.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeValidationOptions.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeValidationOptionsValidator.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryValidator.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeBoundaryStartupValidator.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/ScopeValidationServiceCollectionExtensions.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/CloudGraph/GraphMailboxScopeProbe.cs` — line 100%, branch 100%.
- `src/OpenClaw.Core/ScopeValidation/IMailboxScopeProbe.cs` — interface-only (no executable
  behavior); legitimately reports no executable coverage per the coverage policy and is not
  counted against the threshold.

Changed production file:
- `src/OpenClaw.Core/Program.cs` — the one added registration statement (the
  `AddScopeBoundaryValidation(...)` call at lines 69-72) records 12 hits from the
  WebApplicationFactory integration tests (which boot the composition root with
  ScopeValidation disabled, so the call executes and returns early). The
  `AddScopeBoundaryValidation` method itself is line 100%, branch 100%. No regression on the
  changed line.

## Verdict

Post-change `OpenClaw.Core` line coverage 92.82% >= 85% and branch coverage 81.40% >= 75%;
no regression on changed lines (module coverage increased and the changed Program.cs line is
covered); every new production file is at 100% line and branch coverage. Coverage
delta/threshold gate: PASS.
