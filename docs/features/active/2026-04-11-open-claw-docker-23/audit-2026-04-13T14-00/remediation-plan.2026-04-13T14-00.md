# Remediation Plan: OpenClaw Docker Pre-MVP (#23)

**Plan Date:** 2026-04-13
**Prepared by:** GitHub Copilot (atomic_planner)
**Feature Folder:** `docs/features/active/2026-04-11-open-claw-docker-23`
**Work Mode:** `full-feature`
**Trigger:** Feature audit verdict = NEEDS REVISION — STC5 sub-scenario "Empty calendar-window results outside cache range" has no recorded execution evidence; 3 skipped tests lack documented rationale.
**Remediation Inputs:** `docs/features/active/2026-04-11-open-claw-docker-23/remediation-inputs.2026-04-13T14-00.md`
**Scope:** Remediation Items 1 and 2 from remediation inputs. Item 3 (HTTP 500 vs 503 for missing token file) is out of scope for this pass and is documented as a deferred follow-up only.
**AC Sources:** `spec.md` §Seeded Test Conditions STC5, `user-story.md` (no changes expected).
**Review Artifacts:** `feature-audit.2026-04-13T14-00.md`, `code-review.2026-04-13T14-00.md`, `policy-audit.2026-04-13T14-00.md`

---

## Overview

This plan closes the single blocking gap that prevents the STC5 full-feature audit criterion from moving from PARTIAL to PASS. The work covers three targeted activities: (1) running and recording the empty calendar-window execution evidence to satisfy STC5, (2) identifying and documenting the three skipped tests in a new evidence file (and adding inline code comments for any that lack a documented reason), and (3) recording the HTTP 500-vs-503 design inconsistency as a deferred post-merge follow-up entry rather than implementing the change now. No production code changes are expected. A final QA pass is included to validate any test-comment edits made during Phase 3 and to confirm the feature-audit and spec.md are updated correctly.

---

### Phase 0 — Context & Inputs

- [x] [P0-T1] Read policy files in required order — `general-code-change.instructions.md` → `csharp-code-change.instructions.md` → `csharp-unit-test.instructions.md` → `general-unit-test.instructions.md` — and record a policy-read evidence artifact at `docs/features/active/2026-04-11-open-claw-docker-23/evidence/other/remediation-phase0-instructions-read.2026-04-13T14-00.md` containing `Timestamp:`, `PolicyOrder:` (listing all four files in read order), and a confirmation line that all files were read.
  - Acceptance: the file exists; `Timestamp:` is present; `PolicyOrder:` lists all four policy files in the correct order; a confirmation line is present.

- [x] [P0-T2] Read `remediation-inputs.2026-04-13T14-00.md` in full; read `spec.md` §Seeded Test Conditions STC5 (line 184) and confirm the checkbox is `- [ ]`; read `feature-audit.2026-04-13T14-00.md` STC5 row and Overall Verdict; read `evidence/qa-gates/operator-troubleshooting.2026-04-13T00-21-39Z.md` and confirm `EmptyCalendarWindowFinding` is absent.
  - Acceptance: confirmed — spec.md STC5 checkbox is `- [ ]`; feature-audit STC5 sub-row verdict for "Empty calendar-window outside cache range" is `⚠️ PARTIAL`; Overall Verdict is `NEEDS REVISION`; `EmptyCalendarWindowFinding` is absent from the operator-troubleshooting artifact; Item 3 scope is confirmed as post-merge future consideration only.

- [x] [P0-T3] Capture remediation baseline test state: run `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --settings mailbridge.runsettings` and record the result in `TestResults/remediation-baseline/test-baseline.2026-04-13T14-00.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including the test summary line.
  - Acceptance: artifact exists; `EXIT_CODE: 0`; `Output Summary:` records `passed=115, failed=0, skipped=3`.

---

### Phase 1 — STC5 Execution Evidence

- [x] [P1-T1] Verify the Docker compose stack and Windows-side HostAdapter are operational by running `curl.exe -v -H "Authorization: Bearer <token>" http://localhost:4319/v1/status` with the bearer token from `%USERPROFILE%\.openclaw\hostadapter.token`; if the stack is not running, start it with `docker compose up -d` and start the HostAdapter process before proceeding, then re-run the status check.
  - Acceptance: `curl` returns HTTP 200 with a valid `ApiEnvelope<BridgeStatusDto>` body; the stack and HostAdapter are confirmed operational before proceeding to P1-T2.

