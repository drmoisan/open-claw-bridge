# Policy Compliance Audit: sensitivity-redaction (#18, co-delivers #20)

**Audit Date:** 2026-07-02
**Code Under Test:** C# only. 3 modified production `.cs` files (`src/OpenClaw.MailBridge/OutlookScanner.cs`, `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs`, `src/OpenClaw.MailBridge/ResponseShaper.cs`), 1 new production `.cs` file (`src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs`, 197 lines), 6 new test `.cs` files, and 2 modified test `.cs` files. Plus feature scoping/evidence Markdown, `docs/api-reference.md`, `docs/architecture-diagrams.md`, and two `prd-feature` agent-memory Markdown files. No Python, PowerShell, TypeScript, Bash, or governed JSON files changed in the branch diff.

**Scope:** Full feature branch `feature/sensitivity-redaction-18` @ `d267c663b0ea966609a97dc9e98e9e5ccbdc8cff` versus resolved base `main` @ merge-base `8c969f1a6e96120dd95f835a289c8b185abee202`. Scope is feature-vs-base over the complete branch diff. Diff file breakdown (name-status): 12 `.cs` (4 production, 8 test), 28 `.md` (40 files, +2600/-59). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md`.

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 4 production `.cs` + 8 test `.cs` | 652 (solution) / 339 (MailBridge.Tests) | 647 pass, 0 fail, 5 env-gated skips | 90.26% line, 79.36% branch (pooled solution) | 90.51% line, 79.60% branch (pooled solution) | OutlookScanner.Redaction.cs (NEW) 100% line / **71.43% branch — FAIL (< 75%)**; OutlookScanner.cs 90.73%/90.00%; OutlookScanner.GraphFields.cs 100%/100%; ResponseShaper.cs 100%/100% |

**Note:** Python, PowerShell, Bash, TypeScript, and governed-JSON rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit **FAIL** on the new-code gate (new-file branch coverage 71.43% < 75%), while the pooled, package, and modified-file gates pass.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/sensitivity-redaction-18/evidence/baseline/dotnet-test-coverage.2026-07-02T08-58.md` (pooled 90.26% line / 79.36% branch; `OpenClaw.MailBridge` package 93.08% line / 86.92% branch)
- C# post-change coverage artifact: `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-test-coverage.2026-07-02T09-25.md` (pooled 90.51% line / 79.60% branch; `OpenClaw.MailBridge` package 93.58% line / 87.31% branch)
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head `d267c66`): `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review/<guid>/coverage.cobertura.xml`; independently parsed pooled 90.51% line (4149/4584) / 79.60% branch (925/1162), identical to executor evidence. Reviewer evidence: `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review.2026-07-02T09-45.md`
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura. The pooled gates are met (90.51% line >= 85%, 79.60% branch >= 75%) and all modified files pass, but the new production file `OutlookScanner.Redaction.cs` is at 71.43% branch coverage, below the uniform 75% branch threshold that applies to new code files. The C# coverage gate is therefore **FAIL** and remediation is required.

---

## Executive Summary

This feature branch delivers issue #18 (normalization-time sensitivity redaction) and issue #20 (safe-mode field-suppression completion) as one coordinated change. Items with Outlook `Sensitivity` 2 (Private) or 3 (Confidential) are now redacted inside `NormalizeMessage`/`BuildEventDto` before the DTO reaches the SQLite cache: placeholder subject, nulled content/identity/attendee fields, empty categories, `IsRedacted = true`, `ProtectedFieldsAvailable = false`, with scheduling-mechanical fields retained (master §2.4 / `PRIVATE_BUSY_ONLY`). The sensitive path is never-ingest: `Sensitivity` is read before any protected COM member, and access-recording test doubles prove no protected member is touched. Safe-mode shaping in `ResponseShaper` now suppresses the full protected field set (`ToJson`, `CcJson`, `SenderEmailResolved`, `FromEmailAddress` on messages; `Organizer` and emptied `Categories` on events), sets `ProtectedFieldsAvailable = false`, and stops mutating `IsRedacted` in either mode — the conflation fix that makes `IsRedacted` exclusively the sensitivity-redaction signal. The new redaction logic lives in a new partial-class file `OutlookScanner.Redaction.cs` (197 lines), respecting the 500-line cap on `OutlookScanner.cs` (which shrank from 465 to 462 lines).

