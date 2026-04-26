# Plan — Adapt `.github/workflows/ci.yml` to OpenClaw .NET + PowerShell stack (Issue #47)

- Feature folder: `docs/features/active/2026-04-23-adapt-ci-workflow-47/`
- Issue: #47 (`https://github.com/drmoisan/open-claw-bridge/issues/47`)
- Work Mode: minor-audit
- Branch: `chore/adapt-ci-workflow-47` (off `development`, merge-base `83459c201e0676c000b486290ea3435cf88e6a42`)
- Canonical plan path (update in place across preflight revisions): `docs/features/active/2026-04-23-adapt-ci-workflow-47/plan.2026-04-23T12-40.md`
- Requirements source: `docs/features/active/2026-04-23-adapt-ci-workflow-47/issue.md` (sole source; explicit `## Acceptance Criteria` section AC-1 through AC-7)
- Timestamp token for evidence filenames (this cycle): `2026-04-23T12-40`
- All evidence paths are relative to the feature folder unless noted

## Planning Decision Flags (require executor acknowledgement before Phase 2)

These are unresolved items observed at plan time. Each has a concrete fallback so the plan is executable without further planner input, but they should be recorded in Phase 0 evidence.

- **DF-1 — PoshQC module absence.** The issue and the copied-in workflow reference `scripts/powershell/PoshQC/PoshQC.psm1` and the functions `Install-PoshQCTools`, `Invoke-PoshQCFormat`, `Invoke-PoshQCAnalyze`, `Invoke-PoshQCTest`. A directory listing at plan time shows only `scripts/powershell/modules/OpenClawContainerValidation/` — no `PoshQC` folder and no `PoshQC.psm1`. The actionlint wrapper `scripts/dev-tools/run-actionlint.ps1` exists.
  - Fallback for this plan: Phase 0 Task P0-T7 confirms presence/absence on the active branch at execution time. If the module is present and exports all four functions, the `poshqc` job in Phase 2 calls them verbatim. If any function is missing or the module is absent, Phase 2 writes the `poshqc` job using `if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''` guarding every step so the job is a no-op on branches without the module, and records the gap in `evidence/other/poshqc-module-status.2026-04-23T12-40.md`. This preserves AC-4 literal compliance (the job imports the module path named in the issue and calls the named functions) while not breaking CI when the module is not yet landed.
- **DF-2 — `/warnaserror` build flag.** `.claude/rules/csharp.md` requires `TreatWarningsAsErrors=true`. No csproj in `src/` currently sets `TreatWarningsAsErrors`. Issue AC-3 lists `/warnaserror` in the command.
  - Fallback for this plan: the Phase 2 `dotnet-build-test` job uses `dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror` as mandated by AC-3 and the C# policy. Phase 0 Task P0-T8 runs the same command locally to confirm the repo already builds clean under `/warnaserror`. If the baseline build fails under `/warnaserror`, the executor records it in the baseline artifact and the Phase 2 task text explicitly keeps `/warnaserror` on (do not drop the flag to make CI green) and the executor stops for manual triage; fixing build warnings is out of scope per issue Out of Scope.
- **DF-3 — actionlint binary on host.** `run-actionlint.ps1` requires `actionlint` on PATH. Plan assumes local `actionlint` is installed for Phase 0 / Phase 3. If absent, Phase 0 Task P0-T6 records the gap and Phase 3 falls back to GitHub-Actions-side validation only (documented in evidence).

---

## Phase 0 — Preflight and Baseline Capture

Goal: confirm branch, tools, current on-disk state of the files this plan modifies, and read repository policies in the canonical order.

- [x] **[P0-T1]** Read `CLAUDE.md` at the repo root (if present) and `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/tonality.md`.
  - Command: `pwsh -NoProfile -Command "Get-Content -LiteralPath CLAUDE.md,.claude/rules/general-code-change.md,.claude/rules/general-unit-test.md,.claude/rules/tonality.md -ErrorAction SilentlyContinue | Out-Null; Write-Output OK"`
  - Done when: evidence artifact `evidence/baseline/phase0-instructions-read.2026-04-23T12-40.md` exists with `Timestamp:`, `Policy Order:`, and the explicit list of policy files read (baseline + language-specific `.claude/rules/powershell.md`, `.claude/rules/csharp.md` because this plan modifies `.github/workflows/ci.yml` which exercises both stacks).
  - AC coverage: preflight only (not an AC task).

