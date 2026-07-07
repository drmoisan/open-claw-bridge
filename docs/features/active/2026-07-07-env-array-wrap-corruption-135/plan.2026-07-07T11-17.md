# 2026-07-07-env-array-wrap-corruption - Plan

- **Issue:** #135
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T11-17
- **Status:** Ready for Preflight
- **Version:** 1.0
- **Work Mode:** minor-audit (per `issue.md` metadata: `- Work Mode: minor-audit`)

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- PowerShell Standards: `.claude/rules/powershell.md`
- Evidence Conventions: `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`
- Sole Requirements Source (minor-audit): `docs/features/active/2026-07-07-env-array-wrap-corruption-135/issue.md`, specifically its `## Acceptance Criteria` section (AC-1 through AC-6). `spec.md` and `user-story.md` are intentionally absent for this minor-audit work mode and are NOT required.

**All work must comply with these policies; do not duplicate their content here.**

## Conventions Used by This Plan

- `FEATURE` = `docs/features/active/2026-07-07-env-array-wrap-corruption-135`
- `<ts>` = ISO-8601 timestamp `yyyy-MM-ddTHH-mm` captured at artifact-creation time.
- All evidence artifacts live under `FEATURE/evidence/<kind>/` (canonical kinds used by this plan: `baseline/`, `regression-testing/`, `qa-gates/`). No evidence may be written under any `artifacts/` sub-path. Every command-step evidence artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- **PowerShell toolchain loop** (Phases 0 and 2): run `mcp__drm-copilot__run_poshqc_format` -> `mcp__drm-copilot__run_poshqc_analyze` -> `mcp__drm-copilot__run_poshqc_test` in that order, repo-wide. If any step fails or changes files, restart the loop from format. `EXIT_CODE: SKIPPED` is never a passing outcome for any command task in this plan; every Phase 2 command task is unconditional (no IN_SCOPE/OUT_OF_SCOPE branching).
- **Coverage tooling note (mandatory workaround):** `mcp__drm-copilot__run_poshqc_test` in coverage mode fails on every invocation in this repository because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to files that exist only in the `drm-copilot` source repository, and the repo-local override path referenced in `.claude/rules/powershell.md` (`scripts/powershell/PoshQC/settings/pester.runsettings.psd1`) does not exist here. This is an established, reproduced defect (F11 `#111`, F16 `#125`), not something to re-diagnose. The numeric-coverage source for both baseline and final-QC tasks below is: import the bundled `PoshQC.psd1` module directly and call `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>`, where `<corrected>` is a SCRATCHPAD-only copy of the bundled runsettings with `CodeCoverage.Path` rewritten to this repository's actual production PowerShell files under `scripts/**` (glob all `.ps1`/`.psm1`) and no `ExcludedPath` entry (per `.claude/rules/general-unit-test.md`'s Coverage Exclusion Policy, no production file may be excluded from measurement). Pester v5 emits command-level coverage only; record that percentage as the line/branch proxy, consistent with prior precedent. Do not write the corrected runsettings file into the repo tree.
- **AC tracking source:** per minor-audit mode, the sole AC source is `FEATURE/issue.md`'s `## Acceptance Criteria` section (AC-1 through AC-6). No other document is treated as an AC source.
- **Change budget:** this fix touches exactly 2 production PowerShell files (`scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`) and 2 test files (`tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1`), within the direct-mode budget (up to 2 production files) and the per-batch cap (at most 3 production + 3 test files) in `.claude/rules/powershell.md`. `scripts/Publish.Env.psm1` and `README.md` are explicitly out of scope and must remain byte-identical.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read `CLAUDE.md` at the repository root.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` is created (or appended to, if already created by a later task in this phase) recording that `CLAUDE.md` was read, with a `Timestamp:` field.
- [x] [P0-T2] Read `.claude/rules/general-code-change.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-code-change.md` as read.
- [x] [P0-T3] Read `.claude/rules/general-unit-test.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-unit-test.md` as read.
- [x] [P0-T4] Read `.claude/rules/powershell.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/powershell.md` as read.
- [x] [P0-T5] Finalize the Phase 0 policy-read evidence artifact covering P0-T1 through P0-T4.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` exists containing `Timestamp:`, `Policy Order:` (listing the four files in the exact order read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`), and an explicit list of files read.
- [x] [P0-T6] Capture the PowerShell format baseline: run `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail status and count of files changed by the run).
- [x] [P0-T7] Capture the PowerShell analyze baseline: run `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (diagnostic counts by severity, expected 0 errors).
- [x] [P0-T8] Capture the PowerShell test-and-coverage baseline: run `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround (`Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad-corrected-runsettings>`, scoped to all `scripts/**` `.ps1`/`.psm1` files with no `ExcludedPath`) and record the numeric result.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings workaround invocation), `EXIT_CODE:` for each, and `Output Summary:` containing the pass/fail test counts and the numeric repo-wide baseline command/line-coverage percentage (no placeholders).

