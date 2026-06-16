# Policy Compliance Audit: mailbridge-messagedto-resolved-fields (#73)

**Audit Date:** 2026-06-13
**Code Under Test:** C# changes on branch `feature/mailbridge-messagedto-resolved-fields-73` (`9658ee7`) vs base `main` (`be2ddbf`):
- `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (MODIFIED)
- `src/OpenClaw.MailBridge/IMessageSource.cs` (NEW)
- `src/OpenClaw.MailBridge/ComMessageSource.cs` (NEW)
- `src/OpenClaw.MailBridge/OutlookScanner.cs` (MODIFIED)
- `src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs` (MODIFIED)
- `src/OpenClaw.MailBridge/CacheRepository.cs` (MODIFIED)
- `src/OpenClaw.MailBridge/CacheRepository.Schema.cs` (MODIFIED)
- `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` (MODIFIED)
- `src/OpenClaw.Core/CoreCacheRepository.cs` (MODIFIED)
- `src/OpenClaw.Core/CoreCacheRepository.Schema.cs` (MODIFIED)
- `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` (MODIFIED)
- Test files: `ComMessageSourceTests.cs`, `CacheRepositoryMessageFieldsTests.cs`, `OutlookScannerMessageFieldsTests.cs`, `MailBridgeMessageSourceTestDoubles.cs`, `MailBridgeRuntimeTestDoubles.cs`, `CoreCacheRepositoryMessageFieldsTests.cs`, `SchedulingDtoMapperTests.cs`

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 18 .cs files (3 src new, 8 src modified, 7 test) | 530 | ✅ 530 pass, 0 fail, 3 skipped | MailBridge 94.07% line / 86.54% branch (test-closure); Core 89.17% / 77.59% | MailBridge project 90.90% line / 80.39% branch; Core project 98.60% / 91.68% | ComMessageSource.cs (NEW) 80.1% line / 60.9% branch — FAIL |

**Note:** C# is the only language with changed files in this branch diff. TypeScript, Python, PowerShell, and Bash have zero changed files on this branch.

### Coverage Evidence Checklist

- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- C# baseline coverage artifact: `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/baseline/baseline-test.md` (committed) and untracked local cobertura under `artifacts/coverage/baseline*`
- C# post-change coverage artifact: independently regenerated this review at `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/coverage-review/*/coverage.cobertura.xml` (cobertura, fresh run 2026-06-13T14:32); executor-committed summary at `evidence/qa-gates/final-test.md` and `evidence/qa-gates/coverage-delta.md`
- Per-language comparison summary: Section 1.2.1 below

**Non-negotiable verdict rule:** Numeric baseline and post-change coverage metrics are present for the single in-scope language (C#).

**Fail-closed rule:** A per-new-file coverage FAIL is present (ComMessageSource.cs). Overall verdict is NEEDS REVISION; remediation inputs produced.

**Evidence rule:** All coverage figures were independently re-measured this review by running `dotnet test ... --collect:"XPlat Code Coverage"` and parsing the resulting cobertura. No figures were synthesized.

---

## Executive Summary

This feature extends `MessageDto` with four trailing optional fields (`SenderEmailResolved`, `FromEmailAddress`, `ConversationId`, `MeetingMessageType`) and populates the previously hardcoded-null `ToJson`/`CcJson` fields, introduces a model-agnostic `IMessageSource` interface with a COM adapter `ComMessageSource` (locked decision D-D), propagates the new fields through `SchedulingDtoMapper` and both SQLite cache repositories with idempotent migrations, and adds unit tests for both the meeting-message and ordinary-mail paths.

The implementation is well-structured and matches the spec design decisions. The build passes with analyzers and code-style enforcement at 0 warnings / 0 errors, and the full test suite passes (530 pass, 0 fail, 3 skipped) — both independently re-run this review. Architecture COM-confinement is preserved (no COM types outside `OpenClaw.MailBridge`; no `*.csproj` reference changes).

One coverage finding blocks an unconditional PASS: the principal new file `ComMessageSource.cs` has per-file line coverage of 80.1% and branch coverage of 60.9%, both below the uniform new-code thresholds (line >= 85%, branch >= 75%). The per-project aggregate (90.90% / 80.39%) masks this gap. The uncovered lines are predominantly the COM-only true-SMTP resolution paths (PropertyAccessor `PR_SMTP_ADDRESS`, `GetExchangeUser`) and their fail-soft catch handlers, which are not reachable without live COM under the current test doubles; a subset (fallback branches at lines 175-176, 199-200, 215-218) is pure logic that the existing reflection-based doubles could exercise.

**Policy documents evaluated:**
- ✅ `general-code-change.md`
- ✅ `general-unit-test.md`

**Language-specific policies evaluated:**
- N/A `python-*` (no Python files changed)
- N/A `powershell-*` (no PowerShell files changed)
- N/A Bash (no Bash files changed)
- ✅ `csharp.md` + `architecture-boundaries.md` + `quality-tiers.md` (C# in scope)

[Test coverage: 530 tests pass; per-project C# coverage above uniform thresholds; one new file below per-file threshold.]

**Temporary artifacts cleanup:**
- ✅ No temporary throwaway scripts were created by this review.
- ✅ A fresh coverage run was written to `evidence/qa-gates/coverage-review/` (canonical feature evidence path); it contains a binary `.coverage` plus cobertura XML and may be pruned after remediation.
- Scripts created during development: none observed in the diff.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** - Tests run in any order | ✅ PASS | MSTest `[TestClass]`/`[TestMethod]`; tests use in-memory/throwaway SQLite and reflection-based COM doubles with no shared mutable state. Re-run this review: 530 pass deterministically. |
| **Isolation** - Each test targets single behavior | ✅ PASS | `ComMessageSourceTests` (adapter mapping), `OutlookScannerMessageFieldsTests` (scanner field paths), `CacheRepositoryMessageFieldsTests` / `CoreCacheRepositoryMessageFieldsTests` (round-trip), `SchedulingDtoMapperTests` (mapper). Each targets one behavior. |
| **Fast Execution** - Tests complete quickly | ✅ PASS | Independent run: MailBridge.Tests 11s, Core.Tests ~1s, HostAdapter.Tests 0.6s. |
| **Determinism** - Consistent results | ✅ PASS | No wall-clock dependence in new tests; COM is faked via reflection doubles. No `Thread.Sleep`/`Task.Delay` introduced (confirmed by diff inspection). |
| **Readability & Maintainability** - Clear structure | ✅ PASS | Descriptive test names (e.g. `ExchangeDnSender_should_resolve_to_true_smtp_not_the_dn`, `MeetingRequest_should_satisfy_combined_acceptance_signal`); AAA structure. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | ✅ PASS | **Baseline:** MailBridge 94.07% line / 86.54% branch (test-closure); Core 89.17% line / 77.59% branch.<br>**Command:** `dotnet test ... --collect:"XPlat Code Coverage"` (P0-T4).<br>**Timestamp:** 2026-06-13T13-34.<br>**Source:** `evidence/baseline/baseline-test.md`. |
| **No Coverage Regression** | ⚠️ PARTIAL | **Post-change (project):** MailBridge 90.90% line / 80.39% branch; Core 98.60% / 91.68%.<br>The MailBridge test-closure line/branch moved 94.07%/86.54% → 92.04%/83.29%, attributed by the executor to added fail-soft COM-only catch branches that are unreachable in unit tests. Both projects remain above uniform thresholds, so there is no repo-wide regression, but the change introduced new uncovered branches on the principal new file (see New Code Coverage). |
| **New Code Coverage >=85% line / >=75% branch** | ❌ FAIL | **New files:** `IMessageSource.cs` (interface, no executable lines), `ComMessageSource.cs`.<br>**ComMessageSource.cs:** 121/151 lines = **80.1%** line, 28/46 = **60.9%** branch (independently measured this review).<br>Both are below the uniform new-code thresholds (line >= 85%, branch >= 75%). FAIL — routed to remediation. |
| **Comprehensive Coverage** | ⚠️ PARTIAL | Modified files are well covered (OutlookScanner.cs 92.4%, OutlookScanner.Attendees.cs 91.0%, CacheRepository.cs 91.4%, CacheRepository.Readers.cs 96.3%, BridgeContracts.cs 100%, SchedulingDtoMapper.cs 95.2%, CoreCacheRepository.cs 97.6%). The COM-only SMTP-resolution chain in ComMessageSource.cs (PropertyAccessor, GetExchangeUser, several catch handlers) is untested. |
| **Positive Flows** - Valid inputs | ✅ PASS | Meeting-request and ordinary-mail paths populate all fields (`MeetingRequest_should_satisfy_combined_acceptance_signal`, `OrdinaryMail_should_populate_resolved_fields`). |
| **Negative Flows** - Invalid inputs | ⚠️ PARTIAL | Empty-recipient → `"[]"` and null/missing field degradation are covered; COM-throwing fail-soft catch paths in ComMessageSource are not exercised. |
| **Edge Cases** - Boundary conditions | ✅ PASS | No-recipient message, Bcc-ignored, all 5 `OlMeetingType` values + unknown→null (`ComMessageSourceTests` DataRows 0-4). |
| **Error Handling** - Error paths | ⚠️ PARTIAL | Mapper handles malformed JSON (`ParseAttendees` JsonException) — covered. COM-resolution catch handlers in ComMessageSource — not covered. |
| **Concurrency** - If applicable | N/A | Single-STA-thread COM model; no new concurrency introduced. |
| **State Transitions** - If applicable | N/A | No stateful component added beyond idempotent migrations (covered by round-trip tests). |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 94.07% line (MailBridge test-closure) / 89.17% line (Core) -> Post-change: 90.90% line / 80.39% branch (MailBridge project), 98.60% line / 91.68% branch (Core project). Change: MailBridge project +/- within threshold (no repo-wide regression; both projects remain >= 85% line / >= 75% branch). New/changed-code coverage: ComMessageSource.cs (NEW) 80.1% line / 60.9% branch. Disposition: FAIL (new-file coverage below uniform line >= 85% / branch >= 75% threshold). Evidence: `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/coverage-review/e309de69-9e3a-4b99-acb6-4b5dea06be13/coverage.cobertura.xml`, `evidence/qa-gates/coverage-delta.md`, `evidence/qa-gates/final-test.md`.
- TypeScript: Baseline: N/A - out of scope -> Post-change: N/A - out of scope. Change: N/A - out of scope. New/changed-code coverage: `N/A - out of scope`. Disposition: N/A. Evidence: `N/A - out of scope` (zero changed TypeScript files on branch).
- PowerShell: Baseline: N/A - out of scope -> Post-change: N/A - out of scope. Change: N/A - out of scope. New/changed-code coverage: `N/A - out of scope`. Disposition: N/A. Evidence: `N/A - out of scope` (zero changed PowerShell files on branch).

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | ✅ PASS | FluentAssertions used per repo convention; assertions name the expected field/value. |
| **Arrange-Act-Assert Pattern** | ✅ PASS | New tests follow AAA with reflection-based doubles in Arrange. |
| **Document Intent** | ✅ PASS | Self-documenting test names mapping to AC (see ac-mapping.md). |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | ✅ PASS | No live Outlook/network in tests; COM faked via reflection doubles (`MailBridgeMessageSourceTestDoubles.cs`). |
| **Use Mocks/Stubs** | ✅ PASS | Reflection-based fake message/recipient doubles; SQLite in-process. |
| **Environment Stability** | ✅ PASS | No temporary file creation observed in new test files; SQLite uses in-process databases. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | ✅ PASS | This audit plus `code-review.2026-06-13T14-32.md` and `feature-audit.2026-06-13T14-32.md`. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | ✅ PASS | spec.md issue #73; locked decisions D-A..D-D documented. |
| **Read existing change plans** | ✅ PASS | `plan.2026-06-13T13-34.md` present; `evidence/baseline/phase0-instructions-read.md` records policy read. |
| **Document the plan** | ✅ PASS | Plan file enumerates P0-T1..P7-T8 tasks. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | ✅ PASS | Adapter and fail-soft helpers are small and focused; SMTP chain decomposed into named private methods. |
| **Reusability** | ✅ PASS | `ReadMessageRecipients` and `SerializeAttendees` reuse the #71 attendee serializer/shape. |
| **Extensibility** | ✅ PASS | `IMessageSource` seam enables a future Graph adapter without touching Core/mapper/caches (D-D). |
| **Separation of concerns** | ✅ PASS | COM reads isolated in `ComMessageSource`; pure JSON shaping isolated in `OutlookScanner.Attendees.cs`. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | ✅ PASS | Each partial/file has a single responsibility (schema, readers, adapter, mapper). |
| **Under 500 lines** | ❌ FAIL | `src/OpenClaw.Core/CoreCacheRepository.cs` is 699 lines, over the 500-line cap. It was 687 lines at base (pre-existing violation), and this feature grew it by a net +12 lines (one `MigrateMessagesSchemaAsync` call, four reader named-arg lines, and the messages INSERT/VALUES/ON CONFLICT SQL expansion). All other touched files (including all new files) are at or below 500 lines. See `evidence/other/file-size-check.md`. The rule contains no pre-existing-file exemption; growing an over-cap production file is a policy violation even though most net-new body was routed to the partial. |
| **Public vs internal** | ✅ PASS | `IMessageSource`/`ComMessageSource` are `internal`; only the additive `MessageDto` record fields are public. |
| **No circular dependencies** | ✅ PASS | No `*.csproj` reference changes; `evidence/qa-gates/final-architecture.md`. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | ✅ PASS | PascalCase types/members, camelCase locals; method names describe intent. |
| **Docs/docstrings** | ✅ PASS | XML doc comments on the new interface, adapter, and helpers explaining D-A/D-B/D-C/D-D rationale. |
| **Comment why, not what** | ✅ PASS | Comments explain fail-soft and COM-confinement rationale rather than restating code. |

### 2.5 After Making Changes - Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | ⚠️ PARTIAL | **Command:** `dotnet csharpier check .` (committed evidence `final-csharpier.md`: 171 files, exit 0).<br>**Result:** Not independently re-run this review — the local dotnet tool manifest declares command `csharpier` but the package exposes `dotnet-csharpier`, so `dotnet tool restore` fails and CSharpier cannot be invoked locally. Verified by committed evidence only. |
| **2. Linting** | ✅ PASS | **Command:** `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`<br>**Result:** Independently re-run this review: Build succeeded, 0 Warning(s), 0 Error(s). |
| **3. Type checking** | ✅ PASS | Nullable reference analysis is part of the build above (0 warnings). Committed stricter gate `final-typecheck.md` used `-p:TreatWarningsAsErrors=true` exit 0. |
| **4. Testing** | ✅ PASS | **Command:** `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`<br>**Result:** Independently re-run: 530 pass, 0 fail, 3 skipped. |
| **Full toolchain loop** | ⚠️ PARTIAL | Lint/type/test verified clean in a single pass this review; format stage relies on committed evidence due to the tool-manifest defect; architecture verified by review and committed `final-architecture.md`. |
| **Explicit reporting** | ✅ PASS | Commands and results documented here and in `evidence/qa-gates/*`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | ✅ PASS | spec.md, plan.md, ac-mapping.md. |
| **Design choices explained** | ✅ PASS | D-A..D-D locked decisions in spec.md. |
| **Update supporting documents** | ⚠️ PARTIAL | spec.md Definition of Done leaves "Docs updated (README, links)" and "Telemetry/logging" unchecked; no README update in diff. |
| **Provide next steps** | ✅ PASS | file-size-check.md recommends a follow-up to split CoreCacheRepository.cs. |

---

## 3. Language-Specific Code Change Policy Compliance

### Section 3D: C# Code Change Policy Compliance

#### 3D.1 Tooling & Baseline

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting with CSharpier** | ⚠️ PARTIAL | `dotnet csharpier check .` per committed `final-csharpier.md` (exit 0). Not locally re-runnable (tool-manifest command/package mismatch). |
| **Linting with .NET analyzers** | ✅ PASS | `dotnet build ... -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` re-run: 0 warnings/0 errors. |
| **Type checking (nullable)** | ✅ PASS | Build clean with nullable enabled; no new nullable suppressions (grep for `#pragma warning`/`SuppressMessage`/`ExcludeFromCodeCoverage` returned none — `final-lint.md`). |
| **Testing with MSTest** | ✅ PASS | 530 pass re-run. |

#### 3D.2 C# Design & Type-Safety

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Strong contracts / explicit APIs** | ✅ PASS | `IMessageSource` is a narrow, purpose-specific seam; `MessageDto` additions are nullable-annotated trailing optionals. |
| **Null-safety by default** | ✅ PASS | `string?`/`int?` fields; `NormalizeAddress`/`LooksLikeSmtp` guard nulls. |
| **Composition / focused types** | ✅ PASS | Adapter composes COM helpers; no inheritance added. |
| **Asynchrony / resource safety** | ✅ PASS | COM wrappers released in `finally` via `ComActiveObject.ReleaseAll`; SQLite migration uses `await using` readers. |

#### 3D.3 C# Coding Standards & COM Interop

| Requirement | Status | Evidence |
|------------|--------|----------|
| **File-scoped namespaces** | ✅ PASS | `namespace OpenClaw.MailBridge;` etc. |
| **Exceptions / error codes** | ⚠️ PARTIAL | Adapter uses intentional broad fail-soft `catch { }` blocks (documented by spec D-C as required for non-Exchange/COM fragility). This is a deliberate boundary exception to the broad-catch prohibition, justified in-code; it does not re-raise but degrades to a documented fallback. Acceptable per spec but noted. |
| **COM confined to OpenClaw.MailBridge** | ✅ PASS | `final-architecture.md`: no Interop/Marshal/GetActiveObject outside MailBridge; adapter is internal to MailBridge. |
| **No analyzer/nullable suppressions** | ✅ PASS | None added (`final-lint.md`). |
| **File size <= 500** | ❌ FAIL | `CoreCacheRepository.cs` 699 lines (see 2.3). |

---

## 4. Language-Specific Unit Test Policy Compliance

### Section 4D: C# Unit Test Policy Compliance

#### 4D.1 Framework and Scope

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use MSTest** | ✅ PASS | `[TestClass]`/`[TestMethod]`/`[DataTestMethod]` with `[DataRow]`. |
| **Coverage expectation** | ❌ FAIL | New file `ComMessageSource.cs` 80.1% line / 60.9% branch, below uniform line >= 85% / branch >= 75%. Per-project aggregates pass. |

#### 4D.2 Test Style and Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Focused unit tests** | ✅ PASS | Each new test exercises one behavior. |
| **Mocking sparingly** | ✅ PASS | Reflection-based doubles for COM; no over-mocking. |
| **Organization** | ✅ PASS | Test files mirror the code units under test. |

#### 4D.3 Naming and Readability

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Naming conventions** | ✅ PASS | Behavior-descriptive method names. |
| **Docstrings/comments** | ✅ PASS | Test names self-document; ac-mapping.md cross-references. |

#### 4D.4 Running the Toolchain

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Use MSTest** | ✅ PASS | `dotnet test ...` re-run: 530 pass. |
| **No Alternative Test Runners** | ✅ PASS | MSTest only. |

---

## 5. Test Coverage Detail

### ComMessageSource.cs (NEW) — adapter (10 ComMessageSourceTests + scanner field tests)

| Test Name | Scenario Type | Lines Covered | Status |
|-----------|--------------|---------------|--------|
| Adapter_should_pass_conversation_id_through_unmodified | Positive | ConversationId getter | ✅ |
| MeetingType DataRows 0-4 / null | Edge Case | MeetingMessageType getter | ✅ |
| OrdinaryMail/Meeting recipient projection | Positive | ToRecipients/CcRecipients, EnsureRecipients | ✅ |
| Raw-sender fallback path | Negative/fallback | ResolveSenderSmtp raw branch | ✅ |

**Coverage:** 80.1% line (121/151), 60.9% branch (28/46) of ComMessageSource.cs.

**Not covered:** lines 111-114, 150-154 (fail-soft catch handlers), 175-176, 199-200, 215-218 (SMTP fallback branches), 249-258 (PropertyAccessor `PR_SMTP_ADDRESS` path), 282-292 (`GetExchangeUser` path). The PropertyAccessor and GetExchangeUser paths require live COM; the catch handlers require a COM throw. Fallback branches at 175-176 / 199-200 / 215-218 are pure logic and could be reached by extending the reflection doubles.

### OutlookScanner.Attendees.cs (MODIFIED) — ReadMessageRecipients (scanner field tests + adapter tests)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| OrdinaryMail_should_populate_resolved_fields_and_recipient_json | Positive | ✅ |
| OrdinaryMail_with_no_recipients_should_yield_empty_json_arrays | Edge Case | ✅ |

**Coverage:** 91.0% line (122/134). **Not covered:** minor per-recipient COM-read fallback branches.

### SchedulingDtoMapper.cs (MODIFIED) — MapMessage / MapMeetingMessageType

**Coverage:** 95.2% line (100/105), 85.4% branch. All 5 enum values + unknown→null covered by DataRows.

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests | 533 (530 run + 3 skipped) | ✅ |
| Tests Passed | 530 (100% of run) | ✅ |
| Tests Failed | 0 | ✅ |
| Execution Time | ~13s total across 3 assemblies | ✅ Fast |
| Functions/Classes Tested | New adapter/mapper/cache surfaces tested | ⚠️ ComMessageSource COM paths untested |
| Code Coverage | MailBridge 90.90% line / 80.39% branch (project); ComMessageSource 80.1% line / 60.9% branch (new file) | ❌ new-file below threshold |

---

## 7. Code Quality Checks

**For C#:**

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier Format | `dotnet csharpier check .` | committed evidence exit 0 (171 files); not locally re-run (tool-manifest defect) | ⚠️ |
| .NET Analyzers / Code Style | `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` | 0 warnings, 0 errors (re-run) | ✅ |
| Nullable Type Check | included in build above | 0 warnings | ✅ |
| MSTest Tests | `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` | 530 pass, 0 fail, 3 skipped (re-run) | ✅ |

**Notes:**
The dotnet tool manifest is not installable locally: it declares command `csharpier` but the package id `csharpier` exposes `dotnet-csharpier`, so `dotnet tool restore` fails. This is a known orchestrator-recorded defect. The format stage is therefore verified only by the committed `final-csharpier.md` evidence, not by an independent re-run this review.

---

## 8. Gaps and Exceptions

### Identified Gaps

- **New-code coverage (ComMessageSource.cs):** 80.1% line / 60.9% branch, below uniform line >= 85% / branch >= 75%. Plan: add tests that drive the SMTP-resolution fallback branches and catch handlers via the reflection-based COM doubles (PropertyAccessor returning a value, GetExchangeUser returning a PrimarySmtpAddress, a throwing member to exercise a catch), or document a coverage-exclusion disposition with operator sign-off for the genuinely COM-only paths.
- **File size (CoreCacheRepository.cs):** 699 lines (> 500). Pre-existing, grown +12 by this feature. Plan: follow-up extraction refactor of `UpsertEventsAsync`/`ReadEvent` into partials.
- **Formatting stage not independently verified:** tool-manifest command/package mismatch blocks local CSharpier. Verified by committed evidence only.
- **Definition of Done:** README/links and telemetry/logging items remain unchecked in spec.md.

### Approved Exceptions

- **Broad fail-soft `catch` in ComMessageSource:** justified by spec decision D-C (COM/non-Exchange fragility); degrades to documented fallback rather than re-raising. Documented in-code.

### Removed/Skipped Tests

- **None removed.** 3 tests are skipped by design (non-Windows COM-active-object test; two publish-output tests requiring a publish artifact) — pre-existing, unrelated to this feature.

---

## 9. Summary of Changes

### Commits in This PR/Branch

1. **9658ee7** — feat(mailbridge): resolve sender/from SMTP, recipients, conversationId, meetingType on MessageDto

### Files Modified

1. **src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs** (MODIFIED) — four trailing optional `MessageDto` fields.
2. **src/OpenClaw.MailBridge/IMessageSource.cs** (NEW) — model-agnostic seam.
3. **src/OpenClaw.MailBridge/ComMessageSource.cs** (NEW) — COM adapter, fail-soft SMTP resolution.
4. **src/OpenClaw.MailBridge/OutlookScanner.cs / OutlookScanner.Attendees.cs** (MODIFIED) — route resolved fields through the adapter; `ReadMessageRecipients`.
5. **src/OpenClaw.MailBridge/CacheRepository*.cs** (MODIFIED) — schema migration, parameter binding, readers.
6. **src/OpenClaw.Core/CoreCacheRepository*.cs** (MODIFIED) — schema migration, binding, readers.
7. **src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs** (MODIFIED) — wire sender/from/conversation; map meeting type to Graph vocabulary.
8. **tests/** (NEW + MODIFIED) — adapter, scanner-field, cache round-trip, mapper tests + doubles.

---

## Rejected Scope Narrowing

No caller instruction attempted to narrow the audit scope to a plan, task, phase, or file subset. The caller explicitly instructed a full feature-vs-base audit. One observation recorded for transparency: the regenerated `artifacts/pr_context.summary.txt` "Changed files overview" reported `Core logic changes: 0 files` and listed only docs/tooling, which is inconsistent with the actual `main..HEAD` git diff (18 changed `.cs` files including 3 new source files). Per the scope invariant, the authoritative scope is the resolved base branch (`be2ddbf`) and the actual branch diff; this audit was conducted against the full git diff, not the summary's truncated file overview.

## Evidence Location Compliance

A scan of the branch diff (`git diff --name-only be2ddbf..9658ee7`) found no committed files under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, or `artifacts/coverage/`. The `artifacts/` directory is gitignored; the local coverage artifacts under `artifacts/coverage/baseline*` and `artifacts/coverage/post-change*` are untracked and therefore not part of the diff. All committed feature evidence resides under the canonical `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/<kind>/` path. No evidence-location violation detected in the branch diff.

Note: the repository does not ship `validate_evidence_locations.py`; enforcement is the PreToolUse hook `.claude/hooks/enforce-evidence-locations.ps1`. The branch-diff scan above was performed manually via git.

---

## 10. Compliance Verdict

### Overall Status: ⚠️ PARTIALLY COMPLIANT

The implementation is functionally complete, builds clean with analyzers, passes all tests, and preserves architecture COM-confinement. Two policy findings prevent a full PASS: (1) the new file `ComMessageSource.cs` falls below the uniform new-code coverage thresholds (80.1% line / 60.9% branch vs 85% / 75%), and (2) `CoreCacheRepository.cs` exceeds the 500-line cap (699 lines; pre-existing, grown +12 by this feature).

**Fail-closed reminder:** Verdict is NEEDS REVISION, not PASS. Remediation inputs produced.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- ✅ Before Making Changes
- ✅ Design Principles
- ❌ Module & File Structure (CoreCacheRepository.cs > 500 lines)
- ✅ Naming, Docs, Comments
- ⚠️ Toolchain Execution (format stage verified by evidence only)
- ⚠️ Summarize & Document (DoD docs/telemetry unchecked)

#### Language-Specific Code Change Policy (Section 3 — C#)
- ⚠️ Tooling & Baseline (format not locally re-run)
- ✅ Design & Type-Safety
- ❌ Structure (file-size)

#### General Unit Test Policy (Section 1)
- ✅ Core Principles
- ❌ Coverage & Scenarios (new-file coverage below threshold)
- ✅ Test Structure
- ✅ External Dependencies
- ✅ Policy Audit

#### Language-Specific Unit Test Policy (Section 4 — C#)
- ✅ Framework & Scope (framework); ❌ Coverage
- ✅ Test Style & Structure
- ✅ Naming & Readability
- ✅ Toolchain

---

### Metrics Summary

- ✅ 530/530 run tests passing (100%)
- ✅ MailBridge project 90.90% line / 80.39% branch; Core 98.60% / 91.68%
- ❌ ComMessageSource.cs (new) 80.1% line / 60.9% branch — below threshold
- ❌ CoreCacheRepository.cs 699 lines (> 500)
- ✅ Build clean (0 warnings, 0 errors) with analyzers + code-style
- ✅ Architecture COM-confinement preserved

---

### Recommendation

**Needs revision.** Address the new-file coverage gap on `ComMessageSource.cs` (add tests for the SMTP fallback/catch branches reachable via the reflection doubles, or record an operator-approved coverage disposition for the genuinely COM-only paths) and resolve or formally accept the `CoreCacheRepository.cs` file-size finding. After those are addressed, the change is otherwise ready.

---

## Appendix A: Test Inventory

- OpenClaw.MailBridge.Tests › ComMessageSourceTests › (adapter mapping incl. DataRows 0-4)
- OpenClaw.MailBridge.Tests › OutlookScannerMessageFieldsTests › ExchangeDnSender_should_resolve_to_true_smtp_not_the_dn
- OpenClaw.MailBridge.Tests › OutlookScannerMessageFieldsTests › DelegateSentMeeting_should_reflect_on_behalf_of_in_from_address
- OpenClaw.MailBridge.Tests › OutlookScannerMessageFieldsTests › OrdinaryMail_should_populate_resolved_fields
- OpenClaw.MailBridge.Tests › OutlookScannerMessageFieldsTests › OrdinaryMail_with_no_recipients_should_yield_empty_json_arrays
- OpenClaw.MailBridge.Tests › OutlookScannerMessageFieldsTests › MeetingRequest_should_satisfy_combined_acceptance_signal
- OpenClaw.MailBridge.Tests › CacheRepositoryMessageFieldsTests › (round-trip persist/read)
- OpenClaw.Core.Tests › CoreCacheRepositoryMessageFieldsTests › (round-trip persist/read)
- OpenClaw.Core.Tests › SchedulingDtoMapperTests › (MapMessage + MapMeetingMessageType DataRows)

Aggregate (re-run this review): OpenClaw.HostAdapter.Tests 89/89; OpenClaw.Core.Tests 206/206; OpenClaw.MailBridge.Tests 235 passed / 3 skipped / 238 total. Total 530 passed, 3 skipped.

---

## Appendix B: Toolchain Commands Reference

**For C#:**
```bash
# Formatting (committed evidence only; not locally re-runnable due to tool-manifest defect)
dotnet csharpier check .

# Linting + Code Style + Nullable type check
dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true

# Stricter type-check gate
dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true

# Testing + coverage
dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-06-13
**Policy Version:** Current (as of audit date)
