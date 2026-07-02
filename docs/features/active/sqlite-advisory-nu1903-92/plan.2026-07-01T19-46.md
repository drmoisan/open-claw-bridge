# sqlite-advisory-nu1903 (Plan)

- **Issue:** #92
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-01T19-46
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** minor-audit
- **Tier:** T1 (OpenClaw.Core and OpenClaw.MailBridge data layer)

**Requirements source:** `docs/features/active/sqlite-advisory-nu1903-92/issue.md` is the sole requirements source. The `## Acceptance Criteria` section (AC-1..AC-6) is the only acceptance-criteria source for this minor-audit. `spec.md` and `user-story.md` are not required and must not exist in this feature folder.

**Fail-closed evidence rule:** This plan includes explicit baseline, targeted-verification, and end-state evidence tasks with numeric coverage capture. If any required baseline artifact, QA artifact, or coverage-comparison artifact is missing or contains placeholders, the verdict is BLOCKED or INCOMPLETE, never PASS.

**Evidence location invariant:** All evidence artifacts resolve to `docs/features/active/sqlite-advisory-nu1903-92/evidence/<kind>/` per `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Non-canonical `artifacts/` evidence paths are forbidden.

**Scope facts confirmed during planning:**
- `Microsoft.Data.Sqlite` is pinned per-csproj at `8.0.11` in both `src/OpenClaw.Core/OpenClaw.Core.csproj` (line 17) and `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` (line 23). No `Directory.Packages.props` or `Directory.Build.props` exists, so the edit is made in both `.csproj` files directly (not centrally).
- No `.config/dotnet-tools.json` (local tool manifest) exists in this repository. `dotnet tool restore` has no manifest to restore. CSharpier is invoked via the global subcommand form `csharpier check .` / `csharpier format .` (CSharpier 1.x). Do not use `dotnet csharpier` or `dotnet tool restore`.
- Solution file: `OpenClaw.MailBridge.sln`. Coverage runsettings: `mailbridge.runsettings` (cobertura, excludes `[*.Tests]*`).
- Target frameworks: `net10.0` (Core) and `net10.0-windows` (MailBridge). SDK pinned to `10.0.201` in `global.json`.

---

### Phase 0 — Policy Read & Baseline Capture

- [x] [P0-T1] Read policy files in the required order and record the read evidence: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/architecture-boundaries.md`. Write `docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/phase0-instructions-read.md` with `Timestamp:`, `Policy Order:`, and the explicit list of files read.
  - Acceptance: `phase0-instructions-read.md` exists with `Timestamp:`, `Policy Order:`, and all six files listed as read. Supports AC-6.

- [x] [P0-T2] Capture the baseline dependency state: record the current `Microsoft.Data.Sqlite` version (`8.0.11`) from `src/OpenClaw.Core/OpenClaw.Core.csproj` and `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`, and run `dotnet list OpenClaw.MailBridge.sln package --include-transitive` (or per-project equivalent) to capture the transitive `SQLitePCLRaw.lib.e_sqlite3` version (`2.1.6`). Write `docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-dependency-versions.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (the direct `Microsoft.Data.Sqlite` version in each csproj and the transitive `SQLitePCLRaw.lib.e_sqlite3` version).
  - Acceptance: artifact exists and `Output Summary:` records `Microsoft.Data.Sqlite 8.0.11` in both csproj and transitive `SQLitePCLRaw.lib.e_sqlite3 2.1.6`. Supports AC-1.

- [x] [P0-T3] Capture the baseline NU1903 failure: run `dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror` (restore allowed) and record the failing result. Write `docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-nu1903-build.md` with `Timestamp:`, `Command: dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror`, `EXIT_CODE:` (expected non-zero), and `Output Summary:` (the `NU1903` error line(s) referencing GHSA-2m69-gcr7-jv3q and `SQLitePCLRaw.lib.e_sqlite3` 2.1.6).
  - Acceptance: artifact exists with non-zero `EXIT_CODE:` and `Output Summary:` quoting the `NU1903` GHSA-2m69-gcr7-jv3q error. Establishes the fail-before state for AC-2.

- [x] [P0-T4] Capture the baseline CSharpier format state: run `csharpier check .` and write `docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-csharpier.md` with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, and `Output Summary:` (pass/fail and count of unformatted files).
  - Acceptance: artifact exists with all four fields populated. Supports AC-6.

- [x] [P0-T5] Capture the baseline test and coverage state: run `dotnet test OpenClaw.MailBridge.sln -c Release --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and write `docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-test-coverage.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric baseline line coverage % and branch coverage % from the cobertura report and the passed/failed test counts. If the baseline build cannot complete because of the NU1903 error under `/warnaserror`, note that `dotnet test` here runs without `/warnaserror` and record whether tests execute; if tests cannot run at baseline, record the exact blocking reason and defer numeric coverage capture to Phase 2 post-bump with an explicit note.
  - Acceptance: artifact exists with all four fields; `Output Summary:` records numeric baseline line% and branch% or a recorded blocking reason with deferral note. Supports AC-4.

### Phase 1 — Minimal Dependency Fix (Option B) with Option A Fallback

- [x] [P1-T1] Resolve the target `Microsoft.Data.Sqlite` version: identify the lowest current maintained version whose transitive `SQLitePCLRaw.lib.e_sqlite3` is `>= 2.1.10` (clears GHSA-2m69-gcr7-jv3q). Record the resolved exact version and its transitive `SQLitePCLRaw.lib.e_sqlite3` version in `docs/features/active/sqlite-advisory-nu1903-92/evidence/other/resolved-target-version.md` with `Timestamp:`, `Command:` (the resolution command used, e.g. `dotnet package search` or `dotnet list package --include-transitive` after a trial bump), `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: artifact records one exact `Microsoft.Data.Sqlite` version and confirms its transitive `SQLitePCLRaw.lib.e_sqlite3 >= 2.1.10`. Supports AC-1.