- [x] [P1-T2] Run the empty calendar-window request using the bearer token from `%USERPROFILE%\.openclaw\hostadapter.token`:
  ```
  curl.exe -v -H "Authorization: Bearer <token>" "http://localhost:4319/v1/calendar?start=2026-01-01T00:00:00Z&end=2026-01-02T00:00:00Z&limit=5"
  ```
  Record the HTTP status code and full response body verbatim. If the response body contains non-empty items (i.e., the date range overlaps cached data), retry with an earlier range such as `start=2025-01-01T00:00:00Z&end=2025-01-02T00:00:00Z` to obtain a range guaranteed to be outside the cache window; record the adjusted command and response.
  - Acceptance: HTTP status code is 200; response body contains `"items":[]` (an empty items array); a `meta.bridge` block is present; no fabricated event entries appear in the items array.

- [x] [P1-T3] Create evidence file `docs/features/active/2026-04-11-open-claw-docker-23/evidence/qa-gates/empty-calendar-window-demo.2026-04-13T14-00.md` containing:
  - `EmptyCalendarWindowFinding: PASS`
  - `DemoCommand:` (the exact curl command used in P1-T2)
  - `ResponseStatus: 200`
  - `ResponseBody:` (verbatim response body from P1-T2)
  - `Observation:` (confirming the items array is empty, no fabricated data is returned, and `meta.bridge.cacheStale` reflects the correct freshness state)
  - Acceptance: file exists at the stated path; `EmptyCalendarWindowFinding: PASS` is present; `ResponseStatus: 200` is recorded; `ResponseBody:` field is populated with the actual response and contains `"items":[]`.

- [x] [P1-T4] Update `docs/features/active/2026-04-11-open-claw-docker-23/spec.md` STC5 (line 184): change `- [ ] Operator troubleshooting coverage...` to `- [x] Operator troubleshooting coverage...`.
  - Acceptance: `spec.md` line 184 reads `- [x]` at the start; no other checkbox lines are altered.

- [x] [P1-T5] Update `docs/features/active/2026-04-11-open-claw-docker-23/feature-audit.2026-04-13T14-00.md` STC5 table: change the "Empty calendar-window outside cache range" sub-row verdict from `⚠️ **PARTIAL**` to `✅ **PASS**` and update the Evidence column for that sub-row to reference `evidence/qa-gates/empty-calendar-window-demo.2026-04-13T14-00.md`.
  - Acceptance: the `⚠️ **PARTIAL**` verdict for the empty-calendar-window sub-row is no longer present; `✅ **PASS**` is present in its place; the Evidence column references the new demo file.

- [x] [P1-T6] Update `docs/features/active/2026-04-11-open-claw-docker-23/feature-audit.2026-04-13T14-00.md`: change the STC5 overall-row verdict from `⚠️ **PARTIAL**` to `✅ **PASS**`; change the Seeded Test Conditions Subtotal line from `4 PASS, 1 PARTIAL, 0 FAIL` to `5 PASS, 0 PARTIAL, 0 FAIL`; change the Overall Verdict section from `NEEDS REVISION` to `PASS`.
  - Acceptance: `feature-audit.2026-04-13T14-00.md` no longer contains `NEEDS REVISION` as the overall verdict; Overall Verdict is `PASS`; Seeded Test Conditions Subtotal reads `5 PASS, 0 PARTIAL, 0 FAIL`.

---

### Phase 2 — 500-vs-503 Known-Issue Documentation

- [x] [P2-T1] Search `src/OpenClaw.HostAdapter/` for the class that returns HTTP 500 for a missing or empty server-side token file; record the exact file path and approximate line number.
  - Acceptance: one file path and one class name returning HTTP 500 for a missing token file are identified; this information is complete enough to populate P2-T2.

- [x] [P2-T2] Create `docs/features/active/2026-04-11-open-claw-docker-23/evidence/other/500-503-assessment.2026-04-13T14-00.md` with the following fields:
  - `File:` (path from P2-T1)
  - `ApproxLine:` (line number from P2-T1)
  - `CurrentStatus: 500`
  - `RecommendedStatus: 503`
  - `ChangeComplexity:` (Simple — if it is a single-line status code change; or Compound — if it requires additional model or test updates)
  - `DeferralReason: Per remediation-inputs.2026-04-13T14-00.md Item 3, this change is out of scope for this remediation pass. A follow-up GitHub issue should be opened post-merge.`
  - Acceptance: file exists at the stated path; all six fields are present and non-blank; `DeferralReason:` explicitly references `remediation-inputs.2026-04-13T14-00.md Item 3`.