The mandatory toolchain was independently re-run by the reviewer against branch head `d267c66` and passes:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 202 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** NetArchTest suite in `OpenClaw.Core.Tests` — 2 passed, 0 failed.
- **Tests + coverage:** full solution `dotnet test` with `--collect:"XPlat Code Coverage"` — 647 passed, 0 failed, 5 environment-gated skips (same skips as baseline; +51 tests vs the 596-pass baseline); pooled coverage 90.51% line / 79.60% branch.
- **Regression evidence:** four fail-before artifacts (EXIT 1 each: new normalization tests, new suppression tests, new composition-invariant tests, and the deliberate `IsRedacted` assertion changes, each failing against the pre-change code exactly as planned) are present under `evidence/regression-testing/`.

**One Blocking finding.** The new production file `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` has branch coverage 71.43% (10/14 conditions), below the uniform new-code branch threshold of 75% (quality-tiers.md, uniform gate). The uncovered branches are the `isMeeting == true` arms in `NormalizeSensitiveMessage` and the null-`MessageClass` short-circuit in `IsMeetingItem`: no test scans a sensitive meeting-typed message through normalization. The executor's coverage evidence reported per-file line coverage only (100%), which masked the branch shortfall behind the passing package aggregate (87.31%). One additional Major PARTIAL: the feature adds three new pure functions (`IsSensitive`, `RedactMessage`, `RedactEvent`) on the T2 module `OpenClaw.MailBridge` without property-based tests (T2 gate: >= 1 property test per pure function; CsCheck is the policy-named tool and is not yet referenced anywhere in the repo). Remediation is required; see `remediation-inputs.2026-07-02T09-45.md`.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/csharp.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript / governed JSON (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is production/test C# plus documentation and canonical evidence Markdown. The executor's raw cobertura staging under `artifacts/csharp/{baseline,post-change-final}/` is untracked, gitignored tooling intermediate output (not in the diff); canonical evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`8c969f1`), head SHA (`d267c66`), and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observations (not narrowing instructions, recorded for completeness): (1) the PR-context summary's "Changed files overview" reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only; that categorization is inaccurate — the authoritative `git diff 8c969f1..d267c66` contains 4 production C# files and 8 test C# files. The audit used the authoritative git diff file list. (2) The summary's author-asserted autoclose list contains a malformed token `#ISO-8601` (a parsing artifact) and already-closed issues #71/#72/#73; the verified close candidates for this branch are #18 and #20 only. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 8c969f1a6e96120dd95f835a289c8b185abee202..HEAD | grep -E '^artifacts/'`
- Result: **NONE.** All feature evidence in the diff is written to the canonical `docs/features/active/sensitivity-redaction-18/evidence/<kind>/` locations (baseline, qa-gates, regression-testing, other). No files under `artifacts/` are tracked in the diff at all.
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own evidence was written to the canonical `evidence/qa-gates/` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, and #19 audits); the scan was performed by direct diff inspection.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Each test builds its own fake graph (`FakeOutlookFolder`/`FakeComActiveObject`/`FakeScanStateRepository`) or in-memory SQLite repository with a per-test GUID data source; no shared mutable state. 334/339 MailBridge.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | Pure-transform tests (`OutlookScannerRedactionTests`), scanner-level never-ingest/normalization tests, shaper suppression tests, composition-invariant tests, and cache round-trip tests are separate classes with one behavior per test. |
| **Fast Execution** | PASS | `OpenClaw.MailBridge.Tests` completes 339 tests in ~12 s (reviewer run); all new tests are in-memory fake-COM or in-memory SQLite. |
| **Determinism** | PASS | Fixed clock (`() => FixedNow` constructor seam), fixed `DateTimeOffset` literals, per-test GUID-named in-memory SQLite; no wall-clock reads, sleeps, timers, network, or filesystem. |
| **Readability & Maintainability** | PASS | Descriptive underscore-separated names, AAA structure, FluentAssertions `because` messages (including the never-ingest assertion printing the accessed member list on failure), XML doc summaries on every new class. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline pooled: 90.26% line, 79.36% branch; `OpenClaw.MailBridge` package 93.08% line / 86.92% branch. Source: `evidence/baseline/dotnet-test-coverage.2026-07-02T08-58.md`. |
| **No Coverage Regression** | PASS | Post-change pooled: 90.51% line (+0.25), 79.60% branch (+0.24); MailBridge package 93.59% line (+0.51) / 87.32% branch (+0.40); Core and HostAdapter byte-identical to baseline. Independently confirmed from reviewer cobertura. |
| **New Code Coverage** | **FAIL** | Per-changed-file (reviewer cobertura): NEW file `OutlookScanner.Redaction.cs` 100% line (109/109) but **71.43% branch (10/14) < 75% uniform new-code threshold**. Uncovered: line 63 (3/6 conditions — both `isMeeting == true` ternary arms and the `Attachments` true short-circuit in `NormalizeSensitiveMessage`) and line 170 (1/2 — null/whitespace `MessageClass` short-circuit in `IsMeetingItem`). No test normalizes a sensitive meeting-typed message. Modified files pass: `OutlookScanner.cs` 90.73%/90.00% (changed lines 361-368, 396 covered), `OutlookScanner.GraphFields.cs` 100%/100%, `ResponseShaper.cs` 100%/100%. Evidence: `evidence/qa-gates/coverage-review.2026-07-02T09-45.md`. |
| **Comprehensive Coverage** | PARTIAL | All 19 spec AC groups are exercised for non-meeting messages and events (positive, negative, boundary, log, round-trip, composition). Gap: the sensitive **meeting-message** normalization path (ItemKind "meeting", `MeetingMessageType` retention through the scanner) is untested — the exact source of the branch-coverage FAIL above. |
| **Positive Flows** | PASS | Sensitivity 2 and 3 for both messages and events at transform, scanner, and cache levels; safe and enhanced shaping of full DTOs. |
| **Negative Flows** | PASS | Boundary sensitivities 0, 1, null, -1, 4, 99 produce unredacted DTOs at scanner level (messages and events); already-null protected fields shape without error in both modes. |
| **Edge Cases** | PASS | Out-of-range sensitivity values, null sensitivity, empty-categories invariant (never null), redacted-DTO re-shaping no-op, `IsRedacted` preserve-true/preserve-false in both modes. |
| **Error Handling** | PASS | No new production exception paths (redaction is a pure integer comparison); already-null shaping asserted `NotThrow`; fail-soft COM reads unchanged for non-sensitive items. |
| **Concurrency** | N/A | No new concurrency surface; pure transforms and existing async cache methods. |
| **State Transitions** | PASS | Cache write-then-read round-trip asserted for both item kinds via real `CacheRepository` (in-memory SQLite), proving redaction persists at write time without shaping involvement. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 90.26% line, 79.36% branch (pooled solution) -> Post-change: 90.51% line, 79.60% branch. Change: +0.25% line, +0.24% branch. New/changed-code coverage: NEW OutlookScanner.Redaction.cs 100% line / 71.43% branch (FAIL, below 75% new-code branch gate); modified OutlookScanner.cs 90.73%/90.00% with changed lines covered; OutlookScanner.GraphFields.cs 100%/100%; ResponseShaper.cs 100%/100%; test files excluded from measurement per policy. Disposition: FAIL (pooled and modified-file gates pass; new-file branch coverage 71.43% < 75%). Evidence: `evidence/baseline/dotnet-test-coverage.2026-07-02T08-58.md`, `evidence/qa-gates/final-test-coverage.2026-07-02T09-25.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T09-25.md`, reviewer re-run `evidence/qa-gates/coverage-review.2026-07-02T09-45.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions `because` clauses throughout; the never-ingest assertions enumerate the accessed protected members in the failure message; log assertions print the offending log line and protected value. |
| **Arrange-Act-Assert Pattern** | PASS | Explicit AAA structure in all new classes; cache tests carry `// Arrange` / `// Act` / `// Assert` comments. |
| **Document Intent** | PASS | XML class summaries tie each class to its spec group (A/B/C) and issue number; per-test comments cite the AC (e.g., "B1:", "C3:"). |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No live Outlook COM, network, or filesystem; in-memory SQLite (`Mode=Memory;Cache=Shared`) for cache tests; no temporary files. |
| **Use Mocks/Stubs** | PASS | New access-recording doubles (`SensitivityRedactionTestDoubles.cs`) modeled on the established `MailBridgeRuntimeTestDoubles.cs` reflection-readable fakes; capturing `ILogger` double for log assertions. |
| **Environment Stability** | PASS | No temp files; no mutable global state; fixed injected clock; per-test GUID-named in-memory databases. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. One Blocking and one Major finding routed to remediation. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #18/#20 bodies, `issue.md`, `spec.md` (with an explicit staleness-reconciliation delta table for post-#71/#72/#73 fields), and `user-story.md` define the defect and fix precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read and the five feature documents read. |
| **Document the plan** | PASS | `plan.2026-07-02T08-36.md` present with all tasks checked and per-phase evidence under `evidence/**`. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | Redaction is a pure `with`-expression transform gated by one integer comparison; shaping changes are additive nulls in existing branches; no new abstractions or configuration. |
| **Reusability** | PASS | `RedactMessage`/`RedactEvent` are the single source of truth for the redacted disposition, applied by both the scanner paths and reused directly by tests; `IsMeetingItem` moved (not duplicated) from `OutlookScanner.cs` to the new partial. Residual duplication of the mechanical-field construction between the sensitive and unredacted builders is a documented Minor code-review finding (deliberate never-ingest trade-off). |
| **Extensibility** | PASS | `IsSensitive(int? sensitivity)` is the single extension point for sensitivity semantics; no public API change; record `with` semantics keep field additions safe. |
| **Separation of concerns** | PASS | Pure transforms (`IsSensitive`, `RedactMessage`, `RedactEvent`) are COM-free; COM reads remain in the scanner's mechanical-member helpers; shaping remains read-time-only in `ResponseShaper`. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Redaction logic isolated in the new `OutlookScanner.Redaction.cs` partial; shaping changes confined to `ResponseShaper.cs`. |
| **Under 500 lines** | PASS | `wc -l` (reviewer): OutlookScanner.cs 462 (down from 465), OutlookScanner.Redaction.cs 197, OutlookScanner.GraphFields.cs 132, ResponseShaper.cs 83; new test files 135-364 lines each. All changed files under the 500-line cap. (Two pre-existing test files over 500 lines — `HelpersTests.cs` 557, `MailBridgeRuntimeTests.OutlookScanner.cs` 544 — are untouched by this branch.) Executor evidence: `evidence/qa-gates/file-size-check.2026-07-02T09-25.md`. |
| **Public vs internal** | PASS | New members are `internal` (test-visible via existing InternalsVisibleTo) or `private`; no public API surface change; DTO shapes unchanged. |
| **No circular dependencies** | PASS | No project-reference changes; NetArchTest boundary suite passes (2/2, reviewer run). |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `IsSensitive`, `RedactMessage`, `RedactEvent`, `NormalizeSensitiveMessage`, `BuildSensitiveEventDto`, `LogRedaction`, `RedactedMessageSubject`/`RedactedEventSubject` constants — all literal. |
| **Docs/docstrings** | PASS | XML docs on every new member, citing master §2.4, the spec group, and the never-ingest rationale; `ResponseShaper.ShapeEvent` carries an updated why-comment explaining the `IsRedacted`/`ProtectedFieldsAvailable` signal split and the Location-retained decision. |
| **Comment why, not what** | PASS | Never-ingest ordering comments at both `Sensitivity`-first read sites explain the §2.4 "does not ingest" requirement; deliberate-assertion-change comments in modified tests explain the conflation fix. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 202 files, EXIT 0. Executor: `evidence/qa-gates/final-format.2026-07-02T09-25.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. |
| **4. Architecture** | PASS | NetArchTest boundary tests: 2 passed, 0 failed (reviewer run). COM access remains confined to `OpenClaw.MailBridge`; the new redaction members are pure and accept no COM objects. |
| **5. Testing** | PASS | Reviewer: full solution test run — 647 passed, 0 failed, 5 environment-gated skips (identical to baseline skips; +51 tests vs baseline). |
| **6. Contract/schema checks** | N/A | No wire-shape change: field names/types unchanged; `BridgeContractsCoverageTests` pass unchanged. The behavioral semantics change (`is_redacted`/`protected_fields_available`) is documented in `docs/api-reference.md` and the change description. |
| **7. Integration tests** | PASS | Cache round-trip tests against real `CacheRepository` (in-memory SQLite) cover the write-then-read integration path; no external-system boundary changed. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass loop (`final-single-pass.2026-07-02T09-25.md`). |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in `evidence/qa-gates/coverage-review.2026-07-02T09-45.md`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `evidence/other/change-description.2026-07-02T09-24.md` describes the change including the two deliberate breaking behavioral changes and the stale-cache deployment note. |
| **Design choices explained** | PASS | Spec records the Location-retained decision, the empty-array categories invariant, the never-ingest ordering, and the partial-class placement rationale (500-line cap). |
| **Update supporting documents** | PASS | `docs/api-reference.md` and `docs/architecture-diagrams.md` updated to the new `isRedacted`/`protectedFieldsAvailable` semantics and safe-mode suppression breadth; `evidence/other/docs-review.2026-07-02T09-24.md` records the doc scan. |
| **Provide next steps** | PASS | Deployment note (cache flush / re-scan recommendation for stale unredacted rows) recorded in spec Data & State and the change description. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, TypeScript, and governed-JSON sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70, #80, and #19 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; new code models optionality with `int?`/`string?`; the single test `!` (`loaded!.Subject`) follows a `Should().NotBeNull()` guard. |
| **Null-safety** | PASS | `IsSensitive(int? sensitivity)` handles null explicitly via pattern matching (`is 2 or 3`); redacted constructions assign explicit nulls; no unchecked dereference added. |
| **Async / resource safety** | PASS | Cache tests `await` all async calls and dispose the repository via `using`; no fire-and-forget, no blocking waits. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces in all new files; PascalCase/`_camelCase` conventions maintained; internal constants PascalCase. |
| **Exceptions fail-fast** | PASS | No new catch blocks in production; the redaction decision is a pure comparison with no exception paths; test doubles throw `InvalidOperationException` on protected access when configured. |
| **No new suppressions** | PASS | Diff scan for pragma/SuppressMessage/nullable-disable additions over all changed C-sharp files returned zero matches (reviewer grep over the full branch diff). |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions (all existing suites use MSTest; divergence recorded in `.claude/agent-memory/prd-feature/project_test_framework_discrepancy.md` and acknowledged in this feature's spec Constraints). The new tests follow the established repo convention, consistent with the prior validated #70, #80, and #19 audits. Pre-existing repo-wide divergence, not a finding against this branch.

Note on banned APIs: no `DateTime.Now`/`DateTime.UtcNow`, `Random.Shared`, `Thread.Sleep`, or `Task.Delay` in the diff (reviewer grep); the clock is the injected `() => FixedNow` seam already used by `OutlookScanner` (`_utcNow`), the repo's established pre-`TimeProvider` seam for this class.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataRow]`; FluentAssertions with `because` messages throughout all six new/two modified test files. |
| **Test file location** | PASS | All new tests live in `tests/OpenClaw.MailBridge.Tests/`, mirroring `src/OpenClaw.MailBridge/`; no colocation in the production tree. |
| **Coverage expectation** | **FAIL** | Pooled 90.51% line / 79.60% branch and all modified files pass, but NEW `OutlookScanner.Redaction.cs` is 100% line / 71.43% branch — below the uniform 75% branch gate for new code. See Section 1.2 and `evidence/qa-gates/coverage-review.2026-07-02T09-45.md`. |
| **Property-based tests (T2 density)** | **PARTIAL** | `OpenClaw.MailBridge` is T2; the T2 gate is >= 1 property test per pure function. This feature adds three new pure functions (`IsSensitive`, `RedactMessage`, `RedactEvent`) with no property-based tests. CsCheck (the policy-named tool, `.claude/rules/csharp.md`) is not referenced anywhere in the repository — a repo-wide pre-existing absence, but the per-pure-function obligation attaches to the new functions added by this branch. The existing example-based tests exhaustively pin the full field disposition and the 8-value boundary partition of `IsSensitive`, which mitigates but does not satisfy the gate. Routed to remediation (Major, non-Blocking given the exhaustive example coverage and the repo-wide precedent that no CsCheck harness exists yet). |
| **Mutation testing** | N/A | Mutation gates apply to T1 modules and run in pre-merge/nightly pipelines per policy, not the per-commit loop; `OpenClaw.MailBridge` is T2 (trend-only). |
| **Determinism (no sleeps, no wall clock)** | PASS | Fixed clock seam and fixed literals; no `Thread.Sleep`/`Task.Delay`/`DateTime.Now`; no timers. |
| **No temporary files** | PASS | In-memory shared-cache SQLite with per-test GUID names; access-recording doubles are pure in-memory objects; zero filesystem artifacts. |
| **Focused / isolated** | PASS | Fresh fake graph or repository per test; no cross-test state; log-capture double is per-test. |

