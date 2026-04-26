# Remediation Plan — install-hostadapter-not-started-59

- Feature folder: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/`
- Plan path: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/remediation-plan.2026-04-26T15-51.md`
- Work mode: `minor-audit`
- Remediation source: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/remediation-inputs.2026-04-26T02-16.md`
- Acceptance criteria source: `docs/features/active/2026-04-25-install-hostadapter-not-started-59/issue.md` under `## Acceptance Criteria` only
- Context-only inputs: `plan.2026-04-25T00-00.md`, `policy-audit.2026-04-26T02-16.md`, `code-review.2026-04-26T02-16.md`, `feature-audit.2026-04-26T02-16.md`
- Target remediation commit to validate: `73d8fc5f038632b25b7c78d33345ecfafa90afc0`
- Re-audit base branch: `development`
- Scope guard: close the open remediation loop for `REM-01` only, plus the required validation, artifact refresh, and documentation updates needed to prove closure. Do not widen scope to remediate `REM-02` or `REM-03` unless the verification or QA evidence shows they now block remediation closure.

DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED
PREFLIGHT: ALL CLEAR

## Overview

This plan assumes the code remediation for `REM-01` is already present at `73d8fc5f038632b25b7c78d33345ecfafa90afc0` and limits execution to proving that state, closing the missing remediation artifacts, and refreshing the reduced review outputs. The plan fails closed if the repository state no longer matches the expected remediation commit or the expected split test-file line counts.

### Phase 0 — Context & Baseline

- [x] [P0-T1] Read the applicable repository policy files and authoritative remediation inputs in required order, then write the receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/phase0-instructions-read.md`.
  - Preconditions: the feature folder already exists.
  - Acceptance: `phase0-instructions-read.md` exists and contains `Timestamp:`, `Policy Order:`, `Files Read:`, and `Missing Files:` entries.
  - Acceptance: `Policy Order:` records this exact order: `.github/copilot-instructions.md`, `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, `.github/instructions/powershell-unit-test.instructions.md`, `issue.md`, and `remediation-inputs.2026-04-26T02-16.md`.
  - Acceptance: `Files Read:` records every file from that ordered list that exists at execution time.
  - Acceptance: if `.github/copilot-instructions.md` is absent, `Missing Files:` records that exact path and the receipt explicitly states that execution proceeded with the remaining applicable policy files.
  - Acceptance: the receipt explicitly states that `issue.md` under `## Acceptance Criteria` is the sole acceptance-criteria source for this `minor-audit` remediation plan.

- [x] [P0-T2] Verify the feature folder still satisfies the `minor-audit` scope contract and write the result to one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/minor-audit-scope.*.md`.
  - Preconditions: [P0-T1] is complete.
  - Acceptance: exactly one new `minor-audit-scope.*.md` artifact exists and contains `Timestamp:`, `SearchScope:`, `SearchPatterns:`, and `SearchResult:` fields.
  - Acceptance: the artifact records all of the following exact findings: `WorkMode=minor-audit`, `AcceptanceCriteriaSection=present`, `spec.md=absent`, `user-story.md=absent`, and `research.md=absent` for `docs/features/active/2026-04-25-install-hostadapter-not-started-59/`.

- [x] [P0-T3] Capture baseline remediation-state evidence for the current repository HEAD and the expected split test-file sizes in one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/remediation-state.*.md`.
  - Preconditions: [P0-T1] is complete.
  - Acceptance: exactly one new `remediation-state.*.md` artifact exists and contains `Timestamp:`, `Command: git rev-parse HEAD`, and `HEAD_SHA: 73d8fc5f038632b25b7c78d33345ecfafa90afc0`.
  - Acceptance: the same artifact records these exact command/result pairs:
    - `Command: (Get-Content 'tests/scripts/Install.Tests.ps1').Count` with `Result: 456`
    - `Command: (Get-Content 'tests/scripts/Install.Force.Tests.ps1').Count` with `Result: 163`
    - `Command: (Get-Content 'tests/scripts/Install.HostAdapterStart.Tests.ps1').Count` with `Result: 64`
  - Acceptance: if any recorded value differs, the artifact includes `REM-01 State: REOPENED` and the plan stops before reduced re-audit handoff.

- [x] [P0-T4] Run the approved PowerShell formatter contract and persist the baseline receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-format.2026-04-26T15-51.md`.
  - Preconditions: [P0-T1] is complete.
  - Acceptance: `baseline-format.2026-04-26T15-51.md` exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_format`, `EXIT_CODE:`, and `Output Summary:`.

- [x] [P0-T5] Run the approved PowerShell analyzer contract and persist the baseline receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-analyze.2026-04-26T15-51.md`.
  - Preconditions: [P0-T4] is complete.
  - Acceptance: `baseline-analyze.2026-04-26T15-51.md` exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_analyze`, `EXIT_CODE:`, and `Output Summary:`.

