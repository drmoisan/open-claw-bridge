# Policy Compliance Audit — Issue #137 (hostadapter-v1-basepath-mismatch)

- Reviewed: 2026-07-10T15-10
- Reviewer: feature-review agent
- Base branch: `main` (merge-base `4ce19d186e98c2697eee07ba8a7866ce10af08a0`)
- Head: `bug/hostadapter-v1-basepath-mismatch-137` @ `b33db1867ce009a79a77df7e92363c86821ea764`
- Work mode: `full-bug` (AC source: `spec.md` `## Acceptance Criteria`)
- Scope: full branch diff, merge-base..HEAD, 36 files changed (937 insertions / 6 deletions)

## Rejected Scope Narrowing

None. The delegating prompt supplied the full changed-file list (PowerShell, C#, YAML/config) and did not attempt to narrow scope to a subset. No narrowing language was detected in the caller prompt.

## Policy Reading Order Applied

1. `CLAUDE.md` — absent at repository root (confirmed via direct read attempt: no such file). This is pre-existing repository state, also independently documented by the branch's own baseline evidence (`evidence/baseline/phase0-instructions-read.2026-07-10T12-00.md`), and is not attributable to this PR.
2. `.claude/rules/general-code-change.md` — read, applied below.
3. `.claude/rules/general-unit-test.md` — read, applied below.
4. `.claude/rules/powershell.md` — read, applied (files in scope: `scripts/Install.Preflight.psm1`, `tests/scripts/Install.Preflight.Tests.ps1`).
5. `.claude/rules/csharp.md` — read, applied (files in scope: `src/OpenClaw.Core/Program.cs`, `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs`).
6. `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/orchestrator-state.md` — reviewed for applicability; only `architecture-boundaries.md` and `quality-tiers.md` are relevant to this diff (no benchmark, workflow, or orchestrator-state artifacts are touched).

## Evidence Location Compliance

- **Verdict: PASS.**
- Scanned the full branch diff (`git diff --name-only <merge-base>..HEAD`) for paths under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, `artifacts/coverage/`: zero matches.
- All 20 evidence artifacts added by this branch live under the canonical path `docs/features/active/2026-07-10-hostadapter-v1-basepath-mismatch-137/evidence/{baseline,regression-testing,qa-gates,other,issue-updates}/`, consistent with the Evidence Location Invariant.
- `validate_evidence_locations.py` was not found anywhere in the repository (searched full tree); fell back to a manual diff scan per the "Review env fallbacks" precedent. No violations found by the manual scan.
- Raw (non-evidence) C# test intermediates (TRX/Cobertura) are written to `artifacts/csharp/{baseline,final}/` — this is gitignored, non-committed, raw-output storage explicitly distinguished from evidence in the plan's Conventions section, and is consistent with established repository precedent for raw coverage intermediates. It is not a violation of the Evidence Location Invariant, which governs *evidence artifacts* (summarizing markdown), not raw tool output.
- No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries were needed; no caller instruction specified a non-canonical evidence path.

## Coverage Verification

Languages with changed files in this branch: **PowerShell** and **C#**. (YAML/config files — `.env.example`, `docker-compose.yml`, `docker-compose.dev.yml` — are not source languages covered by the coverage-artifact table and carry no coverage obligation.)

### PowerShell — Verdict: PASS

- Canonical artifact `artifacts/pester/powershell-coverage.xml` exists on disk, but its file timestamp (2026-07-10 10:11) predates this feature's own Phase 0 baseline capture (12:00–12:35) and Phase 5 final run (13:40–13:55); it does not reflect this branch's post-change state and is not relied upon as primary evidence.
- Primary evidence is the branch's own dual-timestamped, before/after coverage record, produced via the documented corrected-runsettings workaround (established defect #111/#125/#135 — bundled `run_poshqc_test` fails on every invocation in this repo):
  - Baseline (`evidence/baseline/poshqc-test.2026-07-10T12-35.md`): 369 tests passed, repo-wide command/line coverage 89.93%, 2,015 analyzed Commands across 30 files, `ExcludedPath = @()`.
  - Post-change (`evidence/qa-gates/final-poshqc-test.2026-07-10T13-45.md`): 370 tests passed (+1, the new AC-5 regression test), repo-wide command/line coverage 89.93% (unchanged), same 30 files measured, no exclusions.
  - Delta/threshold comparison (`evidence/qa-gates/coverage-comparison-powershell.2026-07-10T13-50.md`): no repo-wide regression (89.93% == 89.93%), line coverage >= 85% (PASS), command-coverage branch proxy >= 75% (PASS, with the proxy rationale documented — Pester's `CodeCoverage` engine reports only a single command/line metric, not a separate branch metric), no production file excluded.
- Modified production file `scripts/Install.Preflight.psm1` (single-line literal change inside `Get-HostAdapterPreflightUri`) is included in the measured 30-file set with no exclusion.
- Repo-wide 89.93% >= 85% threshold: PASS. No regression on changed lines: PASS (delta is 0.00pp).

### C# — Verdict: PASS

- Canonical artifact `artifacts/csharp/coverage.xml` exists on disk but is dated 2026-06-06 (over a month stale relative to this branch's 2026-07-10 work) and reports a different, older repo-wide blended figure (90.16%/82.28%); it is not relied upon as primary evidence for the same reason as the PowerShell artifact above.
- Primary evidence is the branch's own baseline/final Cobertura comparison:
  - Baseline (`evidence/baseline/csharp-test-coverage.2026-07-10T12-20.md`): `OpenClaw.Core` package line-rate 99.29%, branch-rate 92.28%; class-level `Program.cs` line-rate 100%, branch-rate 100%.
  - Post-change (`evidence/qa-gates/final-csharp-test-coverage.2026-07-10T13-45.md`): `OpenClaw.Core` package line-rate 99.29%, branch-rate 92.28% (unchanged); class-level `Program.cs` line-rate 100%, branch-rate 100% (unchanged). `OpenClaw.Core.Tests`: 930 → 931 passed (+1, the new AC-6 regression test), 0 failed.
  - Delta/threshold comparison (`evidence/qa-gates/coverage-comparison-csharp.2026-07-10T13-50.md`): no `OpenClaw.Core` regression (PASS), line coverage 99.29% >= 85% (PASS), branch coverage 92.28% >= 75% (PASS).
  - The new test file `CoreHostAdapterBaseUrlFallbackTests.cs` is correctly excluded from the production coverage denominator per the Coverage Exclusion Policy (test files are a permitted exclusion category).
- `mailbridge.runsettings` was inspected directly: the only `<Exclude>` entries are `[*.Tests]*` (assembly-level test-project exclusion) and the `OpenClaw.MailBridge.Tests.dll` module path — both permitted exclusions under the Coverage Exclusion Policy. No production source path is excluded.
- Modified production file `src/OpenClaw.Core/Program.cs` (single-line literal change inside the existing `PostConfigure` fallback branch) remains at 100%/100% class-level coverage before and after. New file's changed lines (the six literal edits) are exercised by both pre-existing tests and the two new regression tests (AC-5, AC-6).

## Uniform Toolchain Gates (general-code-change.md, powershell.md, csharp.md)

| Gate | PowerShell | C# | Evidence |
|---|---|---|---|
| Format | PASS | PASS | `evidence/qa-gates/final-poshqc-format.2026-07-10T13-40.md`, `evidence/qa-gates/final-csharp-format.2026-07-10T13-40.md` |
| Lint/Analyze | PASS (0 error-severity) | PASS (0 warnings/errors, analyzers run via `dotnet build` with `TreatWarningsAsErrors=true`) | `evidence/qa-gates/final-poshqc-analyze.2026-07-10T13-42.md`, `evidence/qa-gates/final-csharp-build.2026-07-10T13-42.md` |
| Type-check | N/A (PowerShell) | PASS (nullable enabled solution-wide, enforced via same build) | same as above |
| Architecture boundaries | N/A (no `.NET` boundary changes touched) | PASS (no new dependency edges; `NetArchTest.Rules` tests execute inside the same `OpenClaw.Core.Tests` run per plan's Conventions; `src/OpenClaw.HostAdapter/Program.cs` confirmed byte-identical, no `/v1` routing added) | `git diff` confirms `HostAdapter/Program.cs` untouched; `evidence/qa-gates/final-csharp-test-coverage.2026-07-10T13-45.md` |
| Unit tests | PASS (370/370, 0 failed) | PASS (931 + 100 + 347/352 passed, 5 pre-existing unrelated skips) | `evidence/qa-gates/final-poshqc-test.2026-07-10T13-45.md`, `evidence/qa-gates/final-csharp-test-coverage.2026-07-10T13-45.md` |
| Single clean pass, no restart pending | PASS (one restart occurred mid-loop when CSharpier flagged the new test file; the recorded final artifacts are all from the subsequent clean re-run with no further restart) | same | `evidence/qa-gates/ac8-toolchain-summary.2026-07-10T13-55.md` |

Restart-loop handling is compliant with `general-code-change.md`'s "restart from step 1 if any stage fails or auto-fixes files" requirement: the CSharpier auto-format triggered a documented restart from PowerShell format, and the final recorded artifacts are all from the subsequent single clean pass.

## File Size Limit (general-code-change.md, 500-line cap)

- New/changed files in this branch: `CoreHostAdapterBaseUrlFallbackTests.cs` (46 lines), `Install.Preflight.psm1` (377 lines), `Install.Preflight.Tests.ps1` (410 lines), `Program.cs` (368 lines) — all under the 500-line cap.
- Two **pre-existing** violations were correctly identified and avoided as extension targets rather than fixed in-scope (fixing them was not part of this bug's AC, and silently expanding scope to include an unrelated refactor would itself be a policy concern):
  - `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` — 616 lines (confirmed via direct line count). The plan correctly avoided extending it and added a new sibling file instead (citing prior repository precedent, issues #128/#130).
  - `tests/scripts/Install.Tests.ps1` — 505 lines (confirmed via direct line count). The plan correctly targeted `Install.Preflight.Tests.ps1` (398 lines pre-change, 410 post-change) instead.
- These two pre-existing over-cap files are flagged here as **informational, not blocking** for this PR — they are unmodified by this branch and their remediation is out of this bug's scope. They should be tracked as separate technical-debt follow-ups.

## Testing Standards — Framework Convention Note (informational, not blocking)

`.claude/rules/csharp.md` states the testing framework standard is "xUnit ... with `[Fact]` and `[Theory]` attributes." The new test file `CoreHostAdapterBaseUrlFallbackTests.cs` uses MSTest (`[TestClass]`/`[TestMethod]`, `Microsoft.VisualStudio.TestTools.UnitTesting`). Verification: `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj` references `MSTest.TestAdapter`/`MSTest.TestFramework` and carries no `xunit` package reference; a repository-wide grep of all 127 files under `tests/OpenClaw.Core.Tests/` shows `[TestClass]`/MSTest usage is the exclusive, pre-existing convention for this entire test project (not a new deviation introduced by this branch). The new file is consistent with 100% of its sibling files. This is a pre-existing conflict between the written rule and actual, established project-wide practice, not a new departure from convention introduced by this PR. Recorded as informational; not treated as a blocking finding against this branch, since consistency with the existing test project's convention is the more defensible choice than a lone xUnit file inside an all-MSTest project.

## Quality Tiers (quality-tiers.md)

- `OpenClaw.Core` / `OpenClaw.Core.Tests` are classified **T1** in `quality-tiers.yml`. Uniform coverage thresholds (line >= 85%, branch >= 75%) apply and are met (99.29%/92.28%, see Coverage Verification above).
- T1-specific gates (CsCheck property tests, Stryker mutation score >= 75%) are not triggered by this change: the change is a literal string-value correction inside an existing, already-100%-covered fallback branch, not a new pure function. No new pure function was introduced that would require a new property test under the "at least one property test per pure function" rule. This is consistent with the spec's explicit design summary ("no routing, DTO, or business-logic change ... a string-literal correction").

## Benchmark Baselines / CI Workflows / Orchestrator-State Rules

- Not applicable — no files under `scripts/benchmarks/**`, no `.github/workflows/**` diffs, and no `artifacts/orchestration/orchestrator-state.json` changes are present in this branch's diff.

## Architecture Boundaries

- **Verdict: PASS.** No new dependency edges. `src/OpenClaw.HostAdapter/Program.cs` (adapter-side routing) is confirmed byte-identical (`git diff` empty) and no `/v1` route/group/prefix was added — consistent with the spec's explicit non-goal. `src/OpenClaw.Core/Program.cs` is a host-bound entry point; its one-line change is a literal-value edit only, with no new imports or layer crossings.

## Overall Policy Verdict

**PASS.** No blocking findings. Two informational notes are recorded (pre-existing 500-line-cap violations in unmodified sibling files; pre-existing MSTest-vs-xUnit convention divergence) — neither is attributable to this branch's changes and neither blocks merge.
