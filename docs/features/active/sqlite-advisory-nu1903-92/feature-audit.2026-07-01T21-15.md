# Feature Audit: SQLitePCLRaw 3.x override to clear NU1903 (#92)

**Audit Date:** 2026-07-01
**Feature Folder:** `docs/features/active/sqlite-advisory-nu1903-92`
**Base Branch:** `main`
**Head Branch:** `fix/sqlite-advisory-nu1903`
**Work Mode:** `minor-audit`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `1f3bb41`)
- **Head branch/commit:** `fix/sqlite-advisory-nu1903` (commit `d4161c1`)
- **Merge base:** `1f3bb419cba0576a10944de842108283a1b88d43`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt` (regenerated during this review)
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt` (regenerated during this review)
  - Feature evidence: `docs/features/active/sqlite-advisory-nu1903-92/evidence/**`
  - Reviewer-reproduced: `dotnet build ... -warnaserror`, `dotnet list ... --include-transitive`, `csharpier check .`
- **Feature folder used:** `docs/features/active/sqlite-advisory-nu1903-92`
- **Requirements source:** `issue.md` (## Acceptance Criteria, AC-1..AC-7)
- **Work mode resolution note:** `issue.md` carries the explicit marker `- Work Mode: minor-audit`; the AC source is the single `## Acceptance Criteria` section in `issue.md`.
- **Scope note:** The PR-context artifacts were stale (they referenced `feature/env-driven-publish-versioning @ a231284`) and were regenerated against the resolved base `main @ 1f3bb41` and current head `d4161c1`. The audit scope is the full branch diff; the only non-doc changes are two `.csproj` files.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `docs/features/active/sqlite-advisory-nu1903-92/issue.md` — only source (minor-audit)

### Acceptance criteria

1. AC-1 A direct SQLitePCLRaw reference (`SQLitePCLRaw.bundle_e_sqlite3` 3.x, plus any companion package required for a coherent graph) is added identically to both `src/OpenClaw.Core/OpenClaw.Core.csproj` and `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`, restoring the transitive `SQLitePCLRaw.lib.e_sqlite3` to the 3.x line (>= 3.50.3).
2. AC-2 `dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror` completes with 0 `NU1903` errors and introduces no new package advisories (no new `NUxxxx`).
3. AC-3 No advisory suppression is introduced (`NoWarn`/`NuGetAuditMode`/`NuGetAuditSuppress` unchanged).
4. AC-4 `dotnet test OpenClaw.MailBridge.sln -c Release` passes, INCLUDING the SQLite-backed cache/DB tests that exercise the native `e_sqlite3` provider at runtime; line coverage >= 85% and branch coverage >= 75%; no regression on changed lines.
5. AC-5 No product-code behavior change, or — if the override forces one — it is minimal, justified, and test-covered.
6. AC-6 Full C# toolchain passes in a single clean pass: CSharpier format -> analyzers/build (`/warnaserror`) -> nullable -> architecture tests -> MSTest tests.
7. AC-7 HARD GATE: if no coherent restore graph exists, or the SQLite-backed tests fail at runtime due to the SQLitePCLRaw 3.x / Microsoft.Data.Sqlite mismatch, the work STOPS and is surfaced to the operator (no forcing, no silent fallback to suppression).

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-1 | Identical direct SQLitePCLRaw 3.x ref in both csproj; transitive `lib.e_sqlite3` >= 3.50.3 | PASS | Both csproj add the identical `SQLitePCLRaw.bundle_e_sqlite3` 3.0.0 line (reviewer `git diff`). Transitive `lib.e_sqlite3` resolves to 3.50.3 in both projects (reviewer-reproduced). | `git diff main...HEAD -- src/**/*.csproj`; `dotnet list <proj> package --include-transitive` | No companion package or MDS bump was needed; MDS unchanged at 8.0.11. |
| AC-2 | Build `-c Release /warnaserror`: 0 NU1903, no new NUxxxx | PASS | Reviewer reproduced: `dotnet build OpenClaw.MailBridge.sln -c Release -warnaserror` -> Build succeeded, 0 Warning(s), 0 Error(s). Executor: `targeted-nu1903-cleared` and `final-build-analyzers` record 0 NU1903, 0 NUxxxx. | `dotnet build OpenClaw.MailBridge.sln -c Release -warnaserror` | `/warnaserror` and `-warnaserror` are the same MSBuild switch; `-` form used to avoid MSYS path rewriting. |
| AC-3 | No advisory suppression introduced | PASS | Reviewer grep of full diff for `NoWarn`/`NuGetAuditMode`/`NuGetAuditSuppress`/`NU1903` matched only doc/plan files, never a build file. Executor: `no-suppression-check.2026-07-01T20-30.md` (NO_SUPPRESSION_MATCHES). | `git diff main...HEAD | grep -iE "NoWarn|NuGetAuditMode|NuGetAuditSuppress|NU1903"` | Confirmed suppression tokens exist only in documentation describing the advisory. |
| AC-4 | Tests pass incl. SQLite runtime; line >= 85% / branch >= 75%; no changed-line regression | PASS | Full suite 587 pass / 5 skip / 0 fail; pooled line 90.73% / branch 79.31%; delta 0.00 pp. SQLite-backed subset: Core 14/14, MailBridge 18/18; native e_sqlite3 3.50.3 loaded at runtime with 0 DllNotFoundException / 0 provider-init / 0 core-mismatch. | `dotnet test OpenClaw.MailBridge.sln -c Release --collect:"XPlat Code Coverage"` (executor evidence) | Coverage cited from executor cobertura evidence per the evidence-verification model; reviewer independently reproduced build + resolution. No changed product-code lines to regress. |
| AC-5 | No product-code behavior change (or minimal/justified/test-covered) | PASS | No .cs file changed anywhere in the diff; the only non-doc changes are two `.csproj` package lines. | `git diff main...HEAD --name-only | grep -vE "^docs/"` -> only the two csproj | The override forced no product-code adjustment. |
| AC-6 | Full C# toolchain single clean pass | PASS | CSharpier check exit 0 (reviewer-reproduced, Checked 193 files); analyzers/nullable via `-warnaserror` 0 diagnostics (reviewer-reproduced); architecture 0 violations (`final-architecture`); MSTest 587 pass. No QC-loop restart. | `csharpier check .`; `dotnet build ... -warnaserror`; executor architecture/test evidence | CSharpier changed no files, so a single clean pass held. |
| AC-7 | HARD GATE: coherent restore + runtime pass, else STOP (no forcing, no suppression) | PASS | Restore succeeded for all 9 projects, no NU1605/NU1107 (`ac7-restore-graph-coherence`). Runtime mismatch check passed: SQLite-backed tests green, native provider loaded (`ac7-runtime-mismatch`, `ac4-runtime-sqlite-provider`). No forcing, no suppression fallback. | `dotnet restore OpenClaw.MailBridge.sln`; SQLite-backed filtered test runs | The gate condition (stop-if-broken) did not fire because both restore and runtime succeeded. |

---

## Summary

**Overall Feature Readiness:** PASS

**Criteria summary:**
- **PASS:** 7 criteria
- **PARTIAL:** 0 criteria
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing PASS:**

1. None.

**Recommended follow-up verification steps:**

1. On any future `Microsoft.Data.Sqlite` upgrade, re-run the restore-graph and SQLite-backed runtime gates, because the SQLitePCLRaw 3.x / MDS 8.0.11 pairing is an unsupported combination.
2. After merge to `main`, rebase PR #91 (`chore/update-agents`) onto `main` to inherit this fix, as noted in issue.md.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- All 7 criteria evaluate PASS.
- The `issue.md` acceptance criteria are authored as prose bullets (`- AC-1 ...`), not markdown checkbox items (`- [ ]`). Per the tracking rules, non-checkbox source items are not reformatted; status is recorded in this audit only. No checkbox change was made to `issue.md`.
- The separate `## Evidence Checklist` in `issue.md` (baseline / targeted verification / end-state) was already checked `[x]` by the executor and is left unchanged.

### AC Status Summary

- Source: `docs/features/active/sqlite-advisory-nu1903-92/issue.md`
- Total AC items: 7
- Checked off (delivered): 0 (source uses prose bullets, not checkboxes; not reformatted)
- Remaining (unchecked): 0 unmet — all 7 PASS, recorded in this audit
- Items remaining: None.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `issue.md` | 7 | 7 (evaluated PASS) | 0 | Prose-only AC bullets; status recorded here, source not rewritten. |
