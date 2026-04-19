# Plan Refinement — Bundle Install Script (Issue #36)

- **Feature folder:** `docs/features/active/2026-04-18-bundle-install-script-36/`
- **Feature branch:** `feature/bundle-install-script-36`
- **Base commit:** `7bd92a8` -> `453343e` (original install feature, 127 tasks completed) -> HEAD
- **Work Mode:** `full-feature` (carryover from the original plan)
- **Refinement Plan Timestamp:** 2026-04-19T00-00
- **Predecessor plan (preserved, do not delete):** `plan.2026-04-18T00-00.md`
- **Acceptance-criteria sources (authoritative):**
  - `docs/features/active/2026-04-18-bundle-install-script-36/spec.md` (Behavior, Definition of Done, Seeded Test Conditions)
  - `docs/features/active/2026-04-18-bundle-install-script-36/user-story.md` (Acceptance Criteria)
- **Supporting inputs:**
  - `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`
  - `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Uninstall.ps1`
  - `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`
  - `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`
  - `artifacts/research/2026-04-18-bundle-install-script.md`
  - `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`, `.claude/rules/tonality.md`

---

## Refinement Context

### Why this refinement is needed

The original plan landed `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1` in the repository's `scripts/` directory. `Install.ps1` auto-detects `artifacts/publish/<version>/` via `Find-NewestPublishVersion` and exposes a `-Version` parameter for explicit selection.

The feature owner has clarified the intended deployment topology:

- The install scripts MUST ship INSIDE every bundle produced by `Publish.ps1`, at the bundle root (same level as `executables/`, `docker/`, `msix/`).
- `Install.ps1` MUST run FROM the bundle directory via `$PSScriptRoot` and contain no decision logic for locating the bundle. The bundle root is the directory the script lives in.
- The manifest schema gains a top-level `version` field; the install scripts themselves are listed in `files`.
- Back-compat is explicitly not required: no consumer has yet depended on the prior flat-array `manifest.json` schema.

This refinement closes the loop by (a) adding an install-script staging stage to `Publish.ps1`, (b) changing the manifest schema to `{ version, files }`, (c) retiring `Find-NewestPublishVersion` and the `-Version` parameter, and (d) self-locating the install via `$PSScriptRoot`.

### Locked decisions (no open questions)

1. `Publish.ps1` + `Publish.Helpers.psm1` copy `scripts/Install.ps1`, `scripts/Uninstall.ps1`, and `scripts/Install.Helpers.psm1` into the bundle root during publish.
2. `manifest.json` schema becomes `{ "version": "<4-part>", "files": [ { "path", "size", "sha256" } ] }`. Install scripts appear in `files`.
3. `Install.ps1`:
   - `-SourcePath` default is `$PSScriptRoot` (dev/test override retained).
   - `-Version` parameter removed.
   - `Find-NewestPublishVersion` usage removed; `artifacts/publish/` auto-detect removed.
   - Version read from `manifest.json` `version` field via `Get-ManifestVersion`.
