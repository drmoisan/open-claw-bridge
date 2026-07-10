# hostadapter-v1-basepath-mismatch (Plan)

- **Issue:** #137
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-10T09-24
- **Status:** Ready for Preflight
- **Version:** 1.0
- **Work Mode:** full-bug (per `issue.md` metadata: `- Work Mode: full-bug`)

## Required References

- General Coding Standards: `.claude/rules/general-code-change.md`
- General Unit Test Policy: `.claude/rules/general-unit-test.md`
- PowerShell Standards: `.claude/rules/powershell.md`
- C# Standards: `.claude/rules/csharp.md`
- Evidence Conventions: `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`
- AC source (authoritative, full-bug mode): `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/spec.md`, `## Acceptance Criteria` section (8 items, AC-1 through AC-8 below). `user-story.md` does not exist in this feature folder and is not required for full-bug mode.
- Bug detail: `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/issue.md`
- Confirmed research: `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/research/2026-07-10T10-15-basepath-mismatch-confirmation-research.md`

**All work must comply with these policies; do not duplicate their content here.**

## Acceptance Criteria (verbatim from `spec.md`)

- AC-1: `.env.example`'s default `OpenClaw__HostAdapter__BaseUrl` (line 3) has no `/v1` segment.
- AC-2: Both `docker-compose.yml` occurrences (lines 27 and 73) and the `docker-compose.dev.yml` occurrence (line 14) default `OpenClaw__HostAdapter__BaseUrl` have no `/v1` segment.
- AC-3: `src/OpenClaw.Core/Program.cs`'s blank-config fallback (line 17) resolves to `http://host.docker.internal:4319/` (no `/v1`, trailing slash preserved).
- AC-4: `scripts/Install.Preflight.psm1`'s default base URL (line 73) has no `/v1` segment.
- AC-5: A PowerShell test asserts the `Install.Preflight` default preflight URL contains no `/v1` segment.
- AC-6: A C# test asserts `OpenClaw.Core`'s resolved `HostAdapter.BaseUrl` fallback (blank config) contains no `/v1` segment.
- AC-7: `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` continues to pass unchanged.
- AC-8: Full PowerShell toolchain (PoshQC format -> analyze -> Pester) and C# toolchain (CSharpier -> analyzers/nullable -> xUnit) pass with no coverage regression on changed lines.

## Explicit Non-Goals

- Do NOT add `/v1` routing to `src/OpenClaw.HostAdapter/Program.cs`. Its root-scoped route surface (`/status`, `/users/{id}/messages`, etc.) is the correct, intended contract.
- Do NOT modify `src/OpenClaw.Core/CoreOptions.cs`. Its class-level default (`"http://host.docker.internal:4319/"`, line 16) is already correct with no `/v1` segment.
- Do NOT weaken or alter the existing assertions in `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`. It already reflects the corrected contract and must continue to pass unchanged. This file is also a pre-existing 500-line-cap violation (616 lines) and must not be extended; the new C# regression test (AC-6) is added to a new sibling file instead (see Phase 1).
- `.env` is gitignored/untracked and is NOT a plan deliverable. It is out of PR scope; the `.env.example` correction (AC-1) is the committable fix, and operators receive the corrected default by re-copying the template.

## Conventions Used by This Plan

