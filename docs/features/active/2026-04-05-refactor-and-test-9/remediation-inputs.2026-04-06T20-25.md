# Remediation Inputs

- Timestamp: 2026-04-06T20-25
- Feature: `docs/features/active/2026-04-05-refactor-and-test-9`
- Base branch: `development`
- Source audits:
  - `policy-audit.2026-04-06T20-25.md`
  - `code-review.2026-04-06T20-25.md`
  - `feature-audit.2026-04-06T20-25.md`

## Required Fixes

1. **Resolve the conflicted feature docs and restore a single authoritative requirements set.**
   - Files:
     - `docs/features/active/2026-04-05-refactor-and-test-9/issue.md`
     - `docs/features/active/2026-04-05-refactor-and-test-9/spec.md`
     - `docs/features/active/2026-04-05-refactor-and-test-9/plan.2026-04-06T14-25.md`
   - Expected behavior:
     - No merge conflict markers remain.
     - `issue.md` keeps a single valid `- Work Mode: full-feature` marker.
     - The active plan’s checkbox state matches the evidence actually available on disk.
   - Acceptance criteria impacted:
     - AC-9 (`docs are updated coherently`)
     - AC-10 (`toolchain pass completed with required evidence`)
   - Verification:
     - `grep -n "<<<<<<<\|=======\|>>>>>>>" docs/features/active/2026-04-05-refactor-and-test-9/*`
     - Manual review of the resolved docs against the latest evidence files.

2. **Remove or split unrelated branch changes outside the scoped runtime refactor.**
   - Files:
     - `.codex/codex-web-setup.sh`
     - `AGENTS.md`
     - Any stale draft audit files that should not ship as part of this feature branch
   - Expected behavior:
     - The branch diff relative to `development` is limited to the runtime refactor, its tests, and the feature’s required docs/evidence.
   - Acceptance criteria impacted:
     - AC-4 (`structure matches the spec`)
     - AC-9 (`docs are updated coherently`)
   - Verification:
     - `git diff --name-status development...HEAD`

3. **Replace policy-forbidden temporary-file unit tests with deterministic seams or approved alternatives.**
   - Files:
     - `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`
     - Potentially `src/OpenClaw.MailBridge/BridgeApplication.cs` if a minimal seam is needed for testability
   - Expected behavior:
     - Unit tests no longer call `Path.GetTempPath`, `Directory.CreateDirectory`, `File.WriteAllTextAsync`, or `Directory.Delete`.
     - Tests remain deterministic and verify the same behaviors without touching the local filesystem.
   - Acceptance criteria impacted:
     - AC-5 (`invariants validated with tests or comparisons`)
     - AC-8 (`tests, linting, and type checks are clean`)
   - Verification:
     - `grep -n "GetTempPath\|Directory\.CreateDirectory\|File\.WriteAllTextAsync\|Directory\.Delete" tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`
     - `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`

4. **Raise targeted runtime-file coverage to satisfy the stated feature threshold.**
   - Files:
     - `src/OpenClaw.MailBridge/BridgeApplication.cs`
     - `src/OpenClaw.MailBridge/ComActiveObject.cs`
     - Any supporting test files needed to cover those paths
   - Expected behavior:
     - Each targeted runtime file reaches at least `80%` line coverage.
     - Prefer meeting the repo’s stronger new-module expectation where practical.
   - Acceptance criteria impacted:
     - AC-2 (`80%+` per targeted file)
     - AC-5 (`invariants validated with tests or comparisons`)
     - AC-10 (`toolchain pass completed with required evidence`)
   - Verification:
     - `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --collect:"XPlat Code Coverage" --results-directory TestResults/review-coverage`
     - Re-parse the generated Cobertura report and record per-file percentages for all targeted runtime files.

5. **Produce policy-grade coverage evidence with numeric baseline, post-change, and changed/new-code metrics.**
   - Files:
     - `docs/features/active/2026-04-05-refactor-and-test-9/evidence/baseline/*`
     - `docs/features/active/2026-04-05-refactor-and-test-9/evidence/qa-gates/*`
   - Expected behavior:
     - Evidence artifacts include exact commands, exit codes, timestamps, and numeric coverage values.
     - The feature no longer depends on a PASS decision without baseline/new-code coverage numbers.
   - Acceptance criteria impacted:
     - AC-10 (`toolchain pass completed with required evidence`)
   - Verification:
     - Inspect the evidence files for `Timestamp:`, `Command:`, `EXIT_CODE:`, and numeric coverage summaries.

6. **Re-run the final QA loop with approved C# commands on a host that provides the required tooling.**
   - Files:
     - Repository root / QA evidence artifacts
   - Expected behavior:
     - `csharpier` passes.
     - Analyzer and nullable builds pass using the repo-approved command surface.
     - The final test/coverage step is captured with the repo-preferred `vstest.console.exe ... /EnableCodeCoverage` command, or a documented repo-approved equivalent if the policy is updated.
   - Acceptance criteria impacted:
     - AC-8 (`tests, linting, and type checks are clean`)
     - AC-10 (`toolchain pass completed with required evidence`)
   - Verification:
     - `csharpier check .`
     - `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
     - `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
     - `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage`

## Do Not Do

- Do not weaken repo policy to allow temporary-file unit tests.
- Do not silently delete failing acceptance criteria or downgrade them to non-requirements.
- Do not keep unrelated `.codex` or generated-agent changes in this feature branch unless the feature docs are explicitly expanded to cover them.
- Do not mark coverage-related plan items complete unless the recorded evidence actually proves the required percentages.
- Do not resolve the doc conflicts by dropping the feature’s coverage requirement.

## Unmet Acceptance Criteria and Minimum Changes Required

| Criterion | Current state | Minimum change required |
|---|---|---|
| AC-2: Unit coverage demonstrates `80%+` per targeted file | **FAIL** | Add tests or seams so `BridgeApplication.cs` and `ComActiveObject.cs` reach at least `80%`, then record fresh numeric evidence. |
| AC-5: Invariants validated with tests or comparisons | **PARTIAL** | Remove filesystem-based unit tests and replace them with policy-compliant deterministic coverage for the same behaviors. |
| AC-8: Tests, linting, and type checks are clean | **PARTIAL** | Re-run the full QA loop after fixing test-policy violations and capture the approved command evidence. |
| AC-9: Docs are updated coherently | **FAIL** | Resolve merge conflict markers and reconcile the docs/plan with actual evidence. |
| AC-10: Toolchain pass completed with required evidence | **PARTIAL** | Capture final approved QA commands plus baseline/post/new-code coverage metrics in evidence artifacts. |