- [x] [P0-T6] Run the approved PowerShell test contract and persist the baseline receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-test.2026-04-26T15-51.md`.
  - Preconditions: [P0-T5] is complete.
  - Acceptance: `baseline-test.2026-04-26T15-51.md` exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: `Output Summary:` records the baseline test totals and explicitly names the coverage artifact path or paths that will be used for numeric coverage verification.
  - Acceptance: numeric coverage values are not considered complete until [P0-T7] records them in a dedicated baseline coverage artifact.

- [x] [P0-T7] Capture baseline numeric PowerShell coverage evidence in one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/baseline/baseline-coverage.*.md`.
  - Preconditions: [P0-T6] is complete.
  - Acceptance:
    - exactly one new `baseline-coverage.*.md` artifact exists;
    - it contains `Timestamp:`, `SearchScope:`, `SearchPatterns:`, `SearchResult:`, and `Output Summary:`;
    - it records the numeric baseline coverage values used for this remediation closure workflow;
    - it records which artifact supplied those values, including the primary path `artifacts/pester/powershell-coverage.xml` and, if needed, any supplementary coverage artifact used because the primary artifact lacks Install-layer entries;
    - if numeric coverage values cannot be obtained for the Install-layer scope, the artifact records the blocking reason and baseline coverage evidence remains incomplete.

### Phase 1 — Targeted Remediation Closure Verification

- [x] [P1-T1] Convert the baseline line-count and commit evidence into a targeted REM-01 verification receipt in one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/regression-testing/rem-01-targeted-verification.*.md`.
  - Preconditions: [P0-T3] is complete.
  - Acceptance: exactly one new `rem-01-targeted-verification.*.md` artifact exists and contains `Timestamp:`, `ValidatedCommit: 73d8fc5f038632b25b7c78d33345ecfafa90afc0`, and `Finding: REM-01`.
  - Acceptance: the artifact records `Verdict: CLOSED` only when the `HEAD_SHA` and all three line counts from [P0-T3] match the required values exactly.
  - Acceptance: if the commit or any line count does not match, the artifact records `Verdict: REOPENED` with the mismatched values and the plan stops before checklist updates or reduced re-audit handoff.
  - Acceptance: the artifact explicitly records that `REM-02` and `REM-03` remain out of scope for this closure plan unless later QA evidence proves they block closure.

- [x] [P1-T2] Evaluate whether `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` are fresh for the current branch state against base branch `development`, then record the result in one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/other/pr-context-status.*.md`.
  - Preconditions: [P0-T3] is complete.
  - Acceptance: exactly one new `pr-context-status.*.md` artifact exists and contains `Timestamp:`, `BaseBranch: development`, `CurrentHead: 73d8fc5f038632b25b7c78d33345ecfafa90afc0`, `PRContextStatus:`, and the exact paths `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`.
  - Acceptance: `PRContextStatus:` is recorded as exactly one of `fresh`, `stale`, or `missing`.

- [x] [P1-T3] Refresh the canonical PR-context artifacts against base branch `development` if [P1-T2] recorded `stale` or `missing`; otherwise record that no refresh was required in one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/other/pr-context-refresh.*.md`.
  - Preconditions: [P1-T2] is complete.
  - Acceptance: exactly one new `pr-context-refresh.*.md` artifact exists and contains `Timestamp:`, `BaseBranch: development`, `RefreshAction:`, and `Result:`.
  - Acceptance: if [P1-T2] recorded `stale` or `missing`, `RefreshAction: performed` is recorded and both `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` exist after the refresh.
  - Acceptance: if [P1-T2] recorded `fresh`, `RefreshAction: not required` is recorded and the artifact names the already-current summary and appendix paths.

### Phase 2 — Final QA, Documentation Update, and Reduced Re-Audit Handoff

- [x] [P2-T1] Run the approved PowerShell formatter contract for the final QA pass and write the receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/final-format.2026-04-26T15-51.md`.
  - Preconditions: [P1-T1] and [P1-T3] are complete.
  - Acceptance: `final-format.2026-04-26T15-51.md` exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_format`, `EXIT_CODE: 0`, and `Output Summary:`.
  - Acceptance: if the formatter changes any file or returns a non-zero exit code, the Phase 2 QA loop restarts from [P2-T1] after the change is reconciled.

- [x] [P2-T2] Run the approved PowerShell analyzer contract for the same QA pass and write the receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/final-analyze.2026-04-26T15-51.md`.
  - Preconditions: [P2-T1] is complete.
  - Acceptance: `final-analyze.2026-04-26T15-51.md` exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_analyze`, `EXIT_CODE: 0`, and `Output Summary:`.
  - Acceptance: if the analyzer reports findings or changes require edits, the Phase 2 QA loop restarts from [P2-T1].

- [x] [P2-T3] Run the approved PowerShell test contract for the same QA pass and write the receipt to `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/final-test.2026-04-26T15-51.md`.
  - Preconditions: [P2-T2] is complete.
  - Acceptance: `final-test.2026-04-26T15-51.md` exists and contains `Timestamp:`, `Command: mcp_drmcopilotext_run_poshqc_test`, `EXIT_CODE: 0`, and `Output Summary:`.
  - Acceptance: `Output Summary:` records the final test totals and explicitly names the coverage artifact path or paths that will be used for numeric final coverage verification.
  - Acceptance: numeric final coverage values are not considered complete until [P2-T4] records them in a dedicated final coverage verification artifact.
  - Acceptance: if the test contract fails, the Phase 2 QA loop restarts from [P2-T1].

- [x] [P2-T4] Capture the final numeric PowerShell coverage verdict and coverage-delta verification in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/qa-gates/coverage-delta.2026-04-26T15-51.md`.
  - Preconditions: [P0-T7], [P2-T1], [P2-T2], and [P2-T3] are complete.
  - Acceptance:
    - `coverage-delta.2026-04-26T15-51.md` exists and contains `Timestamp:`, `SearchScope:`, `SearchPatterns:`, `SearchResult:`, and `Output Summary:`;
    - it records the numeric baseline coverage value from [P0-T7];
    - it records the numeric final coverage value for the final QA run;
    - it records the changed-target coverage verdict needed for PowerShell policy closure;
    - it records the exact artifact path or paths used for those values, including whether the primary artifact `artifacts/pester/powershell-coverage.xml` was sufficient or whether a supplementary artifact was required;
    - it records threshold verdicts for overall coverage and changed/new-code coverage;
    - if numeric coverage cannot be established, final QA remains incomplete and REM-01 closure cannot be reported as closed.