- `FEATURE` = `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137`
- `<ts>` = ISO-8601 timestamp `yyyy-MM-ddTHH-mm` captured at artifact-creation time.
- All evidence artifacts live under `FEATURE/evidence/<kind>/` (canonical kinds used by this plan: `baseline/`, `regression-testing/`, `qa-gates/`, `other/`, `issue-updates/`). No evidence may be written under any `artifacts/` sub-path. Every command-step evidence artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- Raw C# command intermediates (TRX, Cobertura XML, `.coverage` binaries) are written to `artifacts/csharp/<stage>/` (non-evidence raw output; permitted per established precedent). The summarizing markdown evidence lives under `FEATURE/evidence/<kind>/`.
- **PowerShell toolchain loop** (Phases 0 and 5): run `mcp__drm-copilot__run_poshqc_format` -> `mcp__drm-copilot__run_poshqc_analyze` -> `mcp__drm-copilot__run_poshqc_test` in that order, repo-wide. If any step fails or changes files, restart the loop from format.
- **PowerShell coverage tooling note (established workaround):** `mcp__drm-copilot__run_poshqc_test` in coverage mode fails on every invocation in this repository because its bundled `pester.runsettings.psd1` hardcodes `CodeCoverage.Path` to files that exist only in the `drm-copilot` source repository (reproduced defect F11 `#111`, F16 `#125`, confirmed again at `#135`). The numeric-coverage source for both baseline and final-QC PowerShell tasks is: import the bundled `PoshQC.psd1` module directly and call `Invoke-PoshQCTest -Root <repo> -SettingsPath <corrected>`, where `<corrected>` is a SCRATCHPAD-only copy of the bundled runsettings with `CodeCoverage.Path` rewritten to this repository's actual production PowerShell files under `scripts/**` (full glob, no `ExcludedPath` entry per the Coverage Exclusion Policy). Record both the failing MCP invocation and the corrected-runsettings invocation in the evidence artifact.
- **C# toolchain loop** (Phases 0 and 5): `csharpier check .` (format check) / `csharpier format .` (auto-fix) using the global CSharpier 1.3.0 tool -> `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as errors via `Directory.Build.props`) -> `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/<stage>`. Do NOT use `dotnet csharpier` (no working local tool manifest in this repo). Architecture-boundary (`NetArchTest.Rules`) tests execute as part of the `OpenClaw.Core.Tests` run within this same `dotnet test` invocation; no separate architecture-stage command is required.
- **`.env.example` edit channel (mandatory):** `.claude/settings.json` denies the `Read` tool on `.env` and `.env.*`, which transitively blocks `Edit` (requires a prior `Read`) and overwrite-`Write` (same prior-`Read` requirement) on `.env.example`. The executor MUST edit this file via a `Bash(pwsh *)` one-liner, not the `Read`/`Edit`/`Write` file tools, and MUST confirm the result via `git show`/`git diff` (git is not deny-gated), not the `Read` tool. Recommended command form (from confirmed research Section 4):
  ```powershell
  $p = '.env.example'
  (Get-Content -Raw -LiteralPath $p) -replace '(OpenClaw__HostAdapter__BaseUrl=http://host\.docker\.internal:4319)/v1(\r?\n|$)', '$1$2' | Set-Content -NoNewline -LiteralPath $p
  ```