- [x] [P1-T2] Edit `src/OpenClaw.Core/OpenClaw.Core.csproj` line 17 to set the `Microsoft.Data.Sqlite` `Version` to the exact resolved version from P1-T1. Change only this attribute; make no other edit to the file.
  - Acceptance: `src/OpenClaw.Core/OpenClaw.Core.csproj` `PackageReference Include="Microsoft.Data.Sqlite"` `Version` equals the P1-T1 resolved version; no other line changed. Supports AC-1, AC-5.

- [x] [P1-T3] Edit `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` line 23 to set the `Microsoft.Data.Sqlite` `Version` to the identical resolved version from P1-T1. Change only this attribute; make no other edit to the file.
  - Acceptance: `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` `Microsoft.Data.Sqlite` `Version` equals the exact same string as in `src/OpenClaw.Core/OpenClaw.Core.csproj` (lockstep). Supports AC-1.

- [x] [P1-T4] Verify no advisory suppression was introduced: confirm neither csproj (nor any file in the diff) adds `NoWarn` containing `NU1903` or changes `NuGetAuditMode`. Record the diff scope in `docs/features/active/sqlite-advisory-nu1903-92/evidence/other/no-suppression-check.md` with `Timestamp:`, `Command:` (e.g. `git diff --stat` and a grep for `NU1903`/`NuGetAuditMode`), `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: artifact confirms zero occurrences of new `NoWarn NU1903` or `NuGetAuditMode` changes; diff is limited to the two `Microsoft.Data.Sqlite` version attributes (plus any narrowly-scoped, justified product-code adjustment covered by tests per AC-5). Supports AC-3, AC-5.

- [ ] [P1-T5] Option A fallback conditional: run `dotnet restore OpenClaw.MailBridge.sln` and `dotnet build OpenClaw.MailBridge.sln -c Release` after the bump. If restore or build fails on `net10.0`/`net10.0-windows` in a way attributable to the `Microsoft.Data.Sqlite` bump (not to the advisory clearing), STOP: revert P1-T2/P1-T3, write `docs/features/active/sqlite-advisory-nu1903-92/evidence/other/option-a-fallback-stop.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` describing the infeasibility and surfacing Option A (add a direct `SQLitePCLRaw.bundle_e_sqlite3` PackageReference at a patched version to both csproj), and halt for human decision. Do not switch strategies silently. If restore and build succeed, record success and continue.
  - Acceptance: either (a) restore+build succeed and the artifact records `EXIT_CODE: 0` with a continue note, or (b) the bump is reverted and the stop artifact surfaces Option A without applying it. Supports AC-1, AC-2, and the documented fallback.
  - **EXECUTION HALT (2026-07-01T19-46):** restore+build succeeded (EXIT 0) BUT the resolved target (MDS 9.0.0 -> SQLitePCLRaw.lib.e_sqlite3 2.1.10) does NOT clear GHSA-2m69-gcr7-jv3q — NU1903 still fires at 2.1.10 and 2.1.11 (verified across MDS 9.0.x and 10.0.x). This invalidates the plan/issue premise ("SQLitePCLRaw >= 2.1.10 clears the advisory"). Neither clean branch (a) nor a pure Option-A infeasibility (b) applies. Bump reverted to baseline 8.0.11; stop-and-surface artifact written at `evidence/other/option-a-fallback-stop.md`. Plan is BLOCKED pending premise/AC revision. Phases 2 and 3 NOT executed.

### Phase 2 — Targeted Verification & Final QC Loop

- [ ] [P2-T1] Targeted verification of NU1903 clearance: run `dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror` and write `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/targeted-nu1903-cleared.md` with `Timestamp:`, `Command: dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror`, `EXIT_CODE:` (expected 0), and `Output Summary:` confirming 0 `NU1903` and no new `NUxxxx` advisory. This command must execute; `SKIPPED` is not a valid outcome.
  - Acceptance: artifact records `EXIT_CODE: 0` and `Output Summary:` states 0 `NU1903` and no new `NUxxxx`. Supports AC-2.

- [ ] [P2-T2] Final QC — Formatting: run `csharpier format .` then `csharpier check .`; write `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/final-csharpier.md` with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, and `Output Summary:`. If `csharpier format .` changes any file, restart the QC loop from this task.
  - Acceptance: `csharpier check .` reports `EXIT_CODE: 0` with no unformatted files. Supports AC-6.

- [ ] [P2-T3] Final QC — Lint/analyzers and nullable/type-check: run `dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror` (build serves as the analyzer + nullable gate under warnings-as-errors); write `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/final-build-analyzers.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (0 analyzer warnings, 0 nullable warnings, 0 `NUxxxx`). If this step changes files, restart the QC loop from P2-T2.
  - Acceptance: artifact records `EXIT_CODE: 0` with 0 analyzer/nullable/advisory warnings. Supports AC-2, AC-6.