---

## 5. Test Coverage Detail

### New and modified test classes (51 net-new tests vs baseline)

| Test Class | Scope | Scenario Types | Status |
|-----------|--------------|--------|--------|
| `OutlookScannerRedactionTests` (new, 16 tests) | Pure transforms | `IsSensitive` 2/3 true and 0/1/null/-1/4/99 false; full message/event redaction disposition; full mechanical-field retention | PASS |
| `OutlookScannerSensitivityNormalizationTests` (new, ~22 tests) | Scanner + fake COM | Redacted normalization (2/3, message + event); mechanical retention incl. `SensitivityLabel` private/confidential; never-ingest protected-member assertions; 6-value boundary partition unredacted (message + event); bridge-id-only Information-level log assertions with protected-content scan | PASS |
| `CacheRepositorySensitivityRedactionTests` (new, 2 tests) | Cache round-trip (real in-memory SQLite) | Sensitivity=2 message and Sensitivity=3 event upserted via scan, read back redacted with no shaping in the path | PASS |
| `ResponseShaperSafeModeSuppressionTests` (new, 6 tests) | Shaper | B1-B6: full suppression set, mechanical retention (incl. Location retained), enhanced pass-through with preview sanitization and verbatim BodyFull, already-null no-throw | PASS |
| `ResponseShaperCompositionInvariantTests` (new, 5 tests) | Shaper x redaction | C1-C4: redaction survives enhanced shaping (message + event); safe-mode re-shaping no-op keeps `IsRedacted`; shapers never mutate `IsRedacted` either direction; `ProtectedFieldsAvailable` false on both paths | PASS |
| `ResponseShaperTests` (modified) | Shaper | Deliberate `IsRedacted` assertion inversions with why-comments; extended safe-mode suppression assertions; DTO builder enriched with recipient/resolved-sender values | PASS |
| `ResponseShaperEventBodyFullTests` (modified) | Shaper | Deliberate `IsRedacted` assertion inversions with why-comments; renames reflect preserve semantics | PASS |