- **Change budget:** this fix touches 5 tracked production/config files (`.env.example`, `docker-compose.yml`, `docker-compose.dev.yml`, `src/OpenClaw.Core/Program.cs`, `scripts/Install.Preflight.psm1`) and adds/extends 2 test files (`tests/scripts/Install.Preflight.Tests.ps1`, new `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs`). `src/OpenClaw.Core/CoreOptions.cs`, `src/OpenClaw.HostAdapter/Program.cs`, and `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` are explicitly out of scope and must remain byte-identical.
- **Test file line-count check (performed during planning):** `tests/scripts/Install.Preflight.Tests.ps1` is 398 lines (room under the 500-line cap for the new test in Phase 1). `tests/scripts/Install.Tests.ps1` is already at 505 lines (over cap) and MUST NOT receive new tests from this plan. `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` is 616 lines (over cap, pre-existing violation) and MUST NOT be extended; AC-6 is satisfied via a new sibling file.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read `CLAUDE.md` at the repository root.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` is created (or appended to) recording that `CLAUDE.md` was read, with a `Timestamp:` field.
- [x] [P0-T2] Read `.claude/rules/general-code-change.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-code-change.md` as read.
- [x] [P0-T3] Read `.claude/rules/general-unit-test.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/general-unit-test.md` as read.
- [x] [P0-T4] Read `.claude/rules/powershell.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/powershell.md` as read.
- [x] [P0-T5] Read `.claude/rules/csharp.md`.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` lists `.claude/rules/csharp.md` as read.
- [x] [P0-T6] Finalize the Phase 0 policy-read evidence artifact covering P0-T1 through P0-T5.
  - Acceptance: `FEATURE/evidence/baseline/phase0-instructions-read.<ts>.md` exists containing `Timestamp:`, `Policy Order:` (listing the five files in the exact order read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`, `.claude/rules/csharp.md`), and an explicit list of files read.
- [x] [P0-T7] Capture the PowerShell format baseline: run `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail status and count of files changed by the run).
- [x] [P0-T8] Capture the PowerShell analyze baseline: run `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (diagnostic counts by severity, expected 0 errors).
- [x] [P0-T9] Capture the PowerShell test-and-coverage baseline: run `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround and record the numeric result.
  - Acceptance: `FEATURE/evidence/baseline/poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings workaround invocation), `EXIT_CODE:` for each, and `Output Summary:` containing pass/fail test counts and the numeric repo-wide baseline command/line-coverage percentage (no placeholders).
- [x] [P0-T10] Capture the C# format baseline: run `csharpier check .` from the repo root against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/csharp-format.<ts>.md` exists with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, `Output Summary:` (pass/fail status and count of files checked/needing formatting).
- [x] [P0-T11] Capture the C# build/analyzer/nullable baseline: run `dotnet build OpenClaw.MailBridge.sln` against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/csharp-build.<ts>.md` exists with `Timestamp:`, `Command: dotnet build OpenClaw.MailBridge.sln`, `EXIT_CODE:`, `Output Summary:` (warning/error counts, expected 0/0).
- [x] [P0-T12] Capture the C# test/coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/baseline` against the current pre-fix repository state.
  - Acceptance: `FEATURE/evidence/baseline/csharp-test-coverage.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` containing per-project pass/fail/skip counts and the numeric `OpenClaw.Core` line-rate and branch-rate coverage percentages from the generated Cobertura file(s) under `artifacts/csharp/baseline/`.

### Phase 1 — Regression Tests (must fail first)

- [x] [P1-T1] [expect-fail] Add one new Pester `It` block to `tests/scripts/Install.Preflight.Tests.ps1` asserting that `Get-HostAdapterPreflightUri` (called with an `$EnvMap` that omits the `OpenClaw__HostAdapter__BaseUrl` key, exercising the default-`$baseUrl` path) returns a URI whose `.AbsolutePath` does not contain `/v1`.
  - Acceptance: AC-5 (test-authored half) — the new `It` block exists in `tests/scripts/Install.Preflight.Tests.ps1`, imports `scripts/Install.Preflight.psm1` per the file's existing `Import-Module ... -Force` pattern, and asserts absence of `/v1` in the resolved URI's path (not presence of any specific replacement string).
- [x] [P1-T2] [expect-fail] Run the new `It` block from P1-T1 in targeted mode against the current (pre-fix) `scripts/Install.Preflight.psm1` and confirm it fails.
  - Acceptance: `FEATURE/evidence/regression-testing/ps-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:` (targeted Pester invocation scoped to the new `It` block), `EXIT_CODE:` (non-zero / test-failure exit), `Output Summary:` recording the failure and quoting the assertion failure message showing the current default resolves to a path containing `/v1`.
- [x] [P1-T3] [expect-fail] Add a new C# test file `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` with a `[TestClass]` containing a `[TestMethod]` that builds a `CoreTestWebApplicationFactory` overridden via `WithWebHostBuilder` to bind `OpenClaw:HostAdapter:BaseUrl` to an empty string (exercising the `Program.cs:16-18` `PostConfigure` blank-config fallback branch), resolves `IOptions<OpenClawOptions>` from the factory's service provider, and asserts the resolved `HostAdapter.BaseUrl` does not contain `/v1`.
  - Acceptance: AC-6 (test-authored half) — the new file exists, is a new sibling file (does not modify `HostAdapterHttpClientTests.cs`), uses `[TestClass]`/`[TestMethod]` and FluentAssertions consistent with the surrounding `OpenClaw.Core.Tests` convention, and its assertion targets absence of `/v1` (not presence of a specific replacement string).