- [ ] [P2-T4] Final QC — Architecture-boundary verification: verify the `ProjectReference` graph in the two edited csproj is unchanged and still conforms to `.claude/rules/architecture-boundaries.md` (the SQLite bump adds no new project reference and pulls no Outlook COM into `OpenClaw.Core`). Write `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/final-architecture.md` with `Timestamp:`, `Command:` (e.g. `git diff` on `ProjectReference` lines plus dependency-closure check), `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: artifact confirms 0 architecture-boundary violations and no new project references introduced by the bump. Supports AC-4, AC-6.

- [ ] [P2-T5] Final QC — Tests with coverage: run `dotnet test OpenClaw.MailBridge.sln -c Release --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; write `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/final-test-coverage.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric post-change line coverage %, branch coverage %, and passed/failed test counts (unit, architecture-boundary, contract/schema, integration as applicable). This command must execute; `SKIPPED` is not valid.
  - Acceptance: `EXIT_CODE: 0`; all tests pass; line coverage `>= 85%` and branch coverage `>= 75%` recorded numerically. Supports AC-4.

- [ ] [P2-T6] Coverage delta / no-changed-line-regression verification: compare baseline coverage (P0-T5 or the deferral note) against post-change coverage (P2-T5) and confirm no regression on changed lines. Write `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/coverage-delta.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` reporting baseline line%/branch%, post-change line%/branch%, and changed-line coverage. If required numeric coverage is unavailable, the outcome is remediation-required, not PASS.
  - Acceptance: artifact reports baseline, post-change, and changed-line coverage with no regression on the edited lines. Supports AC-4.

### Phase 3 — End-State Evidence & Status

- [ ] [P3-T1] Write the end-state evidence artifact `docs/features/active/sqlite-advisory-nu1903-92/evidence/other/end-state.md` summarizing: the resolved `Microsoft.Data.Sqlite` version (both csproj, in lockstep), the transitive `SQLitePCLRaw.lib.e_sqlite3` version (`>= 2.1.10`), the cleared NU1903 build (`EXIT_CODE: 0`), no suppression introduced, and the final coverage numbers. Include `Timestamp:` and a mapping table from AC-1..AC-6 to the artifact paths that satisfy each.
  - Acceptance: artifact exists, maps every AC-1..AC-6 to a concrete evidence path, and records the resolved version and cleared-advisory state. Supports AC-1..AC-6.

- [ ] [P3-T2] Update the issue.md evidence checklist and mirror: mark `baseline`, `targeted verification`, and `end-state` complete in `docs/features/active/sqlite-advisory-nu1903-92/issue.md`, and write the mirror `docs/features/active/sqlite-advisory-nu1903-92/evidence/issue-updates/issue-92.2026-07-01T19-46.md` with `Timestamp:`, the exact updated text, and `PostedAs: body` (or `PostedAs: unknown` if not posted to GitHub).
  - Acceptance: issue.md Evidence Checklist boxes are checked and the issue-update mirror exists with required fields. Supports AC-6.