- [x] [P2-T3] Open `docs/features/active/2026-04-11-open-claw-docker-23/evidence/other/feature-completion.2026-04-13T04-24-54Z.md`, locate §Outstanding Follow-Ups (or create that section if absent), and append an entry for the HTTP 500 → 503 post-merge improvement including: the source file identified in P2-T1, the current status code (500), the recommended status code (503), a reference to `code-review.2026-04-13T14-00.md` Minor finding row 1, and an instruction to open a new GitHub issue post-merge.
  - Acceptance: `feature-completion.2026-04-13T04-24-54Z.md` contains a new entry in §Outstanding Follow-Ups referencing the HTTP 500 → 503 change, the source file from P2-T1, and a post-merge action note; no other sections in the file are modified.

---

### Phase 3 — Skipped Test Documentation

- [x] [P3-T1] Search `tests/OpenClaw.HostAdapter.Tests/`, `tests/OpenClaw.Core.Tests/`, and `tests/OpenClaw.MailBridge.Tests/` for all `[Ignore]` and `[Ignore(` attribute occurrences and any runtime guard conditions; list file path, class name, and method name for every occurrence found.
  - Acceptance: search returns exactly 3 occurrences, consistent with the `skipped=3` count from `evidence/qa-gates/coverage.2026-04-13T02-07-35Z.md`; all three test method locations are identified with full paths.

- [x] [P3-T2] For each of the 3 skipped tests identified in P3-T1: read the containing test method (e.g., `[Ignore("reason")]` with a non-empty string, `[Ignore]` without a reason, or a guard condition) and assess `IssueRisk` as Low, Medium, or High based on whether a reachable production code path is left uncovered as a result.
  - Precondition: P3-T1 is complete and all 3 test file locations are known.
  - Acceptance: for each of the 3 tests, the skip mechanism type and `IssueRisk` value are determined and documented internally in preparation for P3-T3 and P3-T4.

- [x] [P3-T3] Create `docs/features/active/2026-04-11-open-claw-docker-23/evidence/qa-gates/skipped-tests.2026-04-13T14-00.md` containing a `SkippedTests:` array with exactly 3 entries; each entry must include `TestName:` (full method name), `Class:`, `Assembly:`, `SkipReason:` (the documented or inferred reason; never left blank), and `IssueRisk:` (Low, Medium, or High).
  - Acceptance: file exists at the stated path; `SkippedTests:` section contains exactly 3 entries; each entry has all 5 required fields populated with non-placeholder values.

- [x] [P3-T4] For each of the 3 skipped tests identified in P3-T2: if the test method's skip attribute has no reason string
  - Precondition: P3-T2 is complete; skip mechanism type is known for all 3 tests.
  - Acceptance: every skipped test method either (a) already has `[Ignore("reason")]` with a non-empty reason string, or (b) has an `// Skipped: <reason>` comment directly above the skip attribute; no skip mechanism remains entirely undocumented after this task; the source files compile cleanly as confirmed in P4-T2 and P4-T3.

---

### Phase 4 — Final Re-Audit Gate

> **Toolchain restart rule:** If P4-T1 reformats any files, re-run P4-T1 through P4-T4 in order until all four steps complete without errors or file changes in a single pass.

- [x] [P4-T1] Run `csharpier .` and record the result in `TestResults/remediation-qa/csharpier-remediation.2026-04-13T14-00.md` with fields `Timestamp:`, `Command: csharpier .`, `EXIT_CODE:`, and `Output Summary:` (number of files formatted or confirmation that 0 files were reformatted).
  - Acceptance: `EXIT_CODE: 0`; `Output Summary:` confirms 0 files reformatted (or, if Phase 3 made source edits, confirms that all modified files were reformatted and the subsequent re-run reports 0 files remaining).

- [x] [P4-T2] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and record the result in `TestResults/remediation-qa/msbuild-analyzers-remediation.2026-04-13T14-00.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: `EXIT_CODE: 0`; `Output Summary:` confirms build succeeded with 0 errors.

- [x] [P4-T3] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` and record the result in `TestResults/remediation-qa/msbuild-nullable-remediation.2026-04-13T14-00.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
  - Acceptance: `EXIT_CODE: 0`; `Output Summary:` confirms build succeeded with 0 errors and 0 nullable warnings treated as errors.

- [x] [P4-T4] Run `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build --settings mailbridge.runsettings` and record the result in `TestResults/remediation-qa/test-remediation.2026-04-13T14-00.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including the full test summary line (`total=`, `passed=`, `failed=`, `skipped=`).
  - Acceptance: `EXIT_CODE: 0`; `Output Summary:` records `passed=115, failed=0`; the `skipped=` count matches the count of tests documented in `evidence/qa-gates/skipped-tests.2026-04-13T14-00.md` (expected: 3, unless P3-T4 changed the skip state of any test).