- [x] [P1-T4] [expect-fail] Run the new test from P1-T3 in targeted mode against the current (pre-fix) `src/OpenClaw.Core/Program.cs` and confirm it fails.
  - Acceptance: `FEATURE/evidence/regression-testing/csharp-expect-fail.<ts>.md` exists with `Timestamp:`, `Command:` (e.g. `dotnet test OpenClaw.MailBridge.sln --filter "FullyQualifiedName~CoreHostAdapterBaseUrlFallbackTests"`), `EXIT_CODE:` (non-zero / test-failure exit), `Output Summary:` recording the failure and quoting the assertion failure message showing the current fallback resolves to a value containing `/v1`.

### Phase 2 — Minimal Fix (mechanical `/v1` strip)

- [x] [P2-T1] Strip the stray `/v1` segment from `.env.example` line 3 via the `Bash(pwsh *)` one-liner specified in Conventions (do NOT use `Read`/`Edit`/`Write` on this file).
  - Acceptance: AC-1 — the `pwsh` one-liner is executed and exits 0.
- [x] [P2-T2] Confirm the `.env.example` edit via `git diff -- .env.example` (not the `Read` tool).
  - Acceptance: AC-1 — `git diff -- .env.example` shows exactly one changed line, the `OpenClaw__HostAdapter__BaseUrl` line, with `/v1` removed and no other content altered; `git show :.env.example | Select-String 'OpenClaw__HostAdapter__BaseUrl'` (or equivalent) confirms the line no longer contains `/v1`.
- [x] [P2-T3] In `docker-compose.yml` line 27, replace `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}` with `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319}`.
  - Acceptance: AC-2 (occurrence 1 of 3) — `git diff docker-compose.yml` shows this line changed with `/v1` removed and no other line in the file differs from this edit.
- [x] [P2-T4] In `docker-compose.yml` line 73, replace `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}` with `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319}`.
  - Acceptance: AC-2 (occurrence 2 of 3) — `git diff docker-compose.yml` shows both P2-T3 and this line changed with `/v1` removed, and no other line in the file differs.
- [x] [P2-T5] In `docker-compose.dev.yml` line 14, replace `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}` with `${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319}`.
  - Acceptance: AC-2 (occurrence 3 of 3) — `git diff docker-compose.dev.yml` shows exactly this one-line change and no other line in the file differs.
- [x] [P2-T6] In `src/OpenClaw.Core/Program.cs` line 17, replace `"http://host.docker.internal:4319/v1/"` with `"http://host.docker.internal:4319/"` (trailing slash preserved).
  - Acceptance: AC-3 — `git diff src/OpenClaw.Core/Program.cs` shows exactly this one-line change; the replacement string is `"http://host.docker.internal:4319/"` with no `/v1` and the trailing slash intact.
- [x] [P2-T7] In `scripts/Install.Preflight.psm1` line 73, replace `$baseUrl = 'http://host.docker.internal:4319/v1'` with `$baseUrl = 'http://host.docker.internal:4319'`.
  - Acceptance: AC-4 — `git diff scripts/Install.Preflight.psm1` shows exactly this one-line change and no other line in the file differs.
- [x] [P2-T8] Confirm `src/OpenClaw.Core/CoreOptions.cs` remains byte-identical to its pre-fix state (non-goal verification).
  - Acceptance: `git diff src/OpenClaw.Core/CoreOptions.cs` returns no output.
- [x] [P2-T9] Confirm `src/OpenClaw.HostAdapter/Program.cs` remains byte-identical to its pre-fix state and no `/v1` route/group/prefix was added (non-goal verification).
  - Acceptance: `git diff src/OpenClaw.HostAdapter/Program.cs` returns no output; a repository-wide grep for `v1` under `src/OpenClaw.HostAdapter/` returns zero matches (unchanged from the confirmed research baseline).

