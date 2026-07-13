# Phase 2 — PowerShell Toolchain Loop (Install.ps1 Stage 9 guard)

Timestamp: 2026-07-12T10-20

Scope: `scripts/Install.ps1`, `tests/scripts/Install.DockerStage.Tests.ps1`

## Step 1 — Format

Command: `mcp__drm-copilot__run_poshqc_format` (scan_folders: the two Phase 2 files)

EXIT_CODE: 0

Output Summary: `ok:true`. Ran twice consecutively; MD5 hashes identical before/after the second run (`6ec19c24c54a4b0e04b0da9aaec38f1b` / `565254d8689eab7064cb08dfb01ae3cd`), confirming 0 files changed (idempotent clean pass). One fix was applied mid-implementation before this clean pass: the initial `throw` string in `Get-ComposeServiceImageTag` used `"...$ImageRepository:<tag>..."`, which PowerShell's parser rejected (`$ImageRepository:` parses as a drive/scope-qualified variable reference) — corrected to `"...${ImageRepository}:<tag>..."`.

## Step 2 — Analyze

Command: `mcp__drm-copilot__run_poshqc_analyze` (scan_folders: the two Phase 2 files), cross-checked via direct `Invoke-PoshQCAnalyze -Root <repo> -ScanFolders <same two files>`

EXIT_CODE: 0

Output Summary: MCP tool returned `ok:true`. Direct invocation printed `PSScriptAnalyzer passed: no findings under <repo>` — 0 errors, 0 warnings.

## Step 3 — Test

Command: `Invoke-Pester -Configuration <config with Run.Path = 'tests/scripts/Install.DockerStage.Tests.ps1'>`

EXIT_CODE: 0

Output Summary: **Tests Passed: 12, Failed: 0, Skipped: 0** — all pre-existing `Install.DockerStage.Tests.ps1` tests (image load stage x3, `-SkipDocker` path x4) plus all 5 new `Context 'image version alignment guard'` tests (P2-T1) pass on this clean pass, confirming P2-T7 (post-guard verification). No format/analyze/test step changed files or failed within this Phase-2 scope, so no restart from format was required.

**Note carried forward to Phase 3 (P3-T4):** this Phase-2-scoped run intentionally covers only the two files named in the plan's explicit Phase 2 scope. A broader ad hoc verification run against `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Force.Tests.ps1` (both named in AC14's regression scope, but NOT in this plan's 2-test-file scope) showed new failures caused by the Stage 9 guard, because those files' own `$script:GetContentMock` fixtures have no `*docker-compose.yml` branch and therefore do not supply matching image tags for the new `Assert-ComposeImageVersionAligned` call. This is documented in full at `FEATURE/evidence/regression-testing/ac14-full-regression.<ts>.md` (P3-T4) rather than remediated here, since fixing those mocks is outside this plan's declared 2-test-file scope.
