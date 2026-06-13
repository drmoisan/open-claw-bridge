# Policy Compliance Audit: mailbridge-eventdto-graph-fields (#72)

**Audit Date:** 2026-06-13
**Code Under Test:** Full feature branch diff `open-claw-bridge-wt-2026-06-12-22-12` against base `main` (merge-base `3041d083691cd77b2b2e888580fc9f2ab8bc611f`, head `c92fae9b82adaeebfe7bcab4d4b9783aa0e19ff4`). C# only: 12 source files and 7 test files changed/added; 25 docs/evidence files.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 19 (12 src + 7 test) | 462 total | ✅ 459 pass, 0 fail, 3 skipped | MailBridge 93.22% line / 83.08% branch; Core 89.32% line / 77.58% branch | MailBridge 93.55% line / 85.47% branch; Core 89.09% line / 77.59% branch | New files at 100% line/branch |

**Note:** Python, PowerShell, TypeScript, Bash, JSON: zero changed files on the branch — N/A.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/baseline/baseline-test.md`
- C# post-change coverage artifact: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/qa-gates/final-test.md`
- TypeScript baseline coverage artifact: N/A - out of scope (no TypeScript files changed)
- TypeScript post-change coverage artifact: N/A - out of scope (no TypeScript files changed)
- PowerShell baseline coverage artifact: N/A - out of scope (no PowerShell files changed)
- PowerShell post-change coverage artifact: N/A - out of scope (no PowerShell files changed)
- Per-language comparison summary: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/regression-testing/coverage-delta.md`
- Cobertura corroboration (verified, not re-run): `tests/OpenClaw.Core.Tests/TestResults/4adea53b-.../coverage.cobertura.xml` (line-rate 0.8909, branch-rate 0.7759); `tests/OpenClaw.MailBridge.Tests/TestResults/e9fd124c-.../coverage.cobertura.xml` (line-rate 0.9355, branch-rate 0.8547)

**Non-negotiable verdict rule:** Numeric baseline and post-change coverage are present for the only in-scope language (C#), plus per-changed-file coverage. Satisfied.

**Fail-closed rule:** All required baseline, QA, and coverage-comparison artifacts are present. No INCOMPLETE/BLOCKED trigger from missing artifacts.

**Evidence rule:** All findings below are grounded in inspected diffs, evidence artifacts, and cobertura files. No evidence was synthesized.

---

## Rejected Scope Narrowing

The PR-context summary artifact (`artifacts/pr_context.summary.txt`) classifies the change as:

> "Core logic changes: 0 files ... Docs/templates/agents/tooling: 25 files"

This classification is inaccurate and, if relied upon, would narrow the audit to docs only. Per the SKILL scope invariant, the authoritative scope is the full branch diff against the resolved base branch. The actual `git diff --name-only 3041d08..c92fae9` shows 12 C# source files and 7 C# test files changed in addition to docs. This audit IGNORES the summary's "0 core logic changes" classification and audits the full C# diff. The summary artifact's changed-files classifier under-reported source changes; the diff is authoritative.

No caller (orchestrator or otherwise) supplied a plan/task/file-subset narrowing instruction. The only narrowing risk was the stale classifier output above, which is rejected.

---

## Evidence Location Compliance

Scanned the branch diff for files written under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, or `artifacts/coverage/`:

```
git diff --name-only 3041d08..c92fae9 | grep -E '^artifacts/(baselines|qa|evidence|coverage)/'
-> (no matches) NO_NONCANONICAL_EVIDENCE_PATHS
```

All feature evidence is written under the canonical `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/<kind>/` path (baseline, qa-gates, regression-testing). The `validate_evidence_locations.py` script is not present in this repository; evidence-location compliance was therefore verified by direct diff scan, which is clean. No FAIL-level evidence-location findings.

---

## Executive Summary

The branch adds nine Graph-shaped fields to `EventDto` and wires them through the Outlook COM scanner, the response shaper, both SQLite caches, and the downstream `SchedulingDtoMapper`. The change is additive and source-compatible (new optional positional record parameters appended after `ResponseStatus`). The full C# toolchain (CSharpier format, .NET analyzer/lint build, nullable type-check build, architecture review, MSTest + coverage) is green per the executor's final QA evidence and is corroborated by the cobertura coverage files. Coverage holds above the uniform thresholds (line >= 85%, branch >= 75%) for both modules and every changed/new file.

One pre-existing condition is carried forward: `src/OpenClaw.Core/CoreCacheRepository.cs` remains over the 500-line cap (687 lines), though the feature reduced it from a baseline of 691 and extracted a new `CoreCacheRepository.Schema.cs` partial. `src/OpenClaw.MailBridge/OutlookScanner.cs` was over cap at baseline (507) and is now compliant (485). This is recorded as a non-blocking carried-forward finding rather than a feature-introduced regression.

**Policy documents evaluated:**
- ✅ `.claude/rules/general-code-change.md`
- ✅ `.claude/rules/general-unit-test.md`

**Language-specific policies evaluated:**
- ✅ `.claude/rules/csharp.md`
- ✅ `.claude/rules/architecture-boundaries.md`
- ✅ `.claude/rules/quality-tiers.md`
- N/A Python / PowerShell / Bash / JSON (no changed files)

**Temporary artifacts cleanup:**
- ✅ No temporary/one-time scripts created during this review.
- N/A No tooling scripts authored by this review.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** | ✅ PASS | Each new test builds its own in-memory SQLite connection string with a unique `Guid.NewGuid()` data source (`CacheRepositoryGraphFieldsTests`, `CoreCacheRepositoryGraphFieldsTests`); scanner tests build a fresh `FakeComActiveObject` per test. No shared mutable state. |
| **Isolation** | ✅ PASS | Each `[TestMethod]` targets a single behavior (one round-trip, one shaping mode, one sensitivity mapping). |
| **Fast Execution** | ✅ PASS | 459 tests pass with 3 skipped; suite runs in-process with in-memory SQLite. No network or live Outlook. Per `final-test.md`. |
| **Determinism** | ✅ PASS | Fixed timestamps (`FixedNow`, `FixedTimestamp`), injected clock delegate `() => FixedNow` into `OutlookScanner`, in-memory SQLite, no wall-clock reads. No `Thread.Sleep`/`Task.Delay`/`Start-Sleep` found in new tests. |
| **Readability & Maintainability** | ✅ PASS | Descriptive test names; AAA structure with explicit Arrange/Act/Assert comments; FluentAssertions with rationale strings. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | ✅ PASS | `evidence/baseline/baseline-test.md`; MailBridge 93.22% line / 83.08% branch, Core 89.32% line / 77.58% branch. Captured before edits (P0-T5). |
| **No Coverage Regression (changed lines)** | ✅ PASS | `coverage-delta.md`: every changed/new file at or above 85% line / 75% branch; new files at 100%. MailBridge line +0.33, branch +2.39. Core line -0.23 (89.32→89.09) from added valid lines diluting pre-existing uncovered code elsewhere; both Core figures remain above thresholds and no changed line lost coverage. |
| **New Code Coverage** | ✅ PASS | New files `EventSensitivityLabel.cs`, `OutlookScanner.GraphFields.cs`, `CacheRepository.Schema.cs`, `CoreCacheRepository.Schema.cs` all at 100% line/branch (exceeds the new-file thresholds line >= 85% / branch >= 75% and the SKILL >= 90% new-file trigger). |
| **Comprehensive Coverage** | ✅ PASS | New tests cover the sensitivity mapping, all nine field derivations, safe/enhanced bodyFull shaping, and both-cache round-trip; AC5 recurring-online-meeting path has a dedicated test. |
| **Positive Flows** | ✅ PASS | Populated round-trip, enhanced-mode bodyFull verbatim, recurring online meeting derivations. |
| **Negative Flows** | ✅ PASS | `EventSensitivityLabel.FromSensitivity` null and out-of-range (99) inputs; empty/whitespace categories. |
| **Edge Cases** | ✅ PASS | Empty categories array round-trips as empty; long body exceeding preview cap stays untruncated; seriesMasterId per recurrence state (non-recurring/master/occurrence/exception). |
| **Error Handling** | ✅ PASS | `GetCategories`/`ReadCategories` catch `JsonException` and return null; safe-mode redaction nulls bodyFull. |
| **Concurrency** | N/A | Feature introduces no new concurrency surface. |
| **State Transitions** | ✅ PASS | Idempotent migration verified across two `InitializeAsync` calls (`InitializeAsync_should_be_idempotent_across_two_calls`). |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 93.22% line (MailBridge), 89.32% line (Core) -> Post-change: 93.55% line (MailBridge), 89.09% line (Core). Change: +0.33% line (MailBridge), -0.23% line (Core); branch +2.39% (MailBridge), +0.01% (Core). New/changed-code coverage: 100% (new files); every changed file >= 85% line / 75% branch. Disposition: PASS. Evidence: `evidence/regression-testing/coverage-delta.md`, `evidence/qa-gates/final-test.md`, cobertura files.
  - Numeric baseline (C#): 93.22% line / 83.08% branch (MailBridge), 89.32% line / 77.58% branch (Core). Numeric post-change (C#): 93.55% line / 85.47% branch (MailBridge), 89.09% line / 77.59% branch (Core).

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | ✅ PASS | FluentAssertions with `because` rationale strings (e.g., "safe mode must null bodyFull alongside BodyPreview"). |
| **Arrange-Act-Assert Pattern** | ✅ PASS | Explicit AAA comments in new test methods. |
| **Document Intent** | ✅ PASS | Class-level XML summaries cite the AC they verify (AC2/AC3/AC4/AC5/AC6). |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | ✅ PASS | In-memory shared-cache SQLite; COM doubles (`FakeComActiveObject`, `FakeAppointmentItem`). No real Outlook, network, or processes. |
| **Use Mocks/Stubs** | ✅ PASS | COM boundary substituted by the existing fake-double pattern; `FakeAppointmentItem` extended (as a record) with the new COM analogs. |
| **Environment Stability** | ✅ PASS | No temp files (`Mode=Memory;Cache=Shared`); no mutable global state; deterministic clock seam. Verified by grep: no `GetTempFile`/`GetTempPath`. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | ✅ PASS | This artifact plus `code-review.2026-06-13T03-40.md` and `feature-audit.2026-06-13T03-40.md`. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | ✅ PASS | Issue #72; spec.md and user-story.md document the nine-field objective. |
| **Read existing change plans** | ✅ PASS | `plan.2026-06-12T22-20.md` present; `evidence/baseline/phase0-instructions-read.md` records policy reading. |
| **Document the plan** | ✅ PASS | Atomic plan and phase toolchain evidence under `evidence/qa-gates/`. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | Derivations are small pure switch/split helpers; Body read once and reused for preview and bodyFull. |
| **Reusability** | ✅ PASS | `EventSensitivityLabel.FromSensitivity` mirrors and centralizes the sensitivity vocabulary; `categories` JSON column reuses the existing attendee/resource JSON pattern. |
| **Extensibility** | ✅ PASS | New `EventDto` parameters are optional keyword-defaulted, preserving source compatibility for future callers. |
| **Separation of concerns** | ✅ PASS | Pure derivations in `OutlookScanner.GraphFields.cs`; COM reads isolated to the bridge; persistence DDL/migration split into `*.Schema.cs` partials. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | ✅ PASS | Schema/migration extracted to dedicated partial-class files; mapper changes confined to the mapping method. |
| **Under 500 lines** | ⚠️ PARTIAL | All new and modified files are under 500 except `src/OpenClaw.Core/CoreCacheRepository.cs` at 687 lines. This file was PRE-EXISTING over-cap (691 at baseline) and was reduced by 4 lines plus a new `CoreCacheRepository.Schema.cs` (158) extraction. `OutlookScanner.cs` was pre-existing over-cap (507) and is now compliant (485). `CacheRepository.cs` 465→460 (split prevented crossing the cap). Carried-forward, not feature-introduced; see Section 8. Full counts in Appendix C. |
| **Public vs internal** | ✅ PASS | New `EventSensitivityLabel` is `public static` in the Contracts leaf (appropriately public DTO-adjacent helper); cache repositories remain `internal sealed partial`. |
| **No circular dependencies** | ✅ PASS | No new `ProjectReference` edges (`final-architecture.md`). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | ✅ PASS | `BuildEventDto`, `SplitCategories`, `DeriveSeriesMasterId`, `CategoriesToDbValue`, `GraphFieldColumns`. PascalCase types/members, camelCase locals per csharp.md. |
| **Docs/docstrings** | ✅ PASS | XML doc comments on new public/partial members; `SchedulingDtoMapper`/`SchedulingEventDto` remarks updated to reflect #72 supplying the fields. |
| **Comment why, not what** | ✅ PASS | Comments explain the single-Body-read rationale, safe-mode redaction parity, and OQ-1 recurrence-state handling. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | ✅ PASS | `csharpier check .` EXIT_CODE 0 (`final-format.md`, 2026-06-13T03-26). |
| **2. Linting** | ✅ PASS | `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` EXIT_CODE 0 (`final-lint.md`). |
| **3. Type checking** | ✅ PASS | `dotnet build ... -p:TreatWarningsAsErrors=true` EXIT_CODE 0 (`final-typecheck.md`). |
| **4. Architecture** | ✅ PASS | Project graph and COM confinement verified (`final-architecture.md`); 0 violations. |
| **5. Testing** | ✅ PASS | `dotnet test ... --collect:"XPlat Code Coverage"` EXIT_CODE 0; 459 pass / 0 fail / 3 skipped (`final-test.md`). |
| **Full toolchain loop** | ✅ PASS | Phase 1–6 toolchain evidence plus final gate all green. |
| **Explicit reporting** | ✅ PASS | Commands and exit codes recorded in evidence files and Appendix B. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | ✅ PASS | Commit `c92fae9` "feat(mailbridge): add Graph-shaped event fields to EventDto". |
| **Design choices explained** | ✅ PASS | Locked Design Decisions in spec.md; acceptance-traceability.md maps tasks to ACs. |
| **Update supporting documents** | ✅ PASS | spec.md, user-story.md, plan, and evidence artifacts present. |
| **Provide next steps** | ✅ PASS | See Section 10 Recommendation. |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3E: C# Code Change Policy Compliance

#### 3E.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with CSharpier** | ✅ PASS | `csharpier check .` clean (`final-format.md`). |
| **Linting with .NET analyzers** | ✅ PASS | Analyzer-enforced build clean (`final-lint.md`). |
| **Type checking (nullable)** | ✅ PASS | `TreatWarningsAsErrors=true` build clean (`final-typecheck.md`). |
| **Testing with MSTest** | ✅ PASS | MSTest + coverage green (`final-test.md`). |

#### 3E.2 C# Design & Type Safety

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Strong contracts / explicit APIs** | ✅ PASS | `EventDto` is a `sealed record`; new fields are explicitly typed (`string[]?`, `bool`, `DateTimeOffset?`). |
| **Null-safety by default** | ✅ PASS | Nullable annotations preserved; `Categories ?? Array.Empty<string>()` in the mapper; DB null handled via `DBNull.Value` and `ReadString`. |
| **Composition / focused types** | ✅ PASS | Immutable record DTO; pure derivation helpers; no inheritance added. |
| **Asynchrony / resource safety** | ✅ PASS | `await using` SQLite connections/readers; COM `Body` read once. |

#### 3E.3 COM Interop Confinement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **COM confined to OpenClaw.MailBridge** | ✅ PASS | New COM reads (`IsOnlineMeeting`, `AllowNewTimeProposal`, `RecurrenceState`, `Categories`, `LastModificationTime`, `Body`) via `OutlookComHelpers` in `OutlookScanner.GraphFields.cs` inside `OpenClaw.MailBridge` only. `SchedulingDtoMapper` (Core) and `EventSensitivityLabel` (Contracts) touch no COM. |

#### 3E.4 Error Handling

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Specific exceptions / narrow catch** | ✅ PASS | Only `catch (JsonException)` around category deserialization, returning null — a defined, narrow boundary, not a broad catch. |
| **No ad-hoc console output** | ✅ PASS | No `Console.*` introduced in production paths. |

---

## 4. Language-Specific Unit Test Policy Compliance

### Section 4E: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use MSTest** | ✅ PASS | `[TestClass]`/`[TestMethod]`, `[DataTestMethod]`/`[DataRow]` for sensitivity mapping. |
| **Moq for doubles** | ✅ PASS | Existing fake-double seam reused; Moq available per project; COM double pattern used. |
| **FluentAssertions** | ✅ PASS | `.Should()` assertions throughout new tests. |
| **AAA structure** | ✅ PASS | Explicit Arrange/Act/Assert. |
| **No external deps / no temp files** | ✅ PASS | In-memory SQLite, no temp files (verified by grep). |
| **Determinism (clock/RNG)** | ✅ PASS | Injected clock delegate; fixed timestamps; no banned wait APIs. |
| **Coverage line >= 85% / branch >= 75%** | ✅ PASS | Both modules above thresholds; changed/new files at/above; new files 100%. |

---

## 5. Test Coverage Detail

### EventSensitivityLabel.FromSensitivity (6 cases)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| FromSensitivity mapping for 0/1/2/3 | Positive | ✅ |
| FromSensitivity null input | Negative | ✅ |
| FromSensitivity out-of-range (99) | Edge Case | ✅ |

**Coverage:** 100% line/branch (`EventSensitivityLabel.cs`).

### OutlookScanner Graph-field derivations (OutlookScannerGraphFieldsTests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| categories split / empty | Positive/Edge | ✅ |
| isOrganizer from ResponseStatus | Positive | ✅ |
| isOnlineMeeting from COM | Positive | ✅ |
| allowNewTimeProposals from COM | Positive | ✅ |
| seriesMasterId per recurrence state (non-recurring/master/occurrence/exception) | Edge/State | ✅ |
| lastModifiedDateTime / bodyFull raw | Positive | ✅ |
| recurring online meeting yields expected graph fields (AC5) | Positive | ✅ |

**Coverage:** `OutlookScanner.GraphFields.cs` 100% line/branch; `OutlookScanner.cs` 92.12% line / 88.63% branch.

### ResponseShaper.ShapeEvent (ResponseShaperEventBodyFullTests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| safe mode nulls bodyFull and sets IsRedacted | Error Handling/Redaction | ✅ |
| enhanced mode returns full untruncated body verbatim | Positive/Edge | ✅ |

**Coverage:** `ResponseShaper.cs` 100% line/branch.

### Cache round-trip (CacheRepositoryGraphFieldsTests, CoreCacheRepositoryGraphFieldsTests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| round-trip all nine fields populated | Positive | ✅ |
| round-trip empty categories | Edge Case | ✅ |
| InitializeAsync idempotent across two calls | State Transition | ✅ |

**Coverage:** `CacheRepository.cs` 90.0%/83.82%; `CacheRepository.Readers.cs` 96.1%/83.33%; `CacheRepository.Schema.cs` 100%/100%; `CoreCacheRepository.cs` 97.44%/91.83%; `CoreCacheRepository.Schema.cs` 100%/100%.

### SchedulingDtoMapper.MapEvent (SchedulingDtoMapperTests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| MapEvent maps the nine graph fields through | Positive | ✅ |
| MapEvent recurring online meeting maps expected graph fields | Positive | ✅ |

**Coverage:** `SchedulingDtoMapper.cs` 92.7% line / 80.0% branch.

**Not covered:** None material; all changed lines covered per `coverage-delta.md`.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | 462 | ✅ |
| Tests Passed | 459 (99.4%) | ✅ |
| Tests Failed | 0 | ✅ |
| Tests Skipped | 3 (platform/publish-gated, same as baseline) | ✅ |
| MailBridge coverage | 93.55% line / 85.47% branch | ✅ |
| Core coverage | 89.09% line / 77.59% branch | ✅ |

---

## 7. Code Quality Checks

**For C#:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier Formatting | `csharpier check .` | EXIT_CODE 0, no diffs | ✅ |
| .NET Analyzer Linting | `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` | EXIT_CODE 0 | ✅ |
| Nullable Type Checking | `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` | EXIT_CODE 0 | ✅ |
| MSTest Tests | `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` | EXIT_CODE 0, 459 pass | ✅ |

**Notes:** 3 skipped tests are pre-existing platform/publish-gated tests, identical to baseline. No pre-existing failures introduced by this work.

---

## 8. Gaps and Exceptions

### Identified Gaps

- **Under 500 lines (`CoreCacheRepository.cs`)**: 687 lines, above the 500-line cap. PRE-EXISTING (691 at baseline `evidence/baseline/baseline-file-sizes.md`). The feature reduced it by extracting `CoreCacheRepository.Schema.cs` and did not worsen it. Recommended follow-up: a dedicated refactor to split the remaining read/write methods into additional partials. This is a carried-forward condition, not a feature-introduced regression; classified non-blocking PARTIAL.

### Approved Exceptions

- **None.** The over-cap file is documented in the baseline file-size evidence and is out of scope for #72's additive change; it does not block PR readiness on its own.

### Removed/Skipped Tests

- **None added by this feature.** The 3 skipped tests are pre-existing platform/publish-gated tests (non-Windows COM test and two publish-output tests), identical to baseline.

---

## 9. Summary of Changes

### Commits in This Branch
1. **c92fae9** — feat(mailbridge): add Graph-shaped event fields to EventDto

### Files Modified (C#)
1. `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (MODIFIED) — appended nine optional `EventDto` parameters after `ResponseStatus`.
2. `src/OpenClaw.MailBridge.Contracts/Models/EventSensitivityLabel.cs` (NEW) — int→label mapping helper.
3. `src/OpenClaw.MailBridge/OutlookScanner.cs` (MODIFIED) — delegates DTO construction to `BuildEventDto`.
4. `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (NEW) — COM reads and pure derivations for the nine fields.
5. `src/OpenClaw.MailBridge/ResponseShaper.cs` (MODIFIED) — nulls `BodyFull` in safe mode.
6. `src/OpenClaw.MailBridge/CacheRepository.cs`, `CacheRepository.Readers.cs`, `CacheRepository.Schema.cs` (MODIFIED/NEW) — DDL/migration/write/read for new columns; wired `last_modified_utc`.
7. `src/OpenClaw.Core/CoreCacheRepository.cs`, `CoreCacheRepository.Schema.cs` (MODIFIED/NEW) — mirroring columns, migration helper, reader extension.
8. `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` and `Agent/Contracts/SchedulingEventDto.cs` (MODIFIED) — map new fields through; doc remarks updated.
9. 7 test files (NEW/MODIFIED) under `tests/OpenClaw.MailBridge.Tests` and `tests/OpenClaw.Core.Tests`.

---

## 10. Compliance Verdict

### Overall Status: ⚠️ PARTIALLY COMPLIANT

All gates pass except the carried-forward 500-line cap on the pre-existing `CoreCacheRepository.cs`. No feature-introduced policy violations were found. Coverage, toolchain, architecture, COM confinement, determinism, and source compatibility are all satisfied.

**Fail-closed reminder:** No required baseline, QA, or coverage artifact is missing; the PARTIAL is a pre-existing file-size condition, not a missing-evidence condition.

### Policy-by-Policy Summary

#### General Code Change Policy
- ✅ Before Making Changes
- ✅ Design Principles
- ⚠️ Module & File Structure (pre-existing over-cap file carried forward)
- ✅ Naming, Docs, Comments
- ✅ Toolchain Execution
- ✅ Summarize & Document

#### Language-Specific Code Change Policy (C#)
- ✅ Tooling & Baseline
- ✅ Design & Type Safety
- ✅ COM Confinement
- ✅ Error Handling

#### General Unit Test Policy
- ✅ Core Principles
- ✅ Coverage & Scenarios
- ✅ Test Structure
- ✅ External Dependencies
- ✅ Policy Audit

#### Language-Specific Unit Test Policy (C#)
- ✅ Framework & Scope
- ✅ Test Style & Structure
- ✅ Naming & Readability
- ✅ Toolchain

### Metrics Summary
- ✅ 459/462 tests passing (3 skipped, same as baseline)
- ✅ MailBridge 93.55% line / 85.47% branch; Core 89.09% line / 77.59% branch
- ✅ New files at 100% line/branch
- ✅ 0 architecture violations; COM confined
- ⚠️ One pre-existing over-cap file (`CoreCacheRepository.cs`, 687 lines)

### Recommendation

**Ready for merge (with a tracked follow-up).** The feature is policy-compliant on all gates it controls. The `CoreCacheRepository.cs` 500-line cap is a pre-existing condition the feature improved; recommend opening a follow-up refactor issue to split that file. This is non-blocking for #72.

---

## Appendix A: Test Inventory

New/extended test classes added by this feature:

- `OpenClaw.MailBridge.Tests` › `EventSensitivityLabelTests` (sensitivity mapping)
- `OpenClaw.MailBridge.Tests` › `OutlookScannerGraphFieldsTests` (scanner field population, AC2/AC5)
- `OpenClaw.MailBridge.Tests` › `ResponseShaperEventBodyFullTests` (safe/enhanced bodyFull, AC3)
- `OpenClaw.MailBridge.Tests` › `CacheRepositoryGraphFieldsTests` (bridge cache round-trip, AC4)
- `OpenClaw.MailBridge.Tests` › `MailBridgeRuntimeTestDoubles` (extended `FakeAppointmentItem` with new COM analogs)
- `OpenClaw.Core.Tests` › `CoreCacheRepositoryGraphFieldsTests` (core cache round-trip, AC4)
- `OpenClaw.Core.Tests` › `SchedulingDtoMapperTests` (extended; graph-field mapping, AC5/AC6)

Suite total: 462 tests (459 pass, 0 fail, 3 skipped).

---

## Appendix B: Toolchain Commands Reference

```bash
# Formatting
csharpier check .