### Phase 3 — Post-Fix Regression Verification

- [x] [P3-T1] Re-run the P1-T1 `It` block from `tests/scripts/Install.Preflight.Tests.ps1` in targeted mode against the fixed `scripts/Install.Preflight.psm1` and confirm it now passes.
  - Acceptance: AC-5 (verification half) — `FEATURE/evidence/regression-testing/ps-post-fix-pass.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` confirming the P1-T1 assertion now passes.
- [x] [P3-T2] Re-run the P1-T3 test from `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` in targeted mode against the fixed `src/OpenClaw.Core/Program.cs` and confirm it now passes.
  - Acceptance: AC-6 (verification half) — `FEATURE/evidence/regression-testing/csharp-post-fix-pass.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` confirming the P1-T3 assertion now passes.
- [x] [P3-T3] Run `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` in targeted mode against the fixed repository state.
  - Acceptance: AC-7 (execution half) — `FEATURE/evidence/regression-testing/hostadapterhttpclienttests-pass.<ts>.md` exists with `Timestamp:`, `Command:` (`dotnet test OpenClaw.MailBridge.sln --filter "FullyQualifiedName~HostAdapterHttpClientTests"`), `EXIT_CODE: 0`, `Output Summary:` recording all tests in the file passing.
- [x] [P3-T4] Confirm `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` is byte-identical to its pre-fix state (no edits were made to it).
  - Acceptance: AC-7 (unchanged half) — `git diff tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` returns no output.

### Phase 4 — Manual/Integration Verification Note (not automated this pass)

- [x] [P4-T1] Record a manual/integration verification note documenting the required future validation: publish a fresh bundle (`scripts/Publish.ps1`) and run the full `Install.ps1` flow end-to-end through the Docker stage (not `-SkipDocker`) with an operator `.env` left at its (corrected) defaults, confirming the preflight probe succeeds and the Docker stack starts.
  - Acceptance: `FEATURE/evidence/other/manual-verification-note.<ts>.md` exists, explicitly states this verification is NOT automated in this pass, names the exact command sequence to run (`scripts/Publish.ps1` then `Install.ps1 -DockerEnvFilePath ... -AnthropicEnvFilePath ...` without `-SkipDocker`), and states the expected outcome (preflight probe against `GET http://127.0.0.1:4319/status` succeeds, Docker stage proceeds).

### Phase 5 — Final QA Loop

- [x] [P5-T1] Run the final PowerShell format check: `mcp__drm-copilot__run_poshqc_format` (workspace_root = repo root) over the full repository, including the two edited/added files (`scripts/Install.Preflight.psm1`, `tests/scripts/Install.Preflight.Tests.ps1`). If the run fails or modifies any file, apply the needed fix and restart this task before proceeding to P5-T2.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-format.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (pass status, 0 files changed on the recorded clean pass).
- [x] [P5-T2] Run the final PowerShell analyzer check: `mcp__drm-copilot__run_poshqc_analyze` (workspace_root = repo root) over the full repository. If any error-severity finding is reported, apply the needed fix and restart from P5-T1.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-analyze.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (diagnostic counts by severity, 0 errors).
- [x] [P5-T3] Run the final PowerShell test-and-coverage check: `mcp__drm-copilot__run_poshqc_test`; when it fails on the known coverage-path defect, apply the corrected-runsettings workaround over the full repository, including the new regression test from P1-T1. If any test fails, apply the needed fix and restart from P5-T1.
  - Acceptance: `FEATURE/evidence/qa-gates/final-poshqc-test.<ts>.md` exists with `Timestamp:`, `Command:` (both the failing MCP invocation and the corrected-runsettings workaround invocation), `EXIT_CODE:` for each, `Output Summary:` containing full pass/fail test counts (all tests passing, including the new P1-T1 regression test) and the numeric repo-wide post-change command/line-coverage percentage (no placeholders).