- [x] [P4-T5] Read `docs/features/active/2026-04-11-open-claw-docker-23/feature-audit.2026-04-13T14-00.md` and confirm: (a) Overall Verdict is `PASS`; (b) the STC5 sub-row for "Empty calendar-window outside cache range" shows `✅ **PASS**`; (c) Seeded Test Conditions Subtotal reads `5 PASS, 0 PARTIAL, 0 FAIL`.
  - Acceptance: all three conditions (a), (b), and (c) are confirmed true; no instance of `NEEDS REVISION` or `PARTIAL` remains in the feature-audit for STC5.

- [x] [P4-T6] Read `docs/features/active/2026-04-11-open-claw-docker-23/spec.md` §Seeded Test Conditions and confirm the STC5 checkbox (line 184) starts with `- [x]`.
  - Acceptance: `spec.md` line 184 starts with `- [x]`; no other checkbox states differ from their pre-remediation values.

- [x] [P4-T7] Read `docs/features/active/2026-04-11-open-claw-docker-23/evidence/qa-gates/empty-calendar-window-demo.2026-04-13T14-00.md` and confirm it contains `EmptyCalendarWindowFinding: PASS`, `ResponseStatus: 200`, and a non-empty `ResponseBody:` field whose value includes `"items":[]`.
  - Acceptance: all three fields are present and contain the required values; no placeholder text remains.

- [x] [P4-T8] Read `docs/features/active/2026-04-11-open-claw-docker-23/evidence/qa-gates/skipped-tests.2026-04-13T14-00.md` and confirm it exists with a `SkippedTests:` array containing exactly 3 entries, each with `TestName:`, `Class:`, `Assembly:`, `SkipReason:`, and `IssueRisk:` fields populated.
  - Acceptance: file exists; `SkippedTests:` array has exactly 3 entries; all required fields are non-blank in every entry.

---

## Artifacts Created or Updated by This Remediation

| Artifact | Action |
|---|---|
| `evidence/other/remediation-phase0-instructions-read.2026-04-13T14-00.md` | Created (Phase 0) |
| `TestResults/remediation-baseline/test-baseline.2026-04-13T14-00.md` | Created (Phase 0) |
| `evidence/qa-gates/empty-calendar-window-demo.2026-04-13T14-00.md` | Created (Phase 1) |
| `spec.md` STC5 checkbox | Updated `- [ ]` → `- [x]` (Phase 1) |
| `feature-audit.2026-04-13T14-00.md` STC5 row + Overall Verdict | Updated PARTIAL → PASS, NEEDS REVISION → PASS (Phase 1) |
| `evidence/other/500-503-assessment.2026-04-13T14-00.md` | Created (Phase 2) |
| `evidence/other/feature-completion.2026-04-13T04-24-54Z.md` §Outstanding Follow-Ups | Appended HTTP 500→503 entry (Phase 2) |
| `evidence/qa-gates/skipped-tests.2026-04-13T14-00.md` | Created (Phase 3) |
| Test source files (up to 3) | Conditionally updated — inline `// Skipped:` comment if `[Ignore]` lacks a reason string (Phase 3) |
| `TestResults/remediation-qa/csharpier-remediation.2026-04-13T14-00.md` | Created (Phase 4) |
| `TestResults/remediation-qa/msbuild-analyzers-remediation.2026-04-13T14-00.md` | Created (Phase 4) |
| `TestResults/remediation-qa/msbuild-nullable-remediation.2026-04-13T14-00.md` | Created (Phase 4) |
| `TestResults/remediation-qa/test-remediation.2026-04-13T14-00.md` | Created (Phase 4) |

## Do-Not-Do Constraints (from Remediation Inputs)

- Do not add production code, new tests, or new policies to close STC5.
- Do not adjust the API `limit` cap or modify any API behavior.
- Do not implement the HTTP 500 → 503 status code change in this pass; document as a deferred follow-up only.
- Do not weaken or suppress existing tests.
- Do not create temporary files within tests.
- Do not re-run the full Phase 7 implementation; only record execution evidence and update artifacts.