### Phase 1 — Constrained Small-Path Implementation Placeholder

- [x] [P1-T1] Hand off the constrained implementation batch to `powershell-typed-engineer` via `atomic-executor`, scoped strictly to the two production call-site edits, the two test-mock parity fixes, and the two multi-line regression test additions confirmed in `FEATURE/issue.md`'s `## Suspected Cause / Notes` section; the delegation prompt must enumerate AC-1 through AC-5 verbatim from `FEATURE/issue.md`'s `## Acceptance Criteria` section and must explicitly prohibit any edit to `scripts/Publish.Env.psm1` or `README.md`.
  - Acceptance: the delegation prompt sent to `atomic-executor`/`powershell-typed-engineer` names all six in-scope files (`scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/New-MsixDevCert.Tests.ps1` as editable; `scripts/Publish.Env.psm1`, `README.md` as explicitly out of scope) and quotes AC-1 through AC-5 verbatim.
- [x] [P1-T2] Apply the confirmed fix at `scripts/Publish.ps1` line 118: replace `$envContent = @(Read-EnvFileContent -Path $EnvFilePath)` with `$envContent = Read-EnvFileContent -Path $EnvFilePath`.
  - Acceptance: AC-1 — `git diff scripts/Publish.ps1` shows exactly this one-line change and no other line in the file differs.
- [x] [P1-T3] Apply the confirmed fix at `scripts/New-MsixDevCert.ps1` line 72: replace `$content = @(Read-EnvFileContent -Path $EnvPath)` with `$content = Read-EnvFileContent -Path $EnvPath`.
  - Acceptance: AC-2 — `git diff scripts/New-MsixDevCert.ps1` shows exactly this one-line change and no other line in the file differs.
- [x] [P1-T4] Confirm `scripts/Publish.Env.psm1` and `README.md` remain unchanged by this fix.
  - Acceptance: AC-3 — `git diff scripts/Publish.Env.psm1 README.md` returns no output (both files byte-identical to their pre-fix state).
- [x] [P1-T5] Update the `Read-EnvFileContent` mock in `tests/scripts/Publish.Tests.ps1` (the `BeforeEach` block, currently lines 52-58) to return via the production-parity unary-comma idiom: `return , ([string[]]@($global:PublishTestEnvContent))`.
  - Acceptance: AC-4 (Publish.Tests.ps1 half) — the mock's `return` statement in the file matches the unary-comma idiom exactly, and all pre-existing `It` blocks in this file continue to pass after the change.
- [x] [P1-T6] Update the `Read-EnvFileContent` mock in `tests/scripts/New-MsixDevCert.Tests.ps1` (the `Save-CertThumbprintToEnv` context, currently lines 119-123) to return via the production-parity unary-comma idiom: `return , ([string[]]@('OPENCLAW_PACKAGE_VERSION=1.0.2.0', '# comment'))`.
  - Acceptance: AC-4 (New-MsixDevCert.Tests.ps1 half) — the mock's `return` statement in the file matches the unary-comma idiom exactly, and all pre-existing `It` blocks in this context continue to pass after the change.
- [x] [P1-T7] Add one new `It` block to `tests/scripts/Publish.Tests.ps1`'s version-persistence context, using a fixture of at least one comment line and at least two `KEY=value` lines (mocking only `Read-EnvFileContent` and `Write-EnvFileContent`), asserting that the content passed to `Write-EnvFileContent` preserves every original fixture line verbatim except the updated `OPENCLAW_PACKAGE_VERSION` key, is not collapsed into a single space-joined line, and contains no duplicate `OPENCLAW_PACKAGE_VERSION` key.
  - Acceptance: AC-5 (Publish.Tests.ps1 half) — the new `It` block exists, asserts all four conditions (line preservation, single in-place key update, no space-joined collapse, no duplicate key), and passes once P1-T2 and P1-T5 are applied.
- [x] [P1-T8] Add one new `It` block to `tests/scripts/New-MsixDevCert.Tests.ps1`'s `Save-CertThumbprintToEnv` context, using a fixture of at least one comment line and at least two `KEY=value` lines (mocking only `Read-EnvFileContent` and `Write-EnvFileContent`, and using the real `Set-EnvFileValue` from `scripts/Publish.Env.psm1` unmocked in this `It` block), asserting that the content passed to `Write-EnvFileContent` preserves every original fixture line verbatim except the updated `OPENCLAW_CERT_THUMBPRINT` key, is not collapsed into a single space-joined line, and contains no duplicate `OPENCLAW_CERT_THUMBPRINT` key.
  - Acceptance: AC-5 (New-MsixDevCert.Tests.ps1 half) — the new `It` block exists, uses the real `Set-EnvFileValue` (not a mock) as its update mechanism, asserts all four conditions, and passes once P1-T3 and P1-T6 are applied.