- [x] [P5-T4] Run the final C# format check: `csharpier check .` from the repo root over the full repository, including `src/OpenClaw.Core/Program.cs` and the new `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs`. If any file requires formatting, run `csharpier format .` and restart this task.
  - Acceptance: `FEATURE/evidence/qa-gates/final-csharp-format.<ts>.md` exists with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE: 0`, `Output Summary:` (pass status, 0 files requiring formatting on the recorded clean pass).
- [x] [P5-T5] Run the final C# build/analyzer/nullable check: `dotnet build OpenClaw.MailBridge.sln`. If any warning-as-error or nullable diagnostic is reported, apply the needed fix and restart from P5-T4.
  - Acceptance: `FEATURE/evidence/qa-gates/final-csharp-build.<ts>.md` exists with `Timestamp:`, `Command: dotnet build OpenClaw.MailBridge.sln`, `EXIT_CODE: 0`, `Output Summary:` (0 Warning(s), 0 Error(s), all projects compiled).
- [x] [P5-T6] Run the final C# test/coverage check: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/csharp/final`, including the new `CoreHostAdapterBaseUrlFallbackTests.cs` and the unmodified `HostAdapterHttpClientTests.cs`. If any test fails, apply the needed fix and restart from P5-T4.
  - Acceptance: `FEATURE/evidence/qa-gates/final-csharp-test-coverage.<ts>.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` containing per-project pass/fail/skip counts (all passing, including the new test from P1-T3) and the numeric post-change `OpenClaw.Core` line-rate and branch-rate coverage percentages from the generated Cobertura file(s) under `artifacts/csharp/final/`.
- [x] [P5-T7] Produce the PowerShell coverage delta/threshold verification comparing the P0-T9 baseline against the P5-T3 post-change values.
  - Acceptance: `FEATURE/evidence/qa-gates/coverage-comparison-powershell.<ts>.md` reports baseline coverage, post-change coverage, and coverage for the changed file (`scripts/Install.Preflight.psm1`) as numeric values with an explicit PASS/FAIL against: no repo-wide regression versus baseline, line coverage >= 85%, command-coverage branch proxy >= 75%, and no production PowerShell file excluded from measurement. Any below-threshold, regressed, or unavailable value makes the outcome remediation-required, not PASS.
- [x] [P5-T8] Produce the C# coverage delta/threshold verification comparing the P0-T12 baseline against the P5-T6 post-change values.
  - Acceptance: `FEATURE/evidence/qa-gates/coverage-comparison-csharp.<ts>.md` reports baseline coverage, post-change coverage, and coverage for the changed file (`src/OpenClaw.Core/Program.cs`) and the new file (`tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs`, test file — excluded from the production denominator per policy) as numeric values with an explicit PASS/FAIL against: no `OpenClaw.Core` regression versus baseline, line coverage >= 85%, branch coverage >= 75%. Any below-threshold, regressed, or unavailable value makes the outcome remediation-required, not PASS.
- [x] [P5-T9] Confirm AC-8 is satisfied: verify P5-T1 through P5-T6 all report `EXIT_CODE: 0` and P5-T7/P5-T8 both report PASS on every threshold simultaneously, in a single clean pass with no restart pending.
  - Acceptance: `FEATURE/evidence/qa-gates/ac8-toolchain-summary.<ts>.md` lists each of P5-T1 through P5-T8 with a PASS status and a pointer to its supporting evidence artifact; confirms no step in the final pass triggered a restart-from-P5-T1/P5-T4 loop after the recorded clean-pass artifacts.

### Phase 6 — Acceptance Criteria, Issue Update, and PR Handoff

- [x] [P6-T1] Confirm AC-1 through AC-8 all hold simultaneously by cross-referencing their supporting evidence artifacts (P2-T1/P2-T2 for AC-1; P2-T3 through P2-T5 for AC-2; P2-T6 for AC-3; P2-T7 for AC-4; P1-T1/P3-T1 for AC-5; P1-T3/P3-T2 for AC-6; P3-T3/P3-T4 for AC-7; P5-T9 for AC-8), then check off all 8 boxes in `FEATURE/spec.md`'s `## Acceptance Criteria` section.
  - Acceptance: `FEATURE/evidence/qa-gates/ac-summary.<ts>.md` lists each of AC-1 through AC-8 with a PASS status and a pointer to its supporting evidence artifact; all eight `- [ ]` checkboxes in `FEATURE/spec.md`'s `## Acceptance Criteria` section are changed to `- [x]` with criterion text unchanged.