**Coverage:** `OutlookScanner.Redaction.cs` (NEW) 100% line (109/109) / **71.43% branch (10/14) — FAIL**; `OutlookScanner.cs` 90.73% line / 90.00% branch (changed lines 361-368, 396 covered; 14 uncovered lines pre-existing and untouched); `OutlookScanner.GraphFields.cs` 100%/100%; `ResponseShaper.cs` 100%/100%. **Gap:** no test normalizes a sensitive meeting-typed message (isMeeting=true arms on Redaction.cs line 63; null-`MessageClass` short-circuit on line 170), so `ItemKind = "meeting"` and `MeetingMessageType` retention are verified only at the pure-transform level, not through the scanner.

**Fail-before evidence:** four artifacts under `evidence/regression-testing/` (EXIT 1 each, 2026-07-02T09-08 through T09-20): `redaction-normalization-fail-before` (new normalization tests fail pre-change), `shaper-suppression-fail-before` (new suppression tests fail pre-change), `shaper-assertion-change-fail-before` (modified `ResponseShaperTests`/`ResponseShaperEventBodyFullTests` assertions fail pre-change), `composition-invariants-fail-before` (invariant tests fail pre-change). Pass-after is the full-suite EXIT 0 run (`final-test-coverage.2026-07-02T09-25.md`, reviewer-confirmed).

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 652 (647 passed, 5 env-gated skips) | PASS |
| OpenClaw.MailBridge.Tests | 334 passed / 339 (baseline 283 passed + 51 new; same 5 skips) | PASS |
| Tests Failed | 0 | PASS |
| MailBridge.Tests Execution Time | ~12 s | PASS |
| Pooled Code Coverage | 90.51% line, 79.60% branch | PASS |
| `OpenClaw.MailBridge` package coverage | 93.59% line, 87.32% branch | PASS |
| New-file coverage (`OutlookScanner.Redaction.cs`) | 100% line, 71.43% branch | **FAIL** |
| Net new tests vs baseline | +51 | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 202 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest) | `dotnet test tests/OpenClaw.Core.Tests/... --filter "FullyQualifiedName~ArchitectureBoundary"` | 2 passed, 0 failed | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` | 647 passed, 0 failed, 5 skipped | PASS |
| Per-file coverage re-measure | reviewer cobertura parse (line + branch per changed file) | New file below branch gate (71.43% < 75%) | **FAIL** |

**Notes:** The reviewer re-ran the full toolchain against branch head `d267c66` on 2026-07-02. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

1. **Blocking — new-file branch coverage below the uniform gate.** `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` (new production file) is at 71.43% branch coverage (10/14 conditions), below the 75% branch threshold that quality-tiers.md applies uniformly to new code. Root cause: every sensitive-message normalization test uses a non-meeting `IPM.Note` double, leaving the `isMeeting == true` ternary arms (line 63) and the null-`MessageClass` short-circuit (line 170) uncovered — i.e., a Private/Confidential **meeting message** is never scanned in tests. The executor's `coverage-comparison.2026-07-02T09-25.md` measured per-file line coverage only, which masked the shortfall behind the passing package branch aggregate. Remediation: add sensitive meeting-message normalization tests (meeting-typed and/or `IPM.Schedule.Meeting.Request` double, plus a null-`MessageClass` case). See `remediation-inputs.2026-07-02T09-45.md`.

2. **Major (PARTIAL) — T2 property-test density.** Three new pure functions (`IsSensitive`, `RedactMessage`, `RedactEvent`) on the T2 module lack property-based tests (gate: >= 1 per pure function; tool: CsCheck per `.claude/rules/csharp.md`). CsCheck is absent repo-wide (pre-existing), and the delivered example-based tests exhaustively pin the boundary partition and full field dispositions, so this is graded Major rather than Blocking. Routed to remediation for either delivery (add CsCheck property tests) or a recorded, dated exception consistent with how the repo intends to bootstrap its property-testing harness.

3. **Informational — executor coverage evidence granularity.** Executor per-file coverage evidence should include branch percentages per changed/new file, not line-only, to prevent aggregate masking (this is the second occurrence of this pattern; see the #73 review). Recorded for process improvement; the reviewer re-measure is the enforcing control.

### Approved Exceptions

- **CSharpier invocation path:** the repo has no local dotnet-tool manifest that restores cleanly in this environment; the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70, #80, and #19 audits. The format check ran to EXIT 0 over all 202 files.
- **MCP template/validator tools unavailable:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set (issue #19 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.

### Removed/Skipped Tests

- **None removed.** Two existing test files were modified with **deliberate, spec-enumerated assertion changes** (the `IsRedacted` conflation fix): four assertions inverted from `BeTrue()` to `BeFalse()` with explanatory comments, plus renames and strengthened suppression assertions. These are the exact changes pre-authorized in spec.md "Existing tests whose behavior deliberately changes" and are covered by fail-before evidence (`shaper-assertion-change-fail-before.2026-07-02T09-17.md`). Not assertion-weakening: each inverted assertion is accompanied by new, stronger assertions (`ProtectedFieldsAvailable`, `ToJson`/`CcJson`, `Organizer`) and by the new preserve-true invariant tests.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `8c969f1`)

Branch `feature/sensitivity-redaction-18`, head `d267c663b0ea966609a97dc9e98e9e5ccbdc8cff` (single commit: "feat(mailbridge): per-item sensitivity redaction and complete safe-mode field suppression"). Range: `8c969f1a6e96120dd95f835a289c8b185abee202..d267c663b0ea966609a97dc9e98e9e5ccbdc8cff`.

### Files Modified (categories)

1. **`src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs`** (NEW, 197 lines) — pure redaction members: `IsSensitive`, `RedactMessage`, `RedactEvent`, placeholder-subject constants, `NormalizeSensitiveMessage`, `BuildSensitiveEventDto`, `LogRedaction`, and the relocated `IsMeetingItem`.
2. **`src/OpenClaw.MailBridge/OutlookScanner.cs`** (MODIFIED, +11/-14 net) — `Sensitivity` read hoisted before protected reads in `NormalizeMessage`; sensitive branch delegates to `NormalizeSensitiveMessage`; `IsMeetingItem` moved out.
3. **`src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs`** (MODIFIED) — `Sensitivity` read hoisted before `Body`/attendee/organizer reads in `BuildEventDto`; sensitive branch delegates to `BuildSensitiveEventDto`.
4. **`src/OpenClaw.MailBridge/ResponseShaper.cs`** (MODIFIED) — safe-mode branches gain `SenderEmailResolved`/`FromEmailAddress`/`ToJson`/`CcJson` (message) and `Organizer`/empty `Categories` (event) suppression plus `ProtectedFieldsAvailable = false`; both modes stop assigning `IsRedacted`.
5. **Tests** (6 NEW, 2 MODIFIED) — see Section 5.
6. **`docs/api-reference.md`, `docs/architecture-diagrams.md`** — updated `isRedacted`/`protectedFieldsAvailable` semantics and safe-mode suppression breadth.
7. **`docs/features/active/sensitivity-redaction-18/**`** (NEW, 24 files) — issue/spec/user-story/plan/github-issue mirrors and canonical evidence (baseline, qa-gates, regression-testing, other).
8. **`.claude/agent-memory/prd-feature/**`** — memory index + one project memory (issue-body staleness reconciliation); no code or policy content.

---

## 10. Compliance Verdict

### Overall Status: NOT COMPLIANT — REMEDIATION REQUIRED

The C# change passes formatting, linting, nullable type-checking, architecture-boundary enforcement, the full unit-test suite (647/647 runnable), the pooled coverage gates, and all modified-file coverage gates, all independently re-run by the reviewer at branch head. Fail-before regression evidence is present for all four new/changed test groups. No evidence-location or file-size violations. No new suppressions. The `modified-workflow-needs-green-run` rule does not fire (verified: no `.github/workflows/**`, `scripts/benchmarks/**`, or `.github/actions/**` paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered.

However, the new production file `OutlookScanner.Redaction.cs` is below the uniform new-code branch-coverage gate (71.43% < 75%) because the sensitive meeting-message normalization path is untested — a Blocking finding. A Major PARTIAL on T2 property-test density accompanies it. Both are routed to remediation.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked FAIL because a required per-new-file coverage gate is failing, not because any artifact or metric is missing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (pure transforms, single source of truth for the disposition)
- Module & File Structure: PASS (all changed files under 500 lines; new partial respects the OutlookScanner.cs cap)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS (breaking changes and deployment note documented)

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (no new production error paths; fail-fast preserved)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: **FAIL** (new-file branch coverage 71.43% < 75%; sensitive meeting-message path untested)
- Test Structure: PASS
- External Dependencies: PASS (fake COM doubles, in-memory SQLite, no temp files)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions repo convention; tests/ mirror)
- Determinism: PASS (fixed injected clock, no sleeps)
- T2 obligations: **PARTIAL** (property-test density gate unmet for the three new pure functions; mutation gate trend-only for T2)

---

### Metrics Summary

- 647/647 runnable solution tests passing (5 pre-existing environment-gated skips; +51 tests vs baseline)
- 90.51% pooled line coverage, 79.60% pooled branch coverage (gates: 85%/75%)
- New file: OutlookScanner.Redaction.cs 100% line / 71.43% branch (branch gate: 75%) — **FAIL**
- Modified files: OutlookScanner.cs 90.73%/90.00% (changed lines covered); GraphFields and ResponseShaper 100%/100%
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- Architecture boundary: 2/2 NetArchTest tests pass

---

### Recommendation

**No-Go for PR until remediation completes.** One Blocking finding (new-file branch coverage below the uniform 75% gate; untested sensitive meeting-message normalization path) and one Major PARTIAL (T2 property-test density for the three new pure functions) require a remediation cycle. Both are narrow, test-only additions; no production code change is expected. Remediation inputs: `docs/features/active/sensitivity-redaction-18/remediation-inputs.2026-07-02T09-45.md`.

---

## Appendix A: Test Inventory

C# test files added by this feature:

1. `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionTests.cs` (188 lines) — 16 tests: `IsSensitive` boundary partition (2/3 true; 0/1/null/-1/4/99 false); `RedactMessage`/`RedactEvent` full protected-field disposition and full mechanical-field retention.
2. `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs` (364 lines) — scanner-level redaction (2/3, message + event), mechanical retention including `SensitivityLabel` mapping, never-ingest protected-member assertions via access-recording doubles, 6-value boundary partition (message + event), bridge-id-only Information-level log assertions with protected-content scans.
3. `tests/OpenClaw.MailBridge.Tests/CacheRepositorySensitivityRedactionTests.cs` (135 lines) — Sensitivity=2 message and Sensitivity=3 event scan-write-read round-trips through real `CacheRepository` (in-memory SQLite), asserting fully redacted stored rows without shaping.
4. `tests/OpenClaw.MailBridge.Tests/ResponseShaperSafeModeSuppressionTests.cs` (242 lines) — spec Group B (B1-B6).
5. `tests/OpenClaw.MailBridge.Tests/ResponseShaperCompositionInvariantTests.cs` (176 lines) — spec Group C (C1-C4) composition invariants.
6. `tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs` (190 lines) — access-recording mail/appointment doubles with ordered protected-access logs and optional throw-on-access.

C# test files modified: `ResponseShaperTests.cs` (+29/-9: deliberate `IsRedacted` inversions, extended suppression assertions, enriched DTO builder), `ResponseShaperEventBodyFullTests.cs` (+12/-2: deliberate `IsRedacted` inversions with why-comments, renames).

No test files were removed. Reviewer run: `OpenClaw.MailBridge.Tests` 334 passed, 0 failed, 5 env-gated skipped; solution total 647 passed, 0 failed, 5 skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review"

# Architecture-boundary tests (subset)
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --no-build --filter "FullyQualifiedName~ArchitectureBoundary"

# Regression subsets (fail-before evidence, executor)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerSensitivityNormalizationTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~ResponseShaperSafeModeSuppressionTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~ResponseShaperCompositionInvariantTests"

# Suppression / banned-API scan (branch diff)
git diff 8c969f1a6e96120dd95f835a289c8b185abee202..HEAD -- 'src/*.cs' 'tests/*.cs' | grep -nE '^\+.*(pragma|SuppressMessage|#nullable|Thread\.Sleep|Task\.Delay|DateTime\.Now|Random\.Shared)'

# Evidence-location scan
git diff --name-only 8c969f1a6e96120dd95f835a289c8b185abee202..HEAD | grep -E '^artifacts/'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-02
**Policy Version:** Current (as of audit date)