- [x] [P1-T9] Run the two edited test files in targeted mode (`mcp__drm-copilot__run_poshqc_test` scoped to `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1`, or the corrected-runsettings workaround scoped the same way if the MCP tool's coverage-path defect blocks the run) to confirm the P1-T2 through P1-T8 edits are internally consistent before the full-repo Phase 2 loop runs.
  - Acceptance: `FEATURE/evidence/regression-testing/targeted-verification.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` confirming all tests in both files pass, including the two new regression tests added in P1-T7 and P1-T8.

### Phase 2 — Final QC Loop

- [x] [P2-T1] Run the final PowerShell format check: `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) over the full repository, including the four edited files. If the run fails or modifies any file, apply the needed fix and restart this task before proceeding to P2-T2.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (pass status, 0 files changed on the recorded clean pass).
- [x] [P2-T2] Run the final PowerShell analyzer check: `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) over the full repository. If any error-severity finding is reported, apply the needed fix and restart from P2-T1.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (diagnostic counts by severity, 0 errors).
- [x] [P2-T3] Run the final PowerShell test-and-coverage check: `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround (`Invoke-PoshQCTest -Root <repo> -SettingsPath <scratchpad-corrected-runsettings>`, scoped to all `scripts/**` `.ps1`/`.psm1` files with no `ExcludedPath`) over the full repository, including the four edited files and the two new regression tests. If any test fails, apply the needed fix and restart from P2-T1.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings workaround invocation), `EXIT_CODE:` for each, and `Output Summary:` containing full pass/fail test counts (all tests passing, including the two new regression tests from P1-T7/P1-T8) and the numeric repo-wide post-change command/line-coverage percentage (no placeholders).
- [x] [P2-T4] Produce the coverage delta/threshold verification comparing the P0-T8 baseline against the P2-T3 post-change values: confirm repo-wide coverage shows no regression versus baseline, confirm coverage for the two changed production files (`scripts/Publish.ps1`, `scripts/New-MsixDevCert.ps1`) meets or exceeds their pre-change per-file coverage, confirm line coverage >= 85% and the command-coverage branch proxy >= 75% per `.claude/rules/general-unit-test.md`, and confirm no production PowerShell file was excluded from measurement.
  - Acceptance: `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md` reports baseline coverage, post-change coverage, and per-changed-file coverage as numeric values with an explicit PASS/FAIL against each threshold; any below-threshold, regressed, or unavailable value makes the outcome remediation-required, not PASS.
- [x] [P2-T5] Confirm AC-6 is satisfied: verify P2-T1 (`EXIT_CODE: 0`), P2-T2 (`EXIT_CODE: 0`, 0 errors), P2-T3 (all tests passing, `EXIT_CODE: 0` on the workaround invocation), and P2-T4 (PASS on all thresholds, no regression) all hold simultaneously, then check off AC-1 through AC-6 in `FEATURE/issue.md`'s `## Acceptance Criteria` section.
  - Acceptance: `FEATURE/evidence/qa-gates/ac6-acceptance-summary.<ts>.md` lists each of AC-1 through AC-6 with a PASS status and a pointer to its supporting evidence artifact (P1-T2/P1-T3/P1-T4/P1-T5+P1-T6/P1-T7+P1-T8/P2-T1 through P2-T4 respectively); all six `- [ ]` checkboxes in `FEATURE/issue.md`'s `## Acceptance Criteria` section are changed to `- [x]` with criterion text unchanged.

## Test Plan

- Unit (Pester v5): `tests/scripts/Publish.Tests.ps1` (new multi-line `.env` regression test, P1-T7) and `tests/scripts/New-MsixDevCert.Tests.ps1` (new multi-line `.env` regression test using the real `Set-EnvFileValue`, P1-T8) — both fail if the redundant `@(...)` wrap is reintroduced at their respective call sites and pass with the fix applied.
- Targeted verification: `FEATURE/evidence/regression-testing/targeted-verification.<ts>.md` (P1-T9).
- Coverage evidence (PowerShell):
  - Baseline: `FEATURE/evidence/baseline/poshqc-test.<ts>.md` (P0-T8)
  - Post-change: `FEATURE/evidence/qa-gates/final-poshqc-test.<ts>.md` (P2-T3)
  - Comparison: `FEATURE/evidence/qa-gates/coverage-comparison.<ts>.md` (P2-T4)

## Open Questions / Notes

- No `spec.md` or `user-story.md` exists in this feature folder; per minor-audit mode this is expected and is not a blocker.
- `scripts/Publish.Env.psm1`'s unary-comma return idiom is confirmed correct and is not modified by this plan; only its two redundant-`@()` call sites are fixed.
- `README.md`'s example was already corrected by a prior session and is left as-is; this plan does not touch it.
