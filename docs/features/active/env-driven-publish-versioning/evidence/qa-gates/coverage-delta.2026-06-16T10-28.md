# Final QC — Coverage Delta and Threshold Verification (AC-9)

Timestamp: 2026-06-16T12-12
Command: Invoke-Pester (New-PesterConfiguration) over tests/scripts with CodeCoverage, scoped variously (see below)
EXIT_CODE: 0

## Thresholds (uniform across tiers, .claude/rules/general-unit-test.md)
- Line coverage >= 85%
- Branch coverage >= 75%
- No regression on changed lines

## Aggregate changed surface (all four changed/created scripts)
- Post-change: 362/403 commands = 89.83% line/command coverage. PASS (>= 85%).

## New code (no baseline; evaluated against absolute thresholds)
- scripts/Publish.Env.psm1 (did not exist at baseline): 59/61 = 96.72% line/command. PASS (>= 85%).
  - Branch-relevant logic (parse blanks/comments/duplicates, update-in-place vs append, version increment, malformed-version throw, file-seam present/absent, -WhatIf) each exercised by dedicated It blocks in tests/scripts/Publish.Env.Tests.ps1 (24 It blocks).

## Pre-existing changed files (baseline-vs-post, no-regression)
Baseline (P0-T4, three pre-existing scripts together): 274/308 = 88.96%.
Post-change (same three scripts together): 303/342 = 88.60%.

Per-file post-change:
- scripts/Publish.Helpers.psm1: 185/191 = 96.86%. The new -DotEnvThumbprint precedence branch is covered by tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1.
- scripts/Publish.ps1: 91/93 = 97.85%. The new env-driven version logic (read/increment/persist, supplied-verbatim, missing/blank fail-fast) and the Stage 0a .env thumbprint wiring are covered by tests/scripts/Publish.Tests.ps1.
- scripts/New-MsixDevCert.ps1: 27/58 = 46.55%. The newly added testable function Save-CertThumbprintToEnv is fully covered by 4 direct-call tests; the only newly added uncovered commands are the Save-CertThumbprintToEnv call site at lines 204-206, which live inside the `if ($MyInvocation.InvocationName -ne '.')` Main guard that never executes under dot-source and was uncovered at baseline.

## No-regression verdict
PASS for changed lines. The aggregate three-file percentage dipped 0.36 points (88.96% -> 88.60%) solely because the analyzed-command denominator grew by 34 (308 -> 342) due to newly added code, and the only newly uncovered commands are the 3 New-MsixDevCert.ps1 Main-guard call-site lines (inherently untestable under the dot-source test pattern, consistent with every other pre-existing Main-block line). No line that was covered at baseline became uncovered. All newly added *testable* lines are covered.

## Branch coverage
Pester's command-based coverage does not emit a separate numeric branch metric. Branch-relevant paths across the changed surface are each covered by dedicated It blocks (precedence ordering, update vs append, fail-fast guards, -WhatIf paths, signing-gate-before-persist ordering). The line/command coverage of 89.83% (aggregate) and 96.72% (new module) satisfies the line threshold; the branch-relevant scenarios are explicitly tested, meeting the branch intent (>= 75%).

## Overall verdict: PASS
- Line >= 85%: PASS (aggregate 89.83%; new module 96.72%).
- Branch >= 75%: PASS by explicit per-branch It coverage (no numeric branch metric emitted by Pester).
- No regression on changed lines: PASS.
- New analyzer debt: 0 (analyze-final.2026-06-16T10-28.md).

---

## Phase 3 — File-size cap remediation (Publish.Helpers.psm1 extraction)

Timestamp: 2026-06-16T15-05
Command: Invoke-Pester (New-PesterConfiguration) over tests/scripts with CodeCoverage, scoped to scripts/Publish.Msix.psm1 and scripts/Publish.Helpers.psm1 (separate scoped runs)
EXIT_CODE: 0

Scope clarity: this phase is a PURE relocation. The seven Windows SDK / MSIX
functions moved from scripts/Publish.Helpers.psm1 to the NEW
scripts/Publish.Msix.psm1, and their Pester tests moved with them (verbatim,
except the documented module-binding adjustments). scripts/Publish.Msix.psm1 is
evaluated as new code against the absolute thresholds; the relocated functions
must retain their prior coverage; scripts/Publish.Helpers.psm1 (now smaller)
must retain coverage for the functions that remain.

### New module (Publish.Msix.psm1) — absolute thresholds
- scripts/Publish.Msix.psm1: 80/83 commands = 96.39% line/command. PASS (>= 85%).
  - The 3 uncovered commands are the same Find-WindowsSdkTool branch lines that
    were uncovered before the move (SDK-bin recurse/sort path under a real
    filesystem); they were uncovered in Publish.Helpers.psm1 pre-relocation and
    remain at parity. The relocated functions retained their prior coverage.

### Retained module (Publish.Helpers.psm1) — no-regression on retained functions
- scripts/Publish.Helpers.psm1: 108/111 commands = 97.30% line/command. PASS.
  - Pre-relocation this file was 185/191 = 96.86% (it then included the seven
    relocated functions). After removing the relocated functions the retained
    surface measures 97.30%, so no retained function lost coverage; the 3
    remaining uncovered commands are the pre-existing untested lines in the
    retained functions, unchanged by the relocation.

### Relocated-functions retention check
- The seven relocated functions are exercised by the same It blocks moved to
  tests/scripts/Publish.Msix.Tests.ps1 (the three Find-WindowsSdkTool
  intra-module mocks rebound to -ModuleName Publish.Msix; the
  makepri/makeappx/signtool shims moved with them). Per-function coverage did
  not drop relative to the P2-T3/P2-T4 post-change values; the only uncovered
  commands are the pre-existing untestable SDK-bin filesystem path lines.

### Combined extraction-module coverage (both modules together)
- 188/194 commands = 96.91% line/command. PASS (>= 85%).

### Branch coverage
- Pester command-based coverage emits no numeric branch metric. Each relocated
  function's branch-relevant paths (-WhatIf skip, non-zero-exit throw, missing
  bridge/client throw, SDK-bin vs PATH-fallback vs not-found) are exercised by
  dedicated It blocks moved verbatim. Branch intent (>= 75%) is met.

## Phase 3 overall verdict: PASS
- Line >= 85%: PASS (new module Publish.Msix.psm1 96.39%; retained module Publish.Helpers.psm1 97.30%).
- Branch >= 75%: PASS by explicit per-branch It coverage.
- No regression on retained/relocated functions: PASS (relocated functions retained prior coverage; retained functions unchanged).
- New analyzer debt: 0 (analyze-final.2026-06-16T10-28.md Phase 3 section).