- [x] [P6-T2] Mirror an issue-update comment/body change for Issue #137 summarizing the fix (six-location `/v1` strip, two new regression tests, full toolchain pass) per the issue-update mirroring convention.
  - Acceptance: `FEATURE/evidence/issue-updates/issue-137.<ts>.md` exists with `Timestamp:`, the exact text intended/posted, `PostedAs: body` or `PostedAs: comment` (or `POSTING BLOCKED` with reason if not posted), and the GitHub URL when posted.
- [x] [P6-T3] Prepare PR notes (summary, risks, validation performed, links to evidence and tests) referencing this plan's Phase 5 QA evidence and the Phase 4 manual-verification note as an outstanding follow-up.
  - Acceptance: A PR description draft exists (as a task output, e.g. recorded in `FEATURE/evidence/other/pr-notes.<ts>.md`) that names all 5 changed tracked files, the 2 new/extended test files, links to the Phase 5 QA-gate evidence paths, and explicitly calls out the Phase 4 manual Docker-stage verification as not yet performed.

## Test Plan

- Unit (Pester v5): `tests/scripts/Install.Preflight.Tests.ps1` — new `It` block (P1-T1) asserting `Get-HostAdapterPreflightUri`'s default resolves to a URI with no `/v1` segment; fails before the fix (P1-T2), passes after (P3-T1).
- Unit (MSTest + FluentAssertions): new file `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` (P1-T3) asserting the resolved `HostAdapter.BaseUrl` fallback (blank config) contains no `/v1`; fails before the fix (P1-T4), passes after (P3-T2).
- Regression (unchanged): `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — no edits; must continue to pass unchanged (P3-T3/P3-T4).
- Coverage evidence (PowerShell): baseline `FEATURE/evidence/baseline/poshqc-test.<ts>.md` (P0-T9); post-change `FEATURE/evidence/qa-gates/final-poshqc-test.<ts>.md` (P5-T3); comparison `FEATURE/evidence/qa-gates/coverage-comparison-powershell.<ts>.md` (P5-T7).
- Coverage evidence (C#): baseline `FEATURE/evidence/baseline/csharp-test-coverage.<ts>.md` (P0-T12); post-change `FEATURE/evidence/qa-gates/final-csharp-test-coverage.<ts>.md` (P5-T6); comparison `FEATURE/evidence/qa-gates/coverage-comparison-csharp.<ts>.md` (P5-T8).
- Manual/integration (not automated this pass): `FEATURE/evidence/other/manual-verification-note.<ts>.md` (P4-T1) — full `Install.ps1` end-to-end run through the Docker stage with an operator `.env` at corrected defaults.

## Open Questions / Notes

- No `user-story.md` exists in this feature folder; per full-bug mode this is expected and is not a blocker. `spec.md` is the sole AC source.
- `tests/scripts/Install.Tests.ps1` (505 lines, already over the 500-line cap) is explicitly excluded as a target for the new PowerShell regression test; the new test is added to `tests/scripts/Install.Preflight.Tests.ps1` (398 lines) instead, which already imports `scripts/Install.Preflight.psm1` and has headroom under the cap.
- `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` (616 lines, pre-existing cap violation) is never modified; the new C# regression test is added via a new sibling file (`CoreHostAdapterBaseUrlFallbackTests.cs`), consistent with prior repository precedent for this same file (`#128`, `#130`).
- `.env:1` cannot be independently re-verified via `Read`/`Grep`/`Glob` in a planning/research session (deny-gated); its content is taken from author-documented, reproduction-verified text in `issue.md`/`spec.md` and is not a plan deliverable regardless (gitignored, untracked).