4. `Uninstall.ps1` behavior unchanged; ships in the bundle and is additionally present at `%LOCALAPPDATA%\OpenClaw\<version>\` after install (by virtue of bundle copy).
5. `Install.Helpers.psm1`:
   - `Find-NewestPublishVersion` retired entirely.
   - `Test-ManifestIntegrity` updated to parse the new schema (top-level `version` + `files`).
   - New `Get-ManifestVersion` helper returns the `version` field; throws on missing/unparseable.
6. Documentation: `README.md` operator text and `docs/mailbridge-runbook.md` Path D are updated to the bundle-root flow (`cd artifacts/publish/<version>/; .\Install.ps1`).
7. Tests: `Find-NewestPublishVersion` tests dropped; `Test-ManifestIntegrity` tests updated for the new schema; `Get-ManifestVersion` tests added; `Install.Tests.ps1` exercises `$PSScriptRoot`-as-default plus `-SourcePath` override against a Pester `TestDrive` bundle; `Publish.Helpers.Tests.ps1` + `Publish.Tests.ps1` cover the install-script-staging helper and the new manifest schema.
8. Back-compat: none. No schema-version detection. No dual-schema support.

### AC / DoD items whose wording or status changes

The following items in `spec.md` and `user-story.md` are impacted by this refinement and must be rewritten in Phase A. Line numbers reference the current committed files read at HEAD `453343e`.

**`spec.md` changes:**

- Line 12 (Overview) — remove "unpacks a selected bundle under" narrative that implies auto-detection; replace with "unpacks the bundle whose directory the script lives in".
- Line 28 (Main path install, step 1) — remove `-Version` from the list of optional parameters and remove "select a non-default bundle" framing for `-SourcePath`; recast as dev/test override whose default is `$PSScriptRoot`.
- Line 29 (Main path install, step 2) — replace the auto-detect paragraph with "The script resolves the bundle root from `$PSScriptRoot`. `-SourcePath` overrides the default value `$PSScriptRoot` for dev/test scenarios. The version is read from `manifest.json` via `Get-ManifestVersion`."
- Line 54 (Negative / edge paths, #1 "Empty publish root") — remove this path entirely; auto-detect no longer exists.
- Line 55 (Negative / edge paths, #2 "`-Version` points to missing bundle") — remove this path entirely.
- Line 73 through 78 (Install.ps1 inputs table) — remove the `-Version` row; update the `-SourcePath` row to reflect the new default `$PSScriptRoot`.
- Line 75 (Install.ps1 inputs, `-Version` row) — delete.
- Line 97–110 (Install record schema) — unchanged in behavior; add a note that `version` matches `Get-ManifestVersion` output.
- Line 127 (API / CLI Surface, script invocations block, line 127) — first example `.\scripts\Install.ps1` replaced by `cd artifacts/publish/<version>; .\Install.ps1`.
- Line 130 (Script invocations, `-SourcePath` example) — retained, but annotate as dev/test override.
- Line 133 (Script invocations, `-Version` example) — delete.
- Line 150–167 (Exported helper module table) — remove the `Find-NewestPublishVersion` row; add a `Get-ManifestVersion` row; revise the `Test-ManifestIntegrity` row to reference the new schema.
- Line 154 (helper table, `Find-NewestPublishVersion` row) — delete.
- Line 176 (Data & State, step 1) — replace "The script selects the bundle root and verifies manifest integrity" with "The script reads the bundle root from `$PSScriptRoot` and verifies manifest integrity (read-only)".
- Line 215–224 (Implementation Strategy, new files) — unchanged paths; add a cross-reference that `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1` are ALSO copied into every bundle by `Publish.ps1`.
- Line 247–263 (Test seams table) — remove the `Find-NewestPublishVersion` row; add a `Get-ManifestVersion` row.
- Line 273 (DoD, 1st bullet) — update `-SourcePath`, `-AllowUnsigned`, `-SkipDocker`, `-Force` list to remove `-Version`.
- Line 279 (DoD, "Manifest-integrity failure aborts before any destination folder is created") — retained but note the new schema.
- Line 313–325 (Owner Decisions (resolved)) — update "Newest-bundle detection" entry: strike through auto-detect-by-version narrative; replace with "bundle root = `$PSScriptRoot`; `-SourcePath` dev override".
- Line 316 (Owner Decisions, `Newest-bundle detection` bullet) — rewrite.
- Line 327–333 (Open Questions) — Q3 (runbook Path D placement) remains valid; add a note that Q1 (health-poll timeout) and Q2 (admin precheck) are resolved and unchanged by this refinement.
- Seeded Test Conditions (lines 288–298): replace bullet #5 (`artifacts/publish/` empty of parseable version directories fails fast) with "running `.\Install.ps1` from outside a bundle root (no sibling `manifest.json`) fails fast with a clear error".

**`user-story.md` changes:**

- Line 10 (Story Statement, 1st bullet) — replace "unpacks the newest bundle under `artifacts/publish/`" with "unpacks the bundle whose root directory it lives in".
- Line 20 (Problem/Why, step 3) — retained; runbook references updated to Path D flow.
- Line 46–57 (Scenario: Install newest bundle on a clean host) — retitle to "Scenario: Install a bundle on a clean host". Step 1 rewrites to "Operator `cd`s to `artifacts/publish/<version>/` and runs `.\Install.ps1`". Step 2 rewrites to "The script sets `$BundleRoot = $PSScriptRoot`." Step 3 unchanged.
- Line 112 (Acceptance Criteria, AC#2 "`.\scripts\Install.ps1 -SourcePath <path>` or `.\scripts\Install.ps1 -Version <v>` overrides the newest-bundle auto-detection") — rewrite to "Running `.\Install.ps1` from a bundle root installs that bundle; `-SourcePath <path>` overrides the default `$PSScriptRoot` for dev/test. `-Version` is no longer a parameter."
- Non-Goals (line 127–136) — no change required, but confirm the refinement does not alter any non-goal.

### Spec-refresh task (Phase A)

Phase A runs before any implementation so the preflight preamble observes the new AC text. Phase A tasks are atomic edits to `spec.md` and `user-story.md` only; they do not change code.

---

## Phase A — Spec Refresh (must run first)

Update `spec.md` and `user-story.md` so every AC / DoD / behavioral bullet reflects the locked refinement decisions. Do not introduce new open questions. All three refinement-locked decisions (install-script staging in Publish, self-locating Install via `$PSScriptRoot`, new `{ version, files }` manifest schema) must be represented in the refreshed spec.

- [x] [PA-T1] Edit `spec.md` Overview (line 12) to replace auto-detect narrative with "unpacks the bundle whose root directory the script lives in" and record a one-line change note at the bottom of the Overview paragraph stating "Refinement 2026-04-19: install scripts now ship inside every bundle and self-locate via `$PSScriptRoot`."
- [x] [PA-T2] Edit `spec.md` Behavior > Main path (install) step 1 (line 28) to remove `-Version` from the optional parameter list and reframe `-SourcePath` as a dev/test override whose default is `$PSScriptRoot`.
- [x] [PA-T3] Rewrite `spec.md` Behavior > Main path (install) step 2 (line 29) to read: "The script resolves the bundle root from `$PSScriptRoot`. When `-SourcePath` is supplied, that value is used verbatim. The version is read from `<BundleRoot>/manifest.json` via `Get-ManifestVersion`."
- [x] [PA-T4] Delete `spec.md` Behavior > Negative/edge paths bullet #1 "Empty publish root" (line 54) and bullet #2 "`-Version` points to missing bundle" (line 55). Renumber subsequent bullets.
- [x] [PA-T5] Add a new Behavior > Negative/edge paths bullet (after the surviving bullets) that reads: "`-SourcePath` or `$PSScriptRoot` points to a directory without `manifest.json` at its root. The script aborts with the path searched." Ensure this replaces the semantics of the deleted bullets.
- [x] [PA-T6] Update `spec.md` Inputs/Outputs > `scripts/Install.ps1` inputs table (lines 72-78) to remove the `-Version` row and update the `-SourcePath` row default from `''` to `$PSScriptRoot` with description "Absolute or relative path to a specific bundle root. Default is `$PSScriptRoot`; dev/test override.".
- [x] [PA-T7] Edit `spec.md` API / CLI Surface > Script invocations block (lines 125-146) to: (a) replace the first example (no-arg install) with a two-line example `cd artifacts/publish/<version>; .\Install.ps1`; (b) delete the `-Version '1.2.3.0'` example; (c) annotate the `-SourcePath` example as a dev/test override.
- [x] [PA-T8] Update `spec.md` API / CLI Surface > Exported helper module table (lines 150-167) to remove the `Find-NewestPublishVersion` row, add a new `Get-ManifestVersion` row ("Reads `manifest.json` at a bundle root and returns the top-level `version` field. Throws on missing or unparseable."), and revise the `Test-ManifestIntegrity` row description to reference the new `{ version, files }` schema.
- [x] [PA-T9] Edit `spec.md` Data & State > Data flow (install) step 1 (line 176) to read: "The script resolves the bundle root from `$PSScriptRoot` (or `-SourcePath` override), reads the version via `Get-ManifestVersion`, and verifies manifest integrity (read-only)."
- [x] [PA-T10] Add a paragraph to `spec.md` Data & State > Persistence describing the new manifest schema: `{ "version": "<4-part>", "files": [ { "path", "size", "sha256" } ] }`. Note that `Install.ps1`, `Uninstall.ps1`, and `Install.Helpers.psm1` are included in `files` because they are staged into the bundle by `Publish.ps1`.
- [x] [PA-T11] Update `spec.md` Implementation Strategy > Scope — new files table (lines 217-224) to append a one-line note after the table: "`Install.ps1`, `Uninstall.ps1`, and `Install.Helpers.psm1` are additionally copied into every bundle root by `Publish.ps1`, and the install script is executed from the bundle root via `$PSScriptRoot` rather than from the repo's `scripts/` directory."
- [x] [PA-T12] Update `spec.md` Implementation Strategy > Test seams table (lines 249-263) to remove the `Find-NewestPublishVersion` row and add a `Get-ManifestVersion` row with mock target "`Mock Get-Content`, `Mock Test-Path`".
- [x] [PA-T13] Update `spec.md` Definition of Done 1st bullet (line 273) to read: "`scripts/Install.ps1` exists and accepts `-SourcePath`, `-AllowUnsigned`, `-SkipDocker`, `-Force`. `-Version` is NOT a parameter (removed in refinement)." Mark status `[ ]` (unchecked) because the implementation task has not yet run under the refinement.
- [x] [PA-T14] Update `spec.md` Definition of Done by adding three new bullets at the end of the DoD list and marking them `[ ]`: (a) "`Publish.ps1` copies `Install.ps1`, `Uninstall.ps1`, and `Install.Helpers.psm1` into every bundle root."; (b) "`manifest.json` uses the `{ version, files }` schema and includes the install scripts in `files`."; (c) "Running `.\Install.ps1` from a bundle root (with `$PSScriptRoot` = that bundle) installs the bundle without any `-Version` or auto-detect parameter."
- [x] [PA-T15] Update `spec.md` Seeded Test Conditions list (lines 288-298) to: (a) mark bullet #5 ("`artifacts/publish/` empty of parseable version directories fails fast") as `[ ]` (unchecked) and rewrite to "Running `.\Install.ps1` from a directory without `manifest.json` fails fast with a clear error naming the directory searched."; (b) append a new bullet "The bundle produced by `Publish.ps1` contains `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1` at the bundle root and the manifest lists them under `files`." marked `[ ]`.
- [x] [PA-T16] Update `spec.md` Owner Decisions (resolved) "Newest-bundle detection" bullet (line 316) to: "Bundle root = `$PSScriptRoot` (the directory the install script lives in). `-SourcePath` is a dev/test override whose default is `$PSScriptRoot`. Auto-detect of `artifacts/publish/<version>/` is retired."
- [x] [PA-T17] Update `spec.md` Open Questions (lines 327-333) to: (a) add a preamble noting that Q1 (health-poll timeout/interval defaults 90s/3s) and Q2 (administrator precheck for `-AllowUnsigned`) are preserved from the original plan and are not reopened by this refinement; (b) retain Q3 text verbatim.
- [x] [PA-T18] Bump `spec.md` `Version` field (line 8) to `0.2` and `Last Updated` (line 6) to `2026-04-19T00:00:00Z`.
- [x] [PA-T19] Rewrite `user-story.md` Story Statement bullet 1 (line 10) to: "As an operator deploying an OpenClaw release bundle on a Windows host, I want to `cd` into the bundle directory and run `.\Install.ps1` with no arguments, so the install script self-locates the bundle via `$PSScriptRoot` and I never have to point the installer at the right directory."
- [x] [PA-T20] Retitle `user-story.md` "Scenario: Install newest bundle on a clean host" (line 46) to "Scenario: Install a bundle on a clean host" and rewrite steps 1-3: step 1 "Operator `cd`s to `artifacts/publish/1.2.3.0/` (the bundle produced by `Publish.ps1`)"; step 2 "Operator runs `.\Install.ps1` with no arguments"; step 3 "The script sets `$BundleRoot = $PSScriptRoot` and reads the version from `<BundleRoot>/manifest.json` via `Get-ManifestVersion`".
- [x] [PA-T21] Rewrite `user-story.md` Acceptance Criteria bullet 2 (line 112) to: "Running `.\Install.ps1` from a bundle root installs that bundle. `-SourcePath <path>` overrides the default `$PSScriptRoot` for dev/test scenarios. `-Version` is not a parameter." Mark status `[ ]` (unchecked) because the refinement has not yet been implemented.
- [x] [PA-T22] Add two new `user-story.md` Acceptance Criteria bullets (appended at the end of the Acceptance Criteria list) and mark each `[ ]`: (a) "`Publish.ps1` copies `Install.ps1`, `Uninstall.ps1`, and `Install.Helpers.psm1` into every bundle root."; (b) "`manifest.json` uses the `{ version, files }` schema; `Get-ManifestVersion` returns the top-level `version` field."
- [x] [PA-T23] Mark `user-story.md` Acceptance Criteria bullet 1 (line 111) as `[ ]` (unchecked) because the no-argument invocation semantics have changed and will be re-verified under the refinement.
- [x] [PA-T24] Bump `user-story.md` `Last Updated` (line 7) to `2026-04-19T00:00:00Z`.
- [x] [PA-T25] Persist a spec-refresh artifact at `evidence/other/spec-refresh.refinement.2026-04-19T00-00.md` with `Timestamp:`, `Command:` (`git diff --stat -- docs/features/active/2026-04-18-bundle-install-script-36/spec.md docs/features/active/2026-04-18-bundle-install-script-36/user-story.md`), `EXIT_CODE:`, and `Output Summary:` listing each edited file and line-count delta. Acceptance: both files were edited; zero unrelated file edits detected.

---

## Phase B — Baseline Capture (refinement)

Capture the toolchain state at the refinement start (HEAD `453343e`) so Phase G can assert no regression. Artifacts under `docs/features/active/2026-04-18-bundle-install-script-36/evidence/baseline/` tagged `baseline-refinement-*.2026-04-19T00-00.md`. Each artifact contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

- [x] [PB-T1] Read `.claude/rules/general-code-change.md` and append path + one-line purpose to `evidence/baseline/phase0-instructions-read.refinement.2026-04-19T00-00.md`. Header fields: `Timestamp:`, `Policy Order:`, and explicit list of files read (starts empty, appended to by PB-T1 through PB-T5).
- [x] [PB-T2] Read `.claude/rules/general-unit-test.md` and append path + purpose.
- [x] [PB-T3] Read `.claude/rules/powershell.md` and append path + purpose.
- [x] [PB-T4] Read `.claude/rules/tonality.md` and append path + purpose.
- [x] [PB-T5] Read the refreshed `spec.md` and `user-story.md` (post-Phase A) and append a confirmation plus re-counted AC totals for each file.
- [x] [PB-T6] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/` and `tests/scripts/` in check-only mode; persist to `evidence/baseline/baseline-refinement-poshqc-format.2026-04-19T00-00.md` with all four schema fields.
- [x] [PB-T7] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against `scripts/` and `tests/scripts/`; persist to `evidence/baseline/baseline-refinement-poshqc-analyze.2026-04-19T00-00.md` including rule-violation counts.
- [x] [PB-T8] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled; persist to `evidence/baseline/baseline-refinement-pester.2026-04-19T00-00.md` with pass/fail counts AND numeric repo-wide line-coverage percentage AND per-file coverage for `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`.
- [x] [PB-T9] Record pre-change line counts for every production file touched by this refinement: `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/Install.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Uninstall.ps1`. Persist to `evidence/baseline/baseline-refinement-line-counts.2026-04-19T00-00.md` with all four schema fields. Acceptance: every file `<= 500` lines at baseline.
- [x] [PB-T10] Record pre-change line counts for every test file touched: `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`. Same artifact fields. Acceptance: every file `<= 500` lines.
- [x] [PB-T11] Snapshot the current `manifest.json` schema shape by generating a single bundle via `.\scripts\Publish.ps1 -Version '0.0.0.0' -SkipSign -OutputDir 'artifacts/publish-refinement-baseline'` (dry-run note: if this exercises heavy dotnet work, instead read a prior published bundle's `manifest.json` from `artifacts/publish/` if present). Persist the shape observation (top-level keys, sample `files[0]` keys) to `evidence/baseline/baseline-refinement-manifest-shape.2026-04-19T00-00.md` with all four schema fields. Acceptance: the current schema already emits `{ version, generatedAt, files }` (confirmed by reading `Write-PublishManifest` in `scripts/Publish.Helpers.psm1` lines 394-442 at HEAD).

---

## Phase C — Publish Pipeline: Install-Script Staging + Manifest Schema

Stage the three install-related files into every bundle and update the manifest emitter. Batch caps of 3 production + 3 test files per batch. Line-count guards before and after each production file edit.

### Phase C — Batch 1 (Publish.Helpers: Copy-InstallScriptsIntoBundle)

- [x] [PC-T1] Read current line count of `scripts/Publish.Helpers.psm1` and append to `evidence/baseline/baseline-refinement-line-counts.2026-04-19T00-00.md` under a "Pre-Phase-C line counts" subsection. Acceptance: `<= 500` pre-change.
- [x] [PC-T2] Add `Copy-InstallScriptsIntoBundle` to `scripts/Publish.Helpers.psm1` with `[CmdletBinding(SupportsShouldProcess=$true)]`. Parameters: `-RepoRoot` (string, mandatory), `-BundleRoot` (string, mandatory). Behavior: resolves `$srcScriptsDir = Join-Path $RepoRoot 'scripts'`; iterates `'Install.ps1', 'Uninstall.ps1', 'Install.Helpers.psm1'`; for each, verifies the source file exists at `<srcScriptsDir>/<name>` and throws with the missing path when absent; copies the file to `Join-Path $BundleRoot $name` under `ShouldProcess`. Uses `Copy-Item -LiteralPath ... -Destination ... -Force`.
- [x] [PC-T3] Update `Export-ModuleMember` at the bottom of `scripts/Publish.Helpers.psm1` (line 444-456) to include `'Copy-InstallScriptsIntoBundle'`.
- [x] [PC-T4] Add `Describe 'Copy-InstallScriptsIntoBundle'` to `tests/scripts/Publish.Helpers.Tests.ps1` with three `It` blocks: (a) invokes `Copy-Item` three times with the expected `-LiteralPath`/`-Destination` pairs in order `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1`; (b) throws with the missing path in the message when any of the three source files is absent (mock `Test-Path` to return `$false` for one path); (c) `-WhatIf` produces zero `Copy-Item` invocations. Mock `Copy-Item` and `Test-Path` at module scope.
- [x] [PC-T5] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Publish.Helpers.psm1` and `tests/scripts/Publish.Helpers.Tests.ps1`. Restart Batch 1 QA from PC-T5 if any file changes.
- [x] [PC-T6] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same two files. Zero new diagnostics required.
- [x] [PC-T7] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled. Acceptance: zero test regressions vs Phase B; targeted coverage for new `Copy-InstallScriptsIntoBundle` lines `>= 90%`.
- [x] [PC-T8] Verify end-state line count of `scripts/Publish.Helpers.psm1` is still `<= 500`. Append to `evidence/baseline/baseline-refinement-line-counts.2026-04-19T00-00.md` under "Post-Phase-C-Batch-1 line counts". Acceptance: pass.

### Phase C — Batch 2 (Publish.Helpers: manifest schema rename + Publish.ps1 wiring)

- [x] [PC-T9] Read pre-change line count of `scripts/Publish.Helpers.psm1` and `scripts/Publish.ps1`. Append to baseline-refinement-line-counts with "Pre-Phase-C-Batch-2" subsection. Acceptance: both `<= 500`.
- [x] [PC-T10] Update `Write-PublishManifest` in `scripts/Publish.Helpers.psm1` (lines 394-442) so the emitted manifest object's top-level shape is `{ version = $Version; files = @($sortedEntries) }`. Remove the `generatedAt` field from the emitted object. Rename any internal references; ensure `ConvertTo-Json -Depth 5` still produces the documented key order (`version` first, `files` last). The function's existing `-Version` parameter and `ValidatePattern` remain.
- [x] [PC-T11] Edit `scripts/Publish.ps1` Stage 4/5 region (between MSIX pack at line 167 and manifest write at line 179) to add a new stage that invokes `Copy-InstallScriptsIntoBundle -RepoRoot $RepoRoot -BundleRoot $BundleRoot` immediately BEFORE the `Write-PublishManifest` call so the install scripts are listed in the resulting manifest's `files`. Emit a new `[install-scripts]` progress line with `Write-Information`.
- [x] [PC-T12] Update `tests/scripts/Publish.Helpers.Tests.ps1` `Describe 'Write-PublishManifest'` block (including the `It` at line 388) so its assertions verify: (a) emitted JSON contains exactly top-level keys `version` and `files` (no `generatedAt`); (b) `version` matches the supplied `-Version` parameter value; (c) `files` is an array whose entries retain `path`, `size`, `sha256`; (d) `manifest.json` is excluded from `files`.
- [x] [PC-T13] Update `tests/scripts/Publish.Tests.ps1` stage-ordering test: add an assertion that `Copy-InstallScriptsIntoBundle` is invoked exactly once, and that its call order is AFTER `Invoke-MakeAppx` / `Invoke-SignTool` and BEFORE `Write-PublishManifest`. Mock `Copy-InstallScriptsIntoBundle` at module scope with a call-log contributor.
- [x] [PC-T14] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Publish.Helpers.psm1`, `scripts/Publish.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`. Restart Batch 2 QA on any change.
- [x] [PC-T15] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same four files. Zero new diagnostics.
- [x] [PC-T16] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage. Acceptance: zero regressions, coverage on changed `Write-PublishManifest` lines `>= 90%`, coverage on changed `Publish.ps1` stage lines `>= 90%`.
- [x] [PC-T17] Verify end-state line counts of `scripts/Publish.Helpers.psm1` and `scripts/Publish.ps1` are each `<= 500`. Append to baseline-refinement-line-counts under "Post-Phase-C-Batch-2".

---

## Phase D — Install.Helpers.psm1 Update (retire Find-NewestPublishVersion, add Get-ManifestVersion, update Test-ManifestIntegrity)

Retire the auto-detect helper. Add the version reader. Update the integrity check to accept only the new schema. Batch caps of 3 production + 3 test files per batch.

### Phase D — Batch 1 (retire + add + update)

- [x] [PD-T1] Read pre-change line count of `scripts/Install.Helpers.psm1` and `tests/scripts/Install.Helpers.Tests.ps1`. Append to baseline-refinement-line-counts under "Pre-Phase-D". Acceptance: both `<= 500`.
- [x] [PD-T2] Delete the `Find-NewestPublishVersion` function body from `scripts/Install.Helpers.psm1` (current lines 12-43 at HEAD `453343e`). Remove the line `'Find-NewestPublishVersion', \`` from the `Export-ModuleMember` list at the bottom (line 436). Do not leave a stub.
- [x] [PD-T3] Add `Get-ManifestVersion` to `scripts/Install.Helpers.psm1` at the top of the function list. Signature: `[CmdletBinding()] [OutputType([string])] param([Parameter(Mandatory=$true)][string]$BundleRoot)`. Behavior: composes `$manifestPath = Join-Path $BundleRoot 'manifest.json'`; throws with the path when `-not (Test-Path -LiteralPath $manifestPath)`; loads the JSON; throws with a clear message when `$manifest.version` is null, empty, or fails `[System.Version]::TryParse`; returns the `version` field as a string.
- [x] [PD-T4] Update `Test-ManifestIntegrity` in `scripts/Install.Helpers.psm1` (current lines 45-110) so that: (a) the function now asserts the loaded manifest has both a `version` (string) and a `files` (array) property and throws a specific schema-violation message when either is missing; (b) all references to `$manifest` entries iterate `$manifest.files` only (the loop body is already correct at lines 70-93 via `@($manifest.files)`, so preserve that); (c) the version field's presence is asserted but its value is not compared against any external value here — that cross-check is the orchestrator's responsibility. Update the function's comment-based help to describe the new schema.
- [x] [PD-T5] Update `Export-ModuleMember` at the bottom of `scripts/Install.Helpers.psm1` (line 435-448): remove the `Find-NewestPublishVersion` entry; add a `Get-ManifestVersion` entry as the first exported name. Final exported function count: `13 - 1 + 1 = 13`.
- [x] [PD-T6] Update the `Describe 'Install.Helpers.psm1' > Context 'module export surface'` test in `tests/scripts/Install.Helpers.Tests.ps1` (current lines 19-51) so its asserted exported-function list replaces `Find-NewestPublishVersion` with `Get-ManifestVersion`. The assertion remains an exact-set match.
- [x] [PD-T7] Delete `Describe 'Find-NewestPublishVersion'` (the `Context` block at current lines 52-88 in `tests/scripts/Install.Helpers.Tests.ps1`) entirely.
- [x] [PD-T8] Add `Describe 'Get-ManifestVersion'` (or `Context` block inside the existing `Describe 'Install.Helpers.psm1'`) to `tests/scripts/Install.Helpers.Tests.ps1` with four `It` blocks: (a) returns the `version` field value when `manifest.json` exists with a valid 4-part version; (b) throws with the bundle root in the message when `manifest.json` is absent; (c) throws with a schema-violation message when `manifest.json` has no top-level `version` property; (d) throws with a parse-failure message when `version` is non-parseable (e.g., `'not-a-version'`). Mock `Test-Path` and `Get-Content` at module scope. No temp files.
- [x] [PD-T9] Update the `Describe 'Test-ManifestIntegrity'` block in `tests/scripts/Install.Helpers.Tests.ps1` (current lines 90-157) so every `BeforeEach` or fixture that composes a manifest JSON payload uses the new `{ version, files }` shape (the `files` array continues to contain `{ path, size, sha256 }` entries). Add one new `It 'throws when manifest lacks the top-level version field'` block. Existing four `It` blocks covering hash match / hash mismatch / missing file / unlisted on-disk file remain and continue to pass under the new shape.
- [x] [PD-T10] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Install.Helpers.psm1` and `tests/scripts/Install.Helpers.Tests.ps1`. Restart Phase D Batch 1 QA from PD-T10 on any change.
- [x] [PD-T11] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against the same two files. Zero new diagnostics.
- [x] [PD-T12] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage. Acceptance: zero regressions, coverage on `Get-ManifestVersion` `>= 90%`, coverage on changed `Test-ManifestIntegrity` lines `>= 90%`.
- [x] [PD-T13] Verify end-state line count of `scripts/Install.Helpers.psm1` is `<= 500`. Append to baseline-refinement-line-counts under "Post-Phase-D".

---

## Phase E — Install.ps1 Update (remove -Version, default -SourcePath to $PSScriptRoot, remove auto-detect)

Rewire the orchestrator to self-locate via `$PSScriptRoot`, remove the `-Version` parameter, and read the version from the manifest. Batch caps of 3 production + 3 test files per batch.

### Phase E — Batch 1 (Install.ps1 parameter + bundle-selection stage rewrite)

- [x] [PE-T1] Read pre-change line count of `scripts/Install.ps1` and `tests/scripts/Install.Tests.ps1`. Append to baseline-refinement-line-counts under "Pre-Phase-E". Acceptance: both `<= 500`.
- [x] [PE-T2] Edit `scripts/Install.ps1` `param()` block (current lines 57-69) to: (a) remove the `-Version` parameter and its `ValidatePattern` attribute; (b) change `-SourcePath` default from `''` to `$PSScriptRoot` (note: `$PSScriptRoot` can be referenced as a default value in a PowerShell `param()` block because it is an automatic variable populated at script parse time). Preserve `-AllowUnsigned`, `-SkipDocker`, `-Force`.
- [x] [PE-T3] Edit `scripts/Install.ps1` file-header comment block (current lines 1-56) so the `.PARAMETER Version` block is removed; the `.PARAMETER SourcePath` block documents the new default (`$PSScriptRoot`) and role (dev/test override, production default is the bundle root); the `.SYNOPSIS`/`.DESCRIPTION` references to auto-detect are replaced with "The script self-locates the bundle via `$PSScriptRoot`."
- [x] [PE-T4] Replace `scripts/Install.ps1` Stage 1 bundle-selection block (current lines 98-121) with a new block that: (a) sets `$BundleRoot = $SourcePath` (which defaults to `$PSScriptRoot`); (b) throws with the path in the message when `-not (Test-Path -LiteralPath (Join-Path $BundleRoot 'manifest.json'))`; (c) reads `$ResolvedVersion = Get-ManifestVersion -BundleRoot $BundleRoot`; (d) emits `[install:select] Selected bundle root $BundleRoot (version $ResolvedVersion)` via `Write-Information`. Remove the prior `$RepoRoot`, `$PublishRoot`, and `Find-NewestPublishVersion` logic entirely.
- [x] [PE-T5] Update `scripts/Install.ps1` MSIX-path composition (current line 178) so it still uses `$ResolvedVersion` (no change needed — the variable continues to be populated, just from `Get-ManifestVersion` now). Verify by reading the edit output.
- [x] [PE-T6] Verify `scripts/Install.ps1` dot-source guard, admin precheck, Docker readiness, bundle copy, .env guard, MSIX install, compose up/wait, install-record write stages are OTHERWISE UNCHANGED. A single grep for `Find-NewestPublishVersion` across `scripts/` must return zero matches. Persist result to `evidence/other/install-ps1-auto-detect-removed.refinement.2026-04-19T00-00.md` with all four schema fields.
- [x] [PE-T7] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/Install.ps1`. Restart Phase E Batch 1 QA from PE-T7 on any change.
- [x] [PE-T8] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against `scripts/Install.ps1`. Zero new diagnostics.
- [x] [PE-T9] Verify end-state line count of `scripts/Install.ps1` is `<= 500`. Append to baseline-refinement-line-counts under "Post-Phase-E-Batch-1".

### Phase E — Batch 2 (Install.Tests.ps1 rewrite)

- [x] [PE-T10] Edit `tests/scripts/Install.Tests.ps1` `BeforeAll` (current lines 19-90) to: (a) remove the `Mock Find-NewestPublishVersion` block (lines 42-45); (b) add a `Mock Get-ManifestVersion` that returns `'1.2.3.0'` and logs its invocation via the existing `$global:InstallTestCalls` list.
- [x] [PE-T11] Rewrite `Context 'parameter binding'` in `tests/scripts/Install.Tests.ps1` (current lines 92-106): (a) drop the `It 'rejects an invalid 3-part -Version (ValidatePattern)'` block; (b) drop the `It '-SourcePath overrides the newest-version auto-detect'` block and replace with `It '-SourcePath overrides the default $PSScriptRoot'` that invokes the script with `-SourcePath 'C:\custom\bundle'` and asserts `$global:InstallTestCalls` does NOT contain `Find-NewestPublishVersion` (defensive check; the mock no longer exists) and `$global:InstallTestCalls[0] -eq 'Get-ManifestVersion'`; (c) add a new `It 'defaults -SourcePath to $PSScriptRoot when not supplied'` block that invokes the script with no `-SourcePath` argument and asserts the first `Get-ManifestVersion` call's `-BundleRoot` argument equals `$PSScriptRoot` (captured via a call-log wrapper on `Mock Get-ManifestVersion`).
- [x] [PE-T12] Delete `Context '-Version path'` entirely from `tests/scripts/Install.Tests.ps1` (current lines 223+ referring to `-Version '2.0.0.0'`). The `-Version` parameter no longer exists.
- [x] [PE-T13] Update `Context 'stage ordering (happy path)'` (first helper call assertion) in `tests/scripts/Install.Tests.ps1`: change the first expected call from `Find-NewestPublishVersion` to `Get-ManifestVersion` (or, if the test currently asserts the sequence `Test-ManifestIntegrity` -> `Test-DockerAvailable` -> ..., prepend `Get-ManifestVersion` as the new first call).
- [x] [PE-T14] Add a new `Context 'bundle-root self-location'` to `tests/scripts/Install.Tests.ps1` with two `It` blocks: (a) when `-SourcePath` is not supplied, the script computes `$BundleRoot = $PSScriptRoot` and passes that value to `Get-ManifestVersion` and `Test-ManifestIntegrity`; (b) when the supplied bundle root directory has no `manifest.json` sibling, the script throws with the path in the message before any further stage runs. Use a Pester `TestDrive` to stage a bundle layout: `TestDrive:\bundle\manifest.json`, `TestDrive:\bundle\executables\x\a.txt`, `TestDrive:\bundle\docker\docker-compose.yml`, `TestDrive:\bundle\msix\OpenClaw.MailBridge_1.2.3.0_x64.msix`. The manifest.json body uses the new `{ version: '1.2.3.0', files: [...] }` schema.
- [x] [PE-T15] Run `mcp__drmCopilotExtension__run_poshqc_format` against `tests/scripts/Install.Tests.ps1`. Restart Phase E Batch 2 QA from PE-T15 on any change.
- [x] [PE-T16] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against `tests/scripts/Install.Tests.ps1`. Zero new diagnostics.
- [x] [PE-T17] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage. Acceptance: zero regressions, coverage on changed `scripts/Install.ps1` lines `>= 90%`, repo-wide line coverage `>= 80%`.
- [x] [PE-T18] Verify end-state line counts of `scripts/Install.ps1` and `tests/scripts/Install.Tests.ps1` are each `<= 500`. Append to baseline-refinement-line-counts under "Post-Phase-E-Batch-2".

---

## Phase F — Documentation Updates + Uninstall.ps1 Confirmation

Update `README.md` and `docs/mailbridge-runbook.md` Path D to describe the self-locating flow. Confirm `Uninstall.ps1` behavior is unchanged (but verify the install-record schema is unaffected by Phase C/D/E changes).

- [x] [PF-T1] Edit `README.md` "What It Does" block (current lines 18-28) to replace the scripted-bundle bullet so the operator instruction reads: "Run `.\Install.ps1` from inside the bundle directory (for example `cd artifacts/publish/<version>; .\Install.ps1`). The install scripts ship INSIDE the bundle and self-locate via `$PSScriptRoot`."
- [x] [PF-T2] Edit `README.md` "Repository Layout" table (current line 51) `scripts/` row description so the bullet about `Install.ps1`, `Uninstall.ps1`, `Install.Helpers.psm1` includes a note that these files are ADDITIONALLY staged into every bundle by `Publish.ps1` and intended to run from the bundle root, not from the repo's `scripts/` directory.
- [x] [PF-T3] Edit `docs/mailbridge-runbook.md` Path D section (current line 471 onward). Replace all `.\scripts\Install.ps1` invocations with a two-line pattern `cd artifacts/publish/<version>` followed by `.\Install.ps1`. Remove any `-Version '1.2.3.0'` example from Path D (current line 499). Keep `-SourcePath` examples but annotate them as dev/test overrides. Ensure `-AllowUnsigned`, `-SkipDocker`, `-Force` examples remain.
- [x] [PF-T4] Update the Path D "Prerequisites" and "Commands" subsections to state explicitly that Path D no longer requires the operator to locate a specific version; the operator simply `cd`s into the bundle directory (produced by `Publish.ps1` or downloaded and extracted) and runs `.\Install.ps1`.
- [x] [PF-T5] Update the Troubleshooting table in `docs/mailbridge-runbook.md` (the rows added by the original plan at Phase 4 P4-T3): remove or reword any row that assumed `artifacts/publish/` auto-detection (for example "Empty publish root"); add one row "No manifest.json at bundle root" with remediation text "Ensure the script is being run from the bundle directory produced by Publish.ps1, not from the repo's `scripts/` directory."
- [x] [PF-T6] Confirm `scripts/Uninstall.ps1` needs zero behavioral changes: grep for `Find-NewestPublishVersion`, `-Version`, `artifacts/publish` in `scripts/Uninstall.ps1`. Persist result to `evidence/other/uninstall-ps1-no-change.refinement.2026-04-19T00-00.md` with all four schema fields. Acceptance: zero matches. Confirm the install-record schema fields consumed by `Uninstall.ps1` (`destinationPath`, `packageFullName`, `composeProjectName`, `composeFilePath`, `skipDocker`) are all still populated by `scripts/Install.ps1` after Phase E edits.
- [x] [PF-T7] Record a docs-preservation artifact `evidence/other/docs-refinement.refinement.2026-04-19T00-00.md` with `Timestamp:`, `Command:` (the `git diff --stat -- README.md docs/mailbridge-runbook.md` invocation), `EXIT_CODE:`, `Output Summary:` listing edited sections and confirming Path A, B, C narratives remain intact (zero removals of Path A/B/C section headings).

---

## Phase G — Final QA Gate (refinement)

Run the complete PoshQC loop (format -> analyze -> test) against the whole repo and compare against the Phase B refinement baseline. Every command step produces its own evidence artifact under `evidence/qa-gates/` tagged `*.refinement.2026-04-19T00-00.md`. Restart the loop from PG-T1 if any step auto-fixes files or fails.

- [x] [PG-T1] Run `mcp__drmCopilotExtension__run_poshqc_format` against `scripts/` and `tests/scripts/`. Persist output to `evidence/qa-gates/final-poshqc-format.refinement.2026-04-19T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (pass/fail, any files changed). If any file changes, restart the phase from PG-T1.
- [x] [PG-T2] Run `mcp__drmCopilotExtension__run_poshqc_analyze` against `scripts/` and `tests/scripts/`. Persist output to `evidence/qa-gates/final-poshqc-analyze.refinement.2026-04-19T00-00.md` with all four schema fields and a diagnostic count. Acceptance: zero diagnostics, or zero new diagnostics vs Phase B baseline with every residual pre-existing diagnostic individually accounted for.
- [x] [PG-T3] Run `mcp__drmCopilotExtension__run_poshqc_test` with coverage collection enabled. Persist output to `evidence/qa-gates/final-pester.refinement.2026-04-19T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric post-change repo-wide line coverage AND per-file coverage for `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`. Acceptance: repo-wide `>= 80%`, per-file targeted `>= 90%` on files changed in this refinement (`Install.ps1`, `Install.Helpers.psm1`, `Publish.ps1`, `Publish.Helpers.psm1`), zero test failures, zero regressions vs Phase B baseline.
- [x] [PG-T4] Emit a coverage-delta artifact `evidence/qa-gates/coverage-delta.refinement.2026-04-19T00-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` recording (a) baseline repo-wide coverage (from PB-T8), (b) post-change repo-wide coverage (from PG-T3), (c) per-file targeted coverage for each refinement-changed production file, (d) explicit assertion `post-change >= baseline - 0` (no regression).
- [x] [PG-T5] Verify end-state file presence: `scripts/Install.ps1`, `scripts/Uninstall.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/Publish.Helpers.psm1`, `tests/scripts/Install.Tests.ps1`, `tests/scripts/Uninstall.Tests.ps1`, `tests/scripts/Install.Helpers.Tests.ps1`, `tests/scripts/Publish.Tests.ps1`, `tests/scripts/Publish.Helpers.Tests.ps1` all present. Persist to `evidence/qa-gates/end-state-file-presence.refinement.2026-04-19T00-00.md` with all four schema fields.
- [x] [PG-T6] Verify end-state line-count policy: every file listed in PG-T5 is `<= 500` lines. Persist counts and pass/fail to `evidence/qa-gates/end-state-line-counts.refinement.2026-04-19T00-00.md` with all four schema fields.
- [x] [PG-T7] Reconcile the refreshed Definition of Done (post-Phase A) against artifacts from Phases A-G. Write `evidence/qa-gates/definition-of-done-reconciliation.refinement.2026-04-19T00-00.md` with one row per DoD item (using the post-refresh text from Phase A), status PASS/FAIL, and the evidence artifact path that proves it. Include a second table re-asserting every `user-story.md` Acceptance Criteria bullet. Acceptance: all DoD and AC items marked PASS with a cited artifact. Include explicit rows for the three refinement-specific DoD bullets added at PA-T14 and the two refinement-specific AC bullets added at PA-T22.

---

## Preflight Checklist

- **Every AC that the refinement changes is mapped to a Phase A spec-refresh task AND an implementation task that realizes it.**
  - `spec.md` Behavior > Main path step 2 (auto-detect removed) -> Phase A PA-T3 + Phase E PE-T4.
  - `spec.md` Inputs/Outputs `-Version` row removed -> Phase A PA-T6 + Phase E PE-T2.
  - `spec.md` API/CLI surface helper-module table (retire `Find-NewestPublishVersion`, add `Get-ManifestVersion`, revise `Test-ManifestIntegrity`) -> Phase A PA-T8 + Phase D PD-T2/PD-T3/PD-T4/PD-T5.
  - `spec.md` DoD bullet 1 (`-Version` removed) -> Phase A PA-T13 + Phase E PE-T2.
  - `spec.md` new DoD bullets (install-script staging, new manifest schema, `$PSScriptRoot` execution) -> Phase A PA-T14 + Phase C PC-T2/PC-T10/PC-T11 + Phase E PE-T4.
  - `spec.md` Owner Decisions "Newest-bundle detection" rewrite -> Phase A PA-T16 + Phase E PE-T4 + Phase D PD-T2.
  - `spec.md` Seeded Test Condition #5 rewrite + new bundle-staging bullet -> Phase A PA-T15 + Phase E PE-T14 + Phase C PC-T13.
  - `user-story.md` Story Statement rewrite -> Phase A PA-T19 + Phase E PE-T4.
  - `user-story.md` Scenario "Install a bundle on a clean host" rewrite -> Phase A PA-T20 + Phase E PE-T14.
  - `user-story.md` AC#1 rebaseline -> Phase A PA-T23 + Phase G PG-T7.
  - `user-story.md` AC#2 rewrite (`-SourcePath` default / `-Version` removed) -> Phase A PA-T21 + Phase E PE-T2/PE-T4.
  - `user-story.md` new AC bullets (install-script staging, `{ version, files }` manifest) -> Phase A PA-T22 + Phase C PC-T2/PC-T10 + Phase D PD-T3.

- **No batch exceeds 3 production + 3 test files.**
  - Phase C Batch 1 touches `scripts/Publish.Helpers.psm1` + `tests/scripts/Publish.Helpers.Tests.ps1` (1 prod + 1 test).
  - Phase C Batch 2 touches `scripts/Publish.Helpers.psm1` + `scripts/Publish.ps1` + `tests/scripts/Publish.Helpers.Tests.ps1` + `tests/scripts/Publish.Tests.ps1` (2 prod + 2 test).
  - Phase D Batch 1 touches `scripts/Install.Helpers.psm1` + `tests/scripts/Install.Helpers.Tests.ps1` (1 prod + 1 test).
  - Phase E Batch 1 touches `scripts/Install.ps1` (1 prod + 0 test).
  - Phase E Batch 2 touches `tests/scripts/Install.Tests.ps1` (0 prod + 1 test).
  - Phase F touches `README.md` + `docs/mailbridge-runbook.md` (docs only; not counted against the prod/test cap).

- **No file is projected to exceed 500 lines post-refinement.** Current baseline (measured against HEAD `453343e`): `scripts/Install.ps1` ~210 lines, `scripts/Install.Helpers.psm1` ~448 lines, `scripts/Uninstall.ps1` ~95 lines, `scripts/Publish.ps1` ~183 lines, `scripts/Publish.Helpers.psm1` ~456 lines. Refinement deltas (estimated): `Install.Helpers.psm1` nets `-32` lines (delete `Find-NewestPublishVersion` 32 lines) plus `+25` lines (add `Get-ManifestVersion`) = 441 lines. `Publish.Helpers.psm1` nets `+30` lines (add `Copy-InstallScriptsIntoBundle`) = 486 lines; still `< 500`. `Publish.ps1` nets `+6` lines (new stage + `Write-Information` call) = 189 lines. `Install.ps1` nets `-10` lines (remove `-Version`, remove auto-detect block, add trivial guard) = 200 lines. All projections enforced by PB-T9/PB-T10 baseline checks and PG-T6 end-state line-count gate.

- **All three refinement-locked decisions are covered by at least one task each.**
  - Install-script staging in Publish: Phase C PC-T2 (`Copy-InstallScriptsIntoBundle` helper), PC-T11 (wire into `Publish.ps1`), PC-T13 (stage-ordering test).
  - Self-locating Install via `$PSScriptRoot`: Phase E PE-T2 (`-SourcePath` default = `$PSScriptRoot`, `-Version` removed), PE-T4 (bundle-selection stage rewrite), PE-T14 (self-location tests).
  - New `{ version, files }` manifest schema: Phase C PC-T10 (`Write-PublishManifest` emits new schema), Phase D PD-T3 (`Get-ManifestVersion` reads new schema), PD-T4 (`Test-ManifestIntegrity` asserts new schema), Phase C PC-T12 (emitter tests), PD-T8/PD-T9 (reader tests).

- **Phase A (spec refresh) runs first.** All subsequent phases read the post-refresh `spec.md` and `user-story.md` for their atomic acceptance criteria; the baseline capture in Phase B explicitly re-reads the refreshed documents (PB-T5).

- **Evidence schema and canonical locations.** Every command-bearing task in Phase B, Phase C, Phase D, Phase E, Phase F, and Phase G specifies `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` fields and writes to canonical `evidence/baseline/`, `evidence/other/`, or `evidence/qa-gates/` paths per `evidence-and-timestamp-conventions`.

- **Atomicity.** Every task describes a single binary outcome: single function addition or deletion, single `Describe`/`Context` block change, single edit to a spec bullet, single command run, single end-state assertion. No bucket or umbrella tasks detected.

- **Final QA loop structure.** Phase G runs format -> analyze -> test in order with an explicit restart-on-change directive at PG-T1 and no `SKIPPED` branches on any command task. Coverage evidence is mandatory and numeric at both PB-T8 (baseline) and PG-T3/PG-T4 (final).

- **Plan-path continuity.** This plan is written to `docs/features/active/2026-04-18-bundle-install-script-36/plan-refinement.2026-04-19T00-00.md` and all preflight revision iterations will update this exact file. The predecessor `plan.2026-04-18T00-00.md` is preserved unchanged.

DIRECTIVE: PREFLIGHT VALIDATION ONLY

PREFLIGHT: ALL CLEAR