- [x] **[P0-T2]** Confirm active branch and base SHA.
  - Command: `git rev-parse --abbrev-ref HEAD && git merge-base HEAD origin/development`
  - Done when: `evidence/baseline/branch-state.2026-04-23T12-40.md` contains `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` showing branch `chore/adapt-ci-workflow-47` and merge-base SHA `83459c201e0676c000b486290ea3435cf88e6a42` (or a documented divergence).

- [x] **[P0-T3]** Record current `.gitignore` content.
  - Command: `pwsh -NoProfile -Command "Get-Content -LiteralPath .gitignore | Set-Content -LiteralPath docs/features/active/2026-04-23-adapt-ci-workflow-47/evidence/baseline/gitignore-before.2026-04-23T12-40.txt -Encoding utf8"`
  - Done when: `evidence/baseline/gitignore-before.2026-04-23T12-40.txt` exists and a companion `evidence/baseline/gitignore-state.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` quoting the exact `.github/` line (expected: single line `.github/`).

- [x] **[P0-T4]** Record current `.github/workflows/ci.yml` content (the pre-adaptation copy).
  - Command: `pwsh -NoProfile -Command "Get-Content -LiteralPath .github/workflows/ci.yml | Set-Content -LiteralPath docs/features/active/2026-04-23-adapt-ci-workflow-47/evidence/baseline/ci-yml-before.2026-04-23T12-40.yml -Encoding utf8"`
  - Done when: `evidence/baseline/ci-yml-before.2026-04-23T12-40.yml` exists; companion `evidence/baseline/ci-yml-state.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with line count and a list of job names found (expected jobs: `quality-checks7`, `security-scan`, `docs-validation`, `build-check`, `poshqc`, `shell-coverage`, `drm-copilot-extension-tests`).

- [x] **[P0-T5]** Confirm `.github/workflows/publish.yml` byte hash so it can be verified unchanged at end of work.
  - Command: `pwsh -NoProfile -Command "(Get-FileHash -LiteralPath .github/workflows/publish.yml -Algorithm SHA256).Hash"`
  - Done when: `evidence/baseline/publish-yml-hash.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` containing the SHA-256 hash value.

- [x] **[P0-T6]** Verify `actionlint` availability on PATH.
  - Command: `pwsh -NoProfile -Command "$c = Get-Command actionlint -ErrorAction SilentlyContinue; if ($null -eq $c) { 'MISSING' } else { & actionlint -version }"`
  - Done when: `evidence/baseline/actionlint-availability.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE:` (0 if present, non-zero otherwise), and `Output Summary:` stating either the actionlint version string or `MISSING` (feeds DF-3 fallback).

- [x] **[P0-T7]** Confirm presence/absence of PoshQC module and its exported functions (DF-1).
  - Command: `pwsh -NoProfile -Command "$p = 'scripts/powershell/PoshQC/PoshQC.psm1'; if (Test-Path -LiteralPath $p) { Import-Module -Force -Name (Resolve-Path $p); Get-Command -Module PoshQC | Select-Object -ExpandProperty Name } else { 'MODULE_ABSENT' }"`
  - Done when: `evidence/other/poshqc-module-status.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with either (a) `MODULE_ABSENT` or (b) the list of exported function names including at minimum `Install-PoshQCTools`, `Invoke-PoshQCFormat`, `Invoke-PoshQCAnalyze`, `Invoke-PoshQCTest`. This artifact dictates the `poshqc` job shape in Phase 2.

- [x] **[P0-T8]** Capture .NET toolchain baseline: `dotnet` version and a clean restore/build/test of the solution with `/warnaserror` (DF-2) and coverage, as a precondition for the Phase 2 job template.
  - Commands (record each exit code separately in the artifact):
    1. `dotnet --info`
    2. `dotnet restore OpenClaw.MailBridge.sln`
    3. `dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror`
    4. `dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults`
  - Done when: `evidence/baseline/dotnet-baseline.2026-04-23T12-40.md` records for each command `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` that includes at minimum: SDK `10.0.201` line, build success/failure, test counts (passed/failed/skipped), and the numeric overall line-coverage percentage (parse `TestResults/*/coverage.cobertura.xml` `line-rate` attribute). Coverage values are mandatory per the Coverage Evidence Contract; record `UNAVAILABLE_REASON:` only if the coverage file is not produced and explain why.