- [x] [P2-T5] Create the missing post-remediation validation receipt in one new artifact matching `docs/features/active/2026-04-25-install-hostadapter-not-started-59/evidence/other/post-remediation-validation.*.md`.
  - Preconditions: [P1-T1], [P2-T1], [P2-T2], [P2-T3], and [P2-T4] are complete.
  - Acceptance: exactly one new `post-remediation-validation.*.md` artifact exists and contains `Timestamp:`, `ValidatedCommit: 73d8fc5f038632b25b7c78d33345ecfafa90afc0`, `REM-01 Status: CLOSED`, and `QA Receipts:` referencing `final-format.2026-04-26T15-51.md`, `final-analyze.2026-04-26T15-51.md`, and `final-test.2026-04-26T15-51.md`.
  - Acceptance: the same artifact records `Install.Tests.ps1=456`, `Install.Force.Tests.ps1=163`, and `Install.HostAdapterStart.Tests.ps1=64` as the validated post-remediation file sizes.
  - Acceptance: the same artifact records the numeric baseline and final coverage values referenced from [P0-T7] and [P2-T4]; if either value is absent, the artifact records `ClosureStatus: BLOCKED` and REM-01 is not treated as closed.
  - Acceptance: the same artifact explicitly states that `REM-02` and `REM-03` were not remediated by this plan and remained out of scope unless the final QA pass proved otherwise.

- [x] [P2-T6] Update the relevant feature-folder documentation state to reflect the validated REM-01 closure in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/remediation-inputs.2026-04-26T02-16.md`.
  - Preconditions: [P2-T5] is complete.
  - Acceptance: `remediation-inputs.2026-04-26T02-16.md` contains a `## Remediation Closure Status` section.
  - Acceptance: that section includes the exact bullet `- REM-01 — Closed at HEAD 73d8fc5f038632b25b7c78d33345ecfafa90afc0; validated by evidence/regression-testing/rem-01-targeted-verification and evidence/other/post-remediation-validation artifacts.`
  - Acceptance: the same section includes separate bullets stating that `REM-02` and `REM-03` remain open context items and were not executed under this closure plan.

- [x] [P2-T7] Hand off to the reduced small-path feature review path against base branch `development`, then verify the required post-remediation re-audit artifacts exist and supersede the stale `2026-04-26T02-16` review set.
  - Preconditions: [P2-T5] and [P2-T6] are complete.
  - Acceptance: the handoff requests refreshed review artifacts for `policy-audit`, `code-review`, and `feature-audit` using the canonical feature-review workflow against base branch `development`.
  - Acceptance: one new `policy-audit.*.md`, one new `code-review.*.md`, and one new `feature-audit.*.md` exist in `docs/features/active/2026-04-25-install-hostadapter-not-started-59/` with timestamps newer than `2026-04-26T02-16`.
  - Acceptance: the refreshed `code-review.*.md` and `policy-audit.*.md` no longer report `REM-01` as open, and the refreshed `feature-audit.*.md` still evaluates the `issue.md` `## Acceptance Criteria` set as delivered.
