# Policy Compliance Audit: SQLitePCLRaw 3.x override to clear NU1903 (issue #92)

**Audit Date:** 2026-07-01
**Code Under Test:**
- `src/OpenClaw.Core/OpenClaw.Core.csproj` (MODIFIED, +1 line)
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` (MODIFIED, +1 line)
- Feature-folder documentation and evidence under `docs/features/active/sqlite-advisory-nu1903-92/**` (docs only)

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 2 files (both .csproj, MSBuild XML) | 587 | ✅ 587 pass, 5 skipped, 0 fail | line 90.73% / branch 79.31% pooled | line 90.73% / branch 79.31% pooled | N/A (no .cs lines changed) |

**Note:** The only non-documentation changes in the branch diff are two `.csproj` files. No .cs, .ps1, .py, or .ts source files changed. There is therefore only one changed application language (C#), and its coverage verdict is explicit PASS (see Section 1.2). No new product-code files or lines were added, so the "new code coverage" cell is N/A by fact, not by scope narrowing.

### Coverage Evidence Checklist

- C# post-change coverage evidence: `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/final-test-coverage.2026-07-01T20-30.md` (pooled line 90.73% / branch 79.31% from three cobertura reports)
- C# baseline coverage evidence: `docs/features/active/sqlite-advisory-nu1903-92/evidence/baseline/baseline-test-coverage.md`
- C# changed-line / no-regression evidence: `docs/features/active/sqlite-advisory-nu1903-92/evidence/qa-gates/coverage-delta.2026-07-01T20-30.md` (delta 0.00 pp line and branch)
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: N/A - out of scope (zero changed TypeScript files on this branch)
- TypeScript post-change coverage artifact: N/A - out of scope (zero changed TypeScript files on this branch)
- PowerShell baseline coverage artifact: N/A - out of scope (zero changed PowerShell files on this branch)
- PowerShell post-change coverage artifact: N/A - out of scope (zero changed PowerShell files on this branch)
- Python coverage artifacts: N/A - out of scope (zero changed Python files on this branch)

**Verdict rules applied:** This audit includes numeric baseline and post-change coverage for the one in-scope language (C#) and confirms no regression on the changed (csproj-only) surface. All cited evidence artifacts exist on disk and were read during this review; no evidence was synthesized.

---

## Executive Summary

The change adds a single direct `PackageReference` to `SQLitePCLRaw.bundle_e_sqlite3` version `3.0.0`, identically, to both `src/OpenClaw.Core/OpenClaw.Core.csproj` and `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`. `Microsoft.Data.Sqlite` remains unchanged at `8.0.11`. The direct reference forces the transitive `SQLitePCLRaw.lib.e_sqlite3` from the advisory-affected 2.1.6 to 3.50.3, which clears NU1903 (GHSA-2m69-gcr7-jv3q). The remainder of the branch diff is feature-folder documentation and evidence artifacts.

The reviewer independently reproduced the C# build gate (`dotnet build OpenClaw.MailBridge.sln -c Release -warnaserror`: build succeeded, 0 Warning(s), 0 Error(s)) and independently confirmed the transitive resolution (`dotnet list ... package --include-transitive`: `SQLitePCLRaw.lib.e_sqlite3` 3.50.3 in both projects). The reviewer also independently reproduced the CSharpier format check (`csharpier check .`: Checked 193 files, exit 0). Test-suite pass/coverage figures are cited from the executor's coverage evidence, which is the required evidence-verification model for this reviewer.

**Policy documents evaluated:**
- ✅ `general-code-change.md` (cross-language code change policy)
- ✅ `general-unit-test.md` (coverage thresholds and no-regression rule)
- ✅ `quality-tiers.md` (uniform line >= 85% / branch >= 75%)
- ✅ `csharp.md` (C# toolchain order — applicable, C# files changed)

**Language-specific policies evaluated:**
- ✅ C#: `csharp.md` (formatting -> analyzers/build -> nullable -> architecture -> MSTest)
- N/A Python / PowerShell / Bash / JSON: no changed files on this branch

**Temporary artifacts cleanup:**
- ✅ No temporary or throwaway scripts were created by this change; the diff is package-reference-only plus documentation.

---

## Rejected Scope Narrowing

None. The caller prompt explicitly instructed the reviewer to determine scope per the scope invariant and not to treat any changed-language toolchain as out of scope. No narrowing instruction was present. The audit scope is the full branch diff `main...HEAD` (`1f3bb41..d4161c1`).

---

## Evidence Location Compliance

The reviewer scanned the branch diff for files written under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, or `artifacts/coverage/`.

- Result: no such files present. All feature evidence is written under the canonical `docs/features/active/sqlite-advisory-nu1903-92/evidence/<kind>/` path.
- Command: `git diff main...HEAD --name-only | grep -iE "artifacts/(baselines|qa|evidence|coverage)/"` returned `NO_EVIDENCE_LOCATION_VIOLATIONS`.
- Verdict: ✅ PASS. No evidence-location violations.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** | ✅ PASS | The change adds no tests and modifies no test code. The existing suite (587 passed, 5 skipped, 0 failed) ran clean post-change per `final-test-coverage.2026-07-01T20-30.md`. No ordering dependency was introduced. |
| **Isolation** | ✅ PASS | No test changes. SQLite-backed tests exercise a single behavior each (open/read/write) per `ac4-runtime-sqlite-provider.2026-07-01T20-30.md`. |
| **Fast Execution** | ✅ PASS | No test additions; suite runtime unaffected by a package-reference-only change. |
| **Determinism** | ✅ PASS | SQLite tests use in-memory shared-cache connections (no temp files) per the AC-4 runtime evidence; no wall-clock or RNG dependency introduced. |
| **Readability & Maintainability** | ✅ PASS | No test code changed. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | ✅ PASS | Baseline pooled line 90.73% / branch 79.31% recorded in `baseline-test-coverage.md` (2026-07-01T19-46) and reused in `coverage-delta.2026-07-01T20-30.md`. |
| **No Coverage Regression** | ✅ PASS | Post-change pooled line 90.73% / branch 79.31%. Delta 0.00 pp on both. The change edits only two `.csproj` files; no product-code lines changed, so there are no changed product-code lines to regress. Evidence: `coverage-delta.2026-07-01T20-30.md`. |
| **New Code Coverage** | N/A (factual) | No new product-code files or lines were added. The diff is two added `<PackageReference>` lines. There is no new code to cover. |
| **Comprehensive Coverage** | ✅ PASS | Pooled line 90.73% >= 85% and branch 79.31% >= 75%. Per-assembly: MailBridge line 93.58% / branch 87.76%; Core line 90.06% / branch 77.70%; HostAdapter line 88.46% / branch 67.19% (branch below 75% on HostAdapter is pre-existing and unaffected by this package-only change; the pooled branch figure clears the threshold and no changed line touches HostAdapter). |
| **Positive / Negative / Edge / Error / Concurrency / State** | ✅ PASS (unchanged) | No behavior changed; existing scenario coverage is unchanged. The runtime provider-load path is positively verified by the SQLite-backed suites (Core 14/14, MailBridge 18/18). |

**C# coverage verdict: PASS.** Pooled line 90.73% >= 85%; pooled branch 79.31% >= 75%; no regression on the changed (csproj-only) surface.

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 90.73% lines -> Post-change: 90.73% lines. Change: +0.00% lines (branch 79.31% -> 79.31%, +0.00%). New/changed-code coverage: N/A - out of scope (no .cs lines changed). Disposition: PASS. Evidence: `evidence/qa-gates/final-test-coverage.2026-07-01T20-30.md`, `evidence/qa-gates/coverage-delta.2026-07-01T20-30.md`.

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | ✅ PASS | SQLite tests use in-memory shared-cache connections; no temp files, no network per `ac4-runtime-sqlite-provider.2026-07-01T20-30.md`. |
| **Environment Stability** | ✅ PASS | No temp-file creation; deterministic in-memory SQLite. |

---

## 2. General Code Change Policy Compliance

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | Minimal change: a single direct `PackageReference` per project. No product-code churn, no build-property gymnastics. |
| **Reusability / Extensibility / Separation of concerns** | ✅ PASS (N/A change) | Dependency-graph change only; no API or layering impact. Architecture evidence confirms no new project edge (`final-architecture.2026-07-01T20-30.md`). |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Under 500 lines** | ✅ PASS | Both csproj files remain small (single-line additions). No file approaches the 500-line cap. |
| **No circular dependencies** | ✅ PASS | No `ProjectReference` added or removed; the compile-time project graph is unchanged (`final-architecture.2026-07-01T20-30.md`). |

### 2.5 After Making Changes - Toolchain Execution (C#)

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | ✅ PASS | `csharpier check .` -> Checked 193 files, exit 0. Independently reproduced by reviewer (exit 0). Executor evidence: `final-csharpier.2026-07-01T20-30.md`. |
| **2. Linting (analyzers)** | ✅ PASS | `dotnet build ... -c Release -warnaserror` -> 0 Warning(s), 0 Error(s); analyzer/CA/CS/IDE count 0. Independently reproduced by reviewer. Executor evidence: `final-build-analyzers.2026-07-01T20-30.md`. |
| **3. Type checking (nullable)** | ✅ PASS | Warnings-as-errors build serves as the nullable gate; 0 diagnostics. Independently reproduced. |
| **4. Architecture-boundary tests** | ✅ PASS | 0 violations; COM confinement preserved; no new project edge. Evidence: `final-architecture.2026-07-01T20-30.md`. |
| **5. Testing (MSTest)** | ✅ PASS (cited) | 587 passed, 5 skipped, 0 failed across 3 assemblies. Evidence: `final-test-coverage.2026-07-01T20-30.md`. |
| **6. Contract / schema** | N/A | No host-service boundary contract changed; dependency-only change. |
| **7. Integration / runtime** | ✅ PASS (cited) | SQLite-backed tests load the native e_sqlite3 3.50.3 provider at runtime: Core 14/14, MailBridge 18/18, 0 DllNotFoundException, 0 provider-init failure, 0 core-mismatch. Evidence: `ac4-runtime-sqlite-provider.2026-07-01T20-30.md`. |
| **Full toolchain loop** | ✅ PASS | CSharpier caused no file changes; single clean pass. Evidence: `final-csharpier.2026-07-01T20-30.md` (no QC-loop restart). |

**Note on `/warnaserror` vs `-warnaserror`:** In Git Bash, `/warnaserror` is rewritten by MSYS path conversion; `-warnaserror` is the identical MSBuild switch. The reviewer used `-warnaserror` and reproduced exit 0.

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Dependencies: approved / minimal** | ✅ PASS | `SQLitePCLRaw` is the native provider family already pulled transitively by `Microsoft.Data.Sqlite`; the change promotes it to a direct reference at 3.x. It is a well-maintained, widely used package. The issue documents why: no `Microsoft.Data.Sqlite` release transitively clears the advisory; only the SQLitePCLRaw 3.x line does. |
| **Lockstep / identical across projects** | ✅ PASS | Both csproj add the exact same `SQLitePCLRaw.bundle_e_sqlite3` `3.0.0` line. Verified by reviewer via `git diff` (identical `+` line in both files). |
| **No advisory suppression** | ✅ PASS | Reviewer grep of the diff for `NoWarn` / `NuGetAuditMode` / `NuGetAuditSuppress` / `NU1903` matched only documentation/plan files describing the advisory, never any build file (csproj/props/targets). Executor evidence: `no-suppression-check.2026-07-01T20-30.md`. |
| **No product-code behavior change** | ✅ PASS | No .cs file changed anywhere in the diff. Evidence: `coverage-delta.2026-07-01T20-30.md`, reviewer `git diff --name-only`. |
| **Restore-graph coherence** | ✅ PASS | `dotnet restore` succeeded for all 9 projects; no NU1605/NU1107. Evidence: `ac7-restore-graph-coherence.2026-07-01T20-30.md`; reviewer's build restored cleanly. |

---

## 4. Language-Specific Unit Test Policy Compliance

No test files were added or modified in this branch. The change is package-reference-only. There are no new or modified C# test files to audit under this section. The existing MSTest suites were executed unchanged and passed (587 pass / 5 skip / 0 fail), and the SQLite-backed subset was used for the AC-4 runtime provider-load verification.

- Python unit tests: N/A — no changed Python files.
- PowerShell unit tests: N/A — no changed PowerShell files.
- C# unit tests: no test-code change; existing MSTest suites pass. Evidence: `evidence/qa-gates/final-test-coverage.2026-07-01T20-30.md`.

---

## 5. Test Coverage Detail

No new or modified production code exists, so there is no per-function coverage delta to detail. Coverage is reported at the assembly level from the post-change cobertura reports (`evidence/qa-gates/final-test-coverage.2026-07-01T20-30.md`):

| Assembly | Line Coverage | Branch Coverage | Status |
|----------|---------------|-----------------|--------|
| OpenClaw.MailBridge.Tests surface | 93.58% (1166/1246) | 87.76% (387/441) | ✅ |
| OpenClaw.Core.Tests surface | 90.06% (1323/1469) | 77.70% (317/408) | ✅ |
| OpenClaw.HostAdapter.Tests surface | 88.46% (997/1127) | 67.19% (170/253) | Pre-existing branch gap; assembly not touched by this change |
| POOLED | 90.73% (3486/3842) | 79.31% (874/1102) | ✅ |

**Not covered by new code:** None — no new code was added.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | 592 (587 executed + 5 skipped) | ✅ |
| Tests Passed | 587 (99.2% of executed) | ✅ |
| Tests Failed | 0 | ✅ |
| Tests Skipped | 5 (pre-existing) | ✅ |
| Assemblies | 3 (HostAdapter.Tests, Core.Tests, MailBridge.Tests) | ✅ |
| Code Coverage | 90.73% lines, 79.31% branches (pooled) | ✅ |
| SQLite-backed runtime subset | Core 14/14, MailBridge 18/18 pass | ✅ |

---

## 7. Code Quality Checks (C#)

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` | Checked 193 files, exit 0 (reviewer-reproduced) | ✅ |
| Analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln -c Release -warnaserror` | 0 Warning(s), 0 Error(s) (reviewer-reproduced) | ✅ |
| NU1903 / advisory | build log NUxxxx scan | 0 NU1903, 0 new NUxxxx | ✅ |
| Transitive resolution | `dotnet list ... package --include-transitive` | `SQLitePCLRaw.lib.e_sqlite3` 3.50.3 in both projects (reviewer-reproduced) | ✅ |
| MSTest | `dotnet test OpenClaw.MailBridge.sln -c Release --collect:"XPlat Code Coverage"` | 587 pass / 5 skip / 0 fail (cited) | ✅ |

**Notes:** HostAdapter branch coverage 67.19% is a pre-existing condition on an assembly this change does not touch; pooled branch coverage (79.31%) clears the >= 75% threshold and there is no changed line in HostAdapter. This is not introduced by issue #92.

---

## 8. Gaps and Exceptions

### Identified Gaps
**None.** All policy requirements applicable to a package-reference-only C# change are met. The one hard runtime gate (unsupported SQLitePCLRaw 3.x + Microsoft.Data.Sqlite 8.0.11 core combination) is substantiated by runtime evidence (AC-4/AC-7): the native provider loads and cache open/read/write paths pass.

### Approved Exceptions
**None.**

### Removed/Skipped Tests
**None.** The 5 skipped tests are pre-existing skips unrelated to this change (baseline was also 587 pass / 5 skip / 0 fail).

---

## 9. Summary of Changes

### Commits in This Branch
1. **d4161c1** — fix(deps): override SQLitePCLRaw to 3.x to clear NU1903 (GHSA-2m69-gcr7-jv3q)

### Files Modified
1. **src/OpenClaw.Core/OpenClaw.Core.csproj** (MODIFIED) — added `<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.0" />`.
2. **src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj** (MODIFIED) — added the identical reference (lockstep).
3. **docs/features/active/sqlite-advisory-nu1903-92/** (NEW) — issue.md, two plan files, 26 evidence artifacts.

---

## 10. Compliance Verdict

### Overall Status: ✅ FULLY COMPLIANT

The change clears NU1903 by forcing `SQLitePCLRaw.lib.e_sqlite3` to 3.50.3 through an identical direct reference in both project files, introduces no advisory suppression, changes no product code, and is substantiated by a reviewer-reproduced clean warnings-as-errors build, reviewer-reproduced transitive resolution, and executor runtime and coverage evidence.

**Fail-closed check:** All required baseline, QA, and coverage-comparison artifacts are present on disk and were inspected. No PASS was recorded on missing evidence.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Design Principles: minimal, simple dependency change
- ✅ Module & File Structure: under 500 lines; no circular deps; no new project edge
- ✅ Toolchain Execution: single clean C# pass (reviewer-reproduced format + build)
- ✅ Summarize & Document: issue.md and plan/evidence documents present

#### Language-Specific Code Change Policy (Section 3 — C#)
- ✅ Dependencies: approved, minimal, lockstep
- ✅ No advisory suppression
- ✅ No product-code behavior change
- ✅ Restore-graph coherence

#### General Unit Test Policy (Section 1)
- ✅ Coverage & Scenarios: pooled line 90.73% >= 85%, branch 79.31% >= 75%, no regression
- ✅ External Dependencies: in-memory SQLite, no temp files

---

### Metrics Summary
- ✅ 587/592 tests passing (5 pre-existing skips), 0 failures
- ✅ Pooled line coverage 90.73%; pooled branch coverage 79.31%
- ✅ 0 NU1903, 0 new NUxxxx (reviewer-reproduced)
- ✅ `SQLitePCLRaw.lib.e_sqlite3` 3.50.3 resolved in both projects (reviewer-reproduced)
- ✅ Identical lockstep reference across both csproj

---

### Recommendation

**Ready for merge.** No blocking or partial findings. The change is a minimal, correctly-scoped dependency override that clears the advisory without suppression and is runtime-verified for the unsupported provider/core combination.

---

## Appendix A: Test Inventory

No tests were added or modified by this change. The relevant runtime-verification subset (used to substantiate AC-4 / AC-7) consists of the SQLite-backed cache/DB test classes exercised at runtime:

- OpenClaw.Core.Tests › CoreCacheRepositoryMessageFieldsTests
- OpenClaw.Core.Tests › CoreCacheRepositoryGraphFieldsTests
- OpenClaw.MailBridge.Tests › CacheRepositoryMessageFieldsTests
- OpenClaw.MailBridge.Tests › CacheRepositoryGraphFieldsTests
- OpenClaw.MailBridge.Tests › CacheRepositoryResponseStatusTests
- OpenClaw.MailBridge.Tests › CacheRepositoryMigrationIdempotencyTests

Full suite: 587 passed / 5 skipped / 0 failed across 3 assemblies. Evidence: `evidence/qa-gates/final-test-coverage.2026-07-01T20-30.md`, `evidence/qa-gates/ac4-runtime-sqlite-provider.2026-07-01T20-30.md`.

---

## Appendix B: Toolchain Commands Reference

```bash
# Format (reviewer-reproduced)
csharpier check .

# Build with warnings-as-errors / advisory gate (reviewer-reproduced)
dotnet build OpenClaw.MailBridge.sln -c Release -warnaserror

# Transitive resolution verification (reviewer-reproduced)
dotnet list src/OpenClaw.Core/OpenClaw.Core.csproj package --include-transitive
dotnet list src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj package --include-transitive

# Suppression scan (reviewer-reproduced)
git diff main...HEAD | grep -iE "NoWarn|NuGetAuditMode|NuGetAuditSuppress|NU1903"

# Tests + coverage (executor evidence; not re-run by reviewer)
dotnet test OpenClaw.MailBridge.sln -c Release --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-01
**Policy Version:** Current (as of audit date)