- [x] **[P0-T9]** Capture PowerShell toolchain baseline (even though this plan changes only a YAML file, the CI file orchestrates PowerShell jobs and `.claude/rules/powershell.md` is in scope).
  - Command: `pwsh -NoProfile -Command "$PSVersionTable.PSVersion.ToString()"`
  - Done when: `evidence/baseline/pwsh-version.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with the PowerShell version string (expected 7.x).

- [x] **[P0-T10]** Confirm the pre-fix `.gitignore` is actually hiding `ci.yml`.
  - Command: `git check-ignore -v .github/workflows/ci.yml; git check-ignore -v .github/workflows/publish.yml; git ls-files .github/workflows/`
  - Done when: `evidence/baseline/gitignore-before-check.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE:` (expected non-zero exit from the first two `check-ignore` commands since they print a match), `Output Summary:` stating (a) `ci.yml` matches `.gitignore:76:.github/`, (b) `publish.yml` is tracked via `git ls-files` despite the ignore line (force-added history), and (c) the current `git ls-files .github/workflows/` listing.

---

## Phase 1 — Fix `.gitignore` negation pattern

Goal: rewrite the `.github/` block so `.github/workflows/ci.yml` and `.github/workflows/publish.yml` are trackable and every other descendant of `.github/` remains ignored.

- [x] **[P1-T1]** Edit `.gitignore`: replace the single line `.github/` (line 76) with the multi-line negation block below. No other line in `.gitignore` changes.
  - File: `.gitignore`
  - Old (single line to replace): `.github/`
  - New (exact five lines, preserving comment header placement):
    ```
    .github/*
    !.github/workflows/
    .github/workflows/*
    !.github/workflows/ci.yml
    !.github/workflows/publish.yml
    ```
  - Done when: `git diff -- .gitignore` shows only this single-line-to-five-lines hunk and the pre/post diff is attached verbatim in `evidence/other/gitignore-edit.2026-04-23T12-40.md` with `Timestamp:`, `Command: git diff -- .gitignore`, `EXIT_CODE: 0`, `Output Summary:` of hunk size.
  - AC coverage: **AC-1**, **AC-7**.

- [x] **[P1-T2]** Verify `ci.yml` is no longer ignored.
  - Command: `git check-ignore -v .github/workflows/ci.yml`
  - Done when: `evidence/regression-testing/gitignore-check-ci.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 1` (unmatched), `Output Summary: no match (empty stdout)`.
  - AC coverage: **AC-1**.

- [x] **[P1-T3]** Verify `publish.yml` is still trackable (was force-added; the new pattern must keep it reachable).
  - Command: `git check-ignore -v .github/workflows/publish.yml`
  - Done when: `evidence/regression-testing/gitignore-check-publish.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 1`, `Output Summary: no match (empty stdout)`.
  - AC coverage: **AC-1** (preserves existing tracked state).

- [x] **[P1-T4]** Verify other `.github/` descendants remain ignored.
  - Command: `git check-ignore -v .github/copilot-instructions.md`
  - Done when: `evidence/regression-testing/gitignore-check-other.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` quoting the matching line from `.gitignore` (expected to match the new `.github/*` line). If the file does not exist, run the same command against `.github/instructions/` (or any directory currently present under `.github/`) and record the substitution in `Output Summary:`.
  - AC coverage: **AC-1** (scope preservation).

- [x] **[P1-T5]** Stage `ci.yml` and confirm git now tracks it.
  - Command: `git add .gitignore .github/workflows/ci.yml && git ls-files .github/workflows/ci.yml`
  - Done when: `evidence/regression-testing/ci-yml-tracked.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` containing the literal path `.github/workflows/ci.yml` on stdout. This is the AC-7 proof artifact.
  - AC coverage: **AC-7**.

---

## Phase 2 — Rewrite `.github/workflows/ci.yml`

Goal: replace the copied-in Python/shell template with a .NET + PowerShell + actionlint workflow. Total file size MUST remain under 300 lines.

- [x] **[P2-T1]** Replace the entire contents of `.github/workflows/ci.yml` with the adapted workflow. Overwrite, do not merge.
  - File: `.github/workflows/ci.yml`
  - Required contents (exact structure; values may be line-broken for readability but keys and values as listed):
    ```yaml
    name: CI

    on:
      push:
        branches: [main, development]
      pull_request:
        branches: [main, development]
      workflow_dispatch:

    jobs:
      dotnet-build-test:
        name: .NET Build + Test
        runs-on: windows-latest
        steps:
          - name: Check out repository
            uses: actions/checkout@v4

          - name: Setup .NET SDK
            uses: actions/setup-dotnet@v4
            with:
              dotnet-version: 10.0.x

          - name: Restore
            run: dotnet restore OpenClaw.MailBridge.sln

          - name: Build (Release, warnings-as-errors)
            run: dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror

          - name: Test with coverage
            run: dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults

          - name: Upload test results
            if: always()
            uses: actions/upload-artifact@v4
            with:
              name: dotnet-test-results
              path: TestResults/**
              if-no-files-found: warn

      poshqc:
        name: PowerShell QC
        runs-on: windows-latest
        steps:
          - name: Check out repository
            uses: actions/checkout@v4

          - name: Install PoshQC tooling
            if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''
            shell: pwsh
            run: |
              Import-Module "${{ github.workspace }}/scripts/powershell/PoshQC/PoshQC.psm1"
              Install-PoshQCTools

          - name: Format PowerShell
            if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''
            shell: pwsh
            run: |
              Import-Module "${{ github.workspace }}/scripts/powershell/PoshQC/PoshQC.psm1"
              Invoke-PoshQCFormat -Root "${{ github.workspace }}"
              $diff = git status --porcelain
              if ($diff) {
                Write-Error "PowerShell files were reformatted. Run Invoke-PoshQCFormat locally and commit the changes."
              }

          - name: Analyze PowerShell
            if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''
            shell: pwsh
            run: |
              Import-Module "${{ github.workspace }}/scripts/powershell/PoshQC/PoshQC.psm1"
              Invoke-PoshQCAnalyze -Root "${{ github.workspace }}"

          - name: Test PowerShell
            if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''
            shell: pwsh
            run: |
              Import-Module "${{ github.workspace }}/scripts/powershell/PoshQC/PoshQC.psm1"
              Invoke-PoshQCTest -Root "${{ github.workspace }}"

          - name: Upload PowerShell test artifacts
            if: always()
            uses: actions/upload-artifact@v4
            with:
              name: poshqc-test-results
              path: artifacts/pester/*
              if-no-files-found: ignore

      actionlint:
        name: Workflow Lint
        runs-on: ubuntu-latest
        steps:
          - name: Check out repository
            uses: actions/checkout@v4

          - name: Install actionlint
            run: |
              bash <(curl -s https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash)
              echo "$PWD" >> "$GITHUB_PATH"

          - name: Run actionlint
            run: actionlint -color
    ```
  - Conditional modification (based on P0-T7 result): if `evidence/other/poshqc-module-status.2026-04-23T12-40.md` `Output Summary:` contains all four function names (`Install-PoshQCTools`, `Invoke-PoshQCFormat`, `Invoke-PoshQCAnalyze`, `Invoke-PoshQCTest`), remove every `if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''` line. Otherwise leave them in place. Record the chosen variant in `evidence/other/poshqc-job-variant.2026-04-23T12-40.md`.
  - Explicit non-inclusion (must NOT appear anywhere in the file): `poetry`, `python`, `setup-python`, `black`, `ruff`, `pyright`, `pytest`, `safety`, `bats`, `shellcheck`, `shfmt`, `kcov`, `codecov`, `drm-copilot`, `atomic-executor`, `shell-qc`, `lexile_corpus_tuner`, `docs-validation`, `build-check`, `security-scan`, `quality-checks7`, `shell-coverage`, `drm-copilot-extension-tests`, `setup-node`, `npm`.
  - Done when: `.github/workflows/ci.yml` contains exactly the jobs `dotnet-build-test`, `poshqc`, `actionlint` and nothing else; total line count is strictly less than 300; and `evidence/other/ci-yml-rewrite.2026-04-23T12-40.md` records `Timestamp:`, `Command: Measure-Object -Line`, `EXIT_CODE: 0`, and `Output Summary:` with the final line count.
  - AC coverage: **AC-2**, **AC-3**, **AC-4**, **AC-6**.

- [x] **[P2-T2]** Verify the .NET job text matches AC-3 literally.
  - Command: `pwsh -NoProfile -Command "Select-String -LiteralPath .github/workflows/ci.yml -Pattern 'actions/setup-dotnet@v4','dotnet-version: 10.0.x','runs-on: windows-latest','dotnet restore OpenClaw.MailBridge.sln','dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror','dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:\"XPlat Code Coverage\"'"`
  - Done when: `evidence/regression-testing/verify-ci-dotnet-job.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` listing one match per pattern (six total). Filename is fixed by AC-3.
  - AC coverage: **AC-3**.

- [x] **[P2-T3]** Verify the PoshQC job text matches AC-4 literally. (Amended: AC-4 evidence written to `evidence/regression-testing/verify-ci-powershell-job.2026-04-23T12-40.md` per the amended AC-4 in `issue.md`; old PoshQC wrapper patterns are verified ABSENT.)
  - Command: `pwsh -NoProfile -Command "Select-String -LiteralPath .github/workflows/ci.yml -Pattern 'scripts/powershell/PoshQC/PoshQC.psm1','Invoke-PoshQCFormat','Invoke-PoshQCAnalyze','Invoke-PoshQCTest','artifacts/pester'"`
  - Done when: `evidence/regression-testing/verify-ci-poshqc-job.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` listing at least one match per pattern (five patterns). Filename is fixed by AC-4.
  - AC coverage: **AC-4**.

- [x] **[P2-T4]** Verify triggers match AC-6 literally.
  - Command: `pwsh -NoProfile -Command "Get-Content -LiteralPath .github/workflows/ci.yml -TotalCount 20 | Out-String"`
  - Done when: `evidence/regression-testing/verify-ci-triggers.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` showing the first 20 lines of the file, which MUST include `push:` with `branches: [main, development]`, `pull_request:` with `branches: [main, development]`, and `workflow_dispatch:`. Filename is fixed by AC-6.
  - AC coverage: **AC-6**.

- [x] **[P2-T5]** Confirm removal of every excluded token.
  - Command: `pwsh -NoProfile -Command "$bad = 'poetry','setup-python','black','ruff','pyright','pytest','safety','bats','shellcheck','shfmt','kcov','codecov','drm-copilot','atomic-executor','shell-qc','lexile_corpus_tuner','docs-validation','build-check','security-scan','quality-checks7','shell-coverage','drm-copilot-extension-tests','setup-node'; $hits = Select-String -LiteralPath .github/workflows/ci.yml -Pattern $bad -SimpleMatch -CaseSensitive:$false; if ($hits) { $hits | ForEach-Object { $_.Line } } else { 'NONE' }"`
  - Done when: `evidence/regression-testing/verify-ci-removals.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary: NONE`.
  - AC coverage: **AC-2**.

- [x] **[P2-T6]** Confirm `.github/workflows/publish.yml` is byte-identical to the Phase 0 baseline.
  - Command: `pwsh -NoProfile -Command "(Get-FileHash -LiteralPath .github/workflows/publish.yml -Algorithm SHA256).Hash"`
  - Done when: `evidence/regression-testing/publish-yml-unchanged.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` containing the SHA-256 hash and an explicit line `MATCHES_BASELINE: true` (comparing against `evidence/baseline/publish-yml-hash.2026-04-23T12-40.md`). If the hash differs, stop and do not proceed to Phase 3.
  - AC coverage: invariant (issue Out of Scope: do not modify `publish.yml`).

---

## Phase 3 — Validate rewritten workflow with actionlint

Goal: prove AC-5 with a clean `actionlint` run against `.github/workflows/`.

- [x] **[P3-T1]** Run actionlint against the workflows directory.
  - Command: `pwsh -NoProfile -File scripts/dev-tools/run-actionlint.ps1 2>&1`
  - Done when: `evidence/regression-testing/actionlint.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with either (a) empty actionlint output (clean pass) or (b) the full actionlint stdout/stderr. If exit code is non-zero, iterate Phase 2 (return to P2-T1) and rerun this task. Filename is fixed by AC-5.
  - AC coverage: **AC-5**.

- [x] **[P3-T2]** Re-confirm `.github/workflows/ci.yml` is staged in the index (AC-7 end-state proof after all edits).
  - Command: `git ls-files .github/workflows/ci.yml && git diff --cached --name-only -- .github/workflows/ci.yml`
  - Done when: `evidence/regression-testing/verify-ci-tracked.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` showing the path present on both outputs. Filename is fixed by AC-7.
  - AC coverage: **AC-7**.

---

## Phase 4 — Final QA Loop and Evidence Rollup

Goal: run language-appropriate end-state QA, capture coverage evidence, and roll up AC traceability. No new code is introduced by this plan, so the final QA loop focuses on the languages whose rules are in scope: C# (because the CI file compiles/tests the solution) and PowerShell (because the CI file orchestrates PoshQC). These end-state runs validate that the baseline still holds after the edits; if they regress it means an unrelated change leaked in.

- [x] **[P4-T1]** C# final QA — restore.
  - Command: `dotnet restore OpenClaw.MailBridge.sln`
  - Done when: `evidence/qa-gates/dotnet-restore.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (restore succeeded). If non-zero, stop and fix before continuing.

- [x] **[P4-T2]** C# final QA — build with warnings-as-errors.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror`
  - Done when: `evidence/qa-gates/dotnet-build.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (build succeeded; list warning count = 0 and error count = 0).

- [x] **[P4-T3]** C# final QA — test with coverage.
  - Command: `dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults`
  - Done when: `evidence/qa-gates/dotnet-test.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with passed/failed/skipped counts AND numeric overall line-coverage percent parsed from `TestResults/*/coverage.cobertura.xml` `line-rate`. This is a coverage-bearing task per the Coverage Evidence Contract.

- [x] **[P4-T4]** C# coverage delta check.
  - Command: (manual compare) read baseline coverage from `evidence/baseline/dotnet-baseline.2026-04-23T12-40.md` and post-change coverage from `evidence/qa-gates/dotnet-test.2026-04-23T12-40.md`.
  - Done when: `evidence/qa-gates/dotnet-coverage-delta.2026-04-23T12-40.md` records `Timestamp:`, the two numeric percent values, a `Delta:` line, and an explicit statement `NoRegression: true|false` against the repository-wide >= 80% floor and the 0% new-code floor (this plan introduces no new C# code, so new-code coverage is N/A and must be recorded as `NewCodeCoverage: N/A (no C# code added)`). If `NoRegression: false`, stop and escalate per tonality policy.

- [x] **[P4-T5]** PowerShell final QA — gate based on P0-T7 result.
  - If P0-T7 recorded `MODULE_ABSENT`: skip-branch authorized by plan. Command: `pwsh -NoProfile -Command "'SKIPPED: PoshQC module not present on branch (DF-1 fallback)'"`. Done when: `evidence/qa-gates/poshqc-final.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary: SKIPPED per plan DF-1 fallback; CI job guards via hashFiles`.
  - If P0-T7 recorded the module present with all four exports: Command: `pwsh -NoProfile -Command "Import-Module scripts/powershell/PoshQC/PoshQC.psm1; Invoke-PoshQCFormat -Root (Get-Location); Invoke-PoshQCAnalyze -Root (Get-Location); Invoke-PoshQCTest -Root (Get-Location)"`. Done when: `evidence/qa-gates/poshqc-final.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` with format/analyze/test pass counts AND PowerShell line-coverage percent if produced (coverage-bearing; record `Coverage: <percent>` or `Coverage: UNAVAILABLE_REASON: <reason>`).
  - This task has an explicit authorized skip branch per the No-SKIPPED Rule.

- [x] **[P4-T6]** Re-run actionlint as the final lint pass on the workflow file.
  - Command: `pwsh -NoProfile -File scripts/dev-tools/run-actionlint.ps1 2>&1`
  - Done when: `evidence/qa-gates/actionlint-final.2026-04-23T12-40.md` records `Timestamp:`, `Command:`, `EXIT_CODE: 0`, `Output Summary:` (empty actionlint output or clean). If non-zero or files change, restart from Phase 2 P2-T1.

- [x] **[P4-T7]** AC traceability rollup.
  - Action: write `evidence/qa-gates/ac-traceability.2026-04-23T12-40.md` with one section per AC (AC-1 through AC-7). Each section MUST cite the exact evidence filename from earlier phases:
    - AC-1 → `evidence/regression-testing/gitignore-check-ci.2026-04-23T12-40.md`, `evidence/regression-testing/gitignore-check-publish.2026-04-23T12-40.md`, `evidence/regression-testing/gitignore-check-other.2026-04-23T12-40.md`
    - AC-2 → `evidence/regression-testing/verify-ci-removals.2026-04-23T12-40.md`, diff against `evidence/baseline/ci-yml-before.2026-04-23T12-40.yml`
    - AC-3 → `evidence/regression-testing/verify-ci-dotnet-job.2026-04-23T12-40.md`
    - AC-4 → `evidence/regression-testing/verify-ci-poshqc-job.2026-04-23T12-40.md`
    - AC-5 → `evidence/regression-testing/actionlint.2026-04-23T12-40.md`
    - AC-6 → `evidence/regression-testing/verify-ci-triggers.2026-04-23T12-40.md`
    - AC-7 → `evidence/regression-testing/ci-yml-tracked.2026-04-23T12-40.md`, `evidence/regression-testing/verify-ci-tracked.2026-04-23T12-40.md`
  - Done when: the rollup file exists, references every AC by number, and lists `PASS` / `FAIL` per AC backed by the cited artifact. If any AC is `FAIL`, the plan outcome is `remediation-required` and MUST NOT be reported as PASS.

- [x] **[P4-T8]** Issue update mirror (GitHub issue #47).
  - Action: write `evidence/issue-updates/issue-47.2026-04-23T12-40.md` containing `Timestamp:`, the exact intended comment/body text summarizing the AC status from P4-T7, and `PostedAs: body` or `PostedAs: comment` (with the issue/comment URL once posted) or `PostedAs: unknown` with a `POSTING BLOCKED` header if a posting credential is unavailable.
  - Done when: the mirror file exists with one of the three terminal statuses recorded.

---

## AC Coverage Matrix (Cross-Reference)

| AC   | Primary task(s)                         | End-state evidence filename                                                 |
|------|-----------------------------------------|-----------------------------------------------------------------------------|
| AC-1 | P1-T1, P1-T2, P1-T3, P1-T4              | `evidence/regression-testing/gitignore-check-ci.2026-04-23T12-40.md`        |
| AC-2 | P2-T1, P2-T5                            | `evidence/regression-testing/verify-ci-removals.2026-04-23T12-40.md`        |
| AC-3 | P2-T1, P2-T2                            | `evidence/regression-testing/verify-ci-dotnet-job.2026-04-23T12-40.md`      |
| AC-4 | P2-T1, P2-T3                            | `evidence/regression-testing/verify-ci-poshqc-job.2026-04-23T12-40.md`      |
| AC-5 | P3-T1                                   | `evidence/regression-testing/actionlint.2026-04-23T12-40.md`                |
| AC-6 | P2-T1, P2-T4                            | `evidence/regression-testing/verify-ci-triggers.2026-04-23T12-40.md`        |
| AC-7 | P1-T5, P3-T2                            | `evidence/regression-testing/verify-ci-tracked.2026-04-23T12-40.md`         |

---

## Preflight Validation

`DIRECTIVE: PREFLIGHT VALIDATION ONLY`

Expected validator outcomes:
- `validate_orchestration_artifacts` with `artifact_type: "plan"` and `artifact_path: docs/features/active/2026-04-23-adapt-ci-workflow-47/plan.2026-04-23T12-40.md` must exit 0.
- Preflight signal from the atomic-executor must be either `PREFLIGHT: ALL CLEAR` or `PREFLIGHT: REVISIONS REQUIRED`. On revisions, update this same file in place and rerun preflight.

---

## Invariants and Scope Guards

- `.github/workflows/publish.yml` must remain byte-identical (verified P0-T5 vs P2-T6).
- No production C# or PowerShell source under `src/` or `scripts/powershell/modules/` is modified by this plan.
- No new dependencies, NuGet packages, or npm packages are added.
- Final `ci.yml` stays strictly under 300 lines (verified P2-T1 done condition).
- No third-party integrations (Codecov, Snyk, etc.) are introduced.
- Tone of every evidence artifact follows `.claude/rules/tonality.md`: factual, neutral, no hyperbole, no humor.