# Linting (analyzer-enforced)
dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true

# Type checking (nullable as errors)
dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true

# Testing + coverage
dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"

# Scope / evidence-location verification (this review)
git diff --name-only 3041d083691cd77b2b2e888580fc9f2ab8bc611f..c92fae9b82adaeebfe7bcab4d4b9783aa0e19ff4
git diff --name-only 3041d08..c92fae9 | grep -E '^artifacts/(baselines|qa|evidence|coverage)/'
```

## Appendix C: Changed-File Line Counts (500-line cap)

| File | Lines | Baseline | Status |
|---|---|---|---|
| `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | 166 | 157 | Under cap |
| `src/OpenClaw.MailBridge.Contracts/Models/EventSensitivityLabel.cs` (new) | 41 | — | Under cap |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | 485 | 507 | Now under cap (was pre-existing over-cap) |
| `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` (new) | 104 | — | Under cap |
| `src/OpenClaw.MailBridge/ResponseShaper.cs` | 70 | 65 | Under cap |
| `src/OpenClaw.MailBridge/CacheRepository.cs` | 460 | 465 | Under cap |
| `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` | 116 | 84 | Under cap |
| `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` (new) | 80 | — | Under cap |
| `src/OpenClaw.Core/CoreCacheRepository.cs` | 687 | 691 | OVER CAP (pre-existing; reduced) |
| `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (new) | 158 | — | Under cap |
| `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` | 170 | 169 | Under cap |
| `src/OpenClaw.Core/Agent/Contracts/SchedulingEventDto.cs` | 55 | — | Under cap |
| test files (largest: `MailBridgeRuntimeTestDoubles.cs`) | 418 | — | Under cap |

---

**Audit Completed By:** Feature Review Agent
**Audit Date:** 2026-06-13
**Policy Version:** Current (as of audit date)
