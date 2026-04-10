# Remediation Plan: finish-outlook-mail-bridge-12 (2026-04-10T22-00)

- **Issue:** #12
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-10
- **Status:** Approved
- **Version:** 1.0
- **Work Mode:** full-feature

## Overview

Remediate five structural policy violations identified in the feature review dated 2026-04-10T22-00. All ten functional acceptance criteria are met. The fixes are mechanical: split three oversized files (one production, two test), document one temp-file policy exception, and correct one evidence artifact. No behavioral changes are permitted.

## Requirements Sources

- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/remediation-inputs.2026-04-10T22-00.md` (PRIMARY)
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/spec.md` (secondary)
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/user-story.md` (secondary)

## Evidence Locations

- Remediation baseline: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/remediation-baseline/`
- QA gate evidence: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/`

## Execution Notes

- Use ISO-8601 minute timestamps in evidence filenames: `yyyy-MM-ddTHH-mm`.
- Every command-evidence artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
- No behavioral code changes are permitted. Only file split operations, policy documentation updates, and evidence corrections.
- This is a C#-only remediation pass. PowerShell QC is not in scope.

## Constraints (Do Not Do)

- Do NOT change any test assertions or test behavior. Only file boundaries should change.
- Do NOT refactor scanner logic. Only extract static COM helper methods.
- Do NOT add new features or change API surfaces.
- Do NOT modify the feature's functional behavior.
- Do NOT touch files that are not listed in the remediation inputs.
- The temp-file exception must be narrowly scoped to the two named files only.

---

### Phase 0 — Context & Inputs

- [x] [P0-T1] Read policy files in required order: (1) `.github/copilot-instructions.md`, (2) `.github/instructions/general-code-change.instructions.md`, (3) `.github/instructions/general-unit-test.instructions.md`, (4) `.github/instructions/csharp-code-change.instructions.md`, (5) `.github/instructions/csharp-unit-test.instructions.md`. Save evidence artifact to `evidence/remediation-baseline/phase0-instructions-read.<timestamp>.md` with fields `Timestamp:`, `Policy Order:`, and file list.
  - Acceptance: artifact exists, contains all five policy file paths, and fields are complete.

- [x] [P0-T2] Read the remediation inputs file `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/remediation-inputs.2026-04-10T22-00.md` and confirm all five required fixes are present and understood.
  - Acceptance: executor confirms five enumerated fixes match the remediation inputs.

- [x] [P0-T3] Capture remediation baseline: run `dotnet tool run csharpier --check .` from the workspace root. Save evidence artifact to `evidence/remediation-baseline/csharpier-check.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all required fields populated. EXIT_CODE is 0 (baseline formatting is clean).

- [x] [P0-T4] Capture remediation baseline: run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` from the workspace root. Save evidence artifact to `evidence/remediation-baseline/msbuild-analyzers.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all required fields populated. EXIT_CODE is 0 (baseline analyzers pass).

- [x] [P0-T5] Capture remediation baseline: run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` from the workspace root. Save evidence artifact to `evidence/remediation-baseline/msbuild-nullable.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all required fields populated. EXIT_CODE is 0 (baseline nullable analysis passes).

- [x] [P0-T6] Capture remediation baseline: run `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"` from the workspace root. Save evidence artifact to `evidence/remediation-baseline/dotnet-test-coverage.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (must include test count, pass/fail counts, and line coverage percentage).
  - Acceptance: artifact exists with all required fields populated. EXIT_CODE is 0, test count and coverage percentage are recorded.

- [x] [P0-T7] Capture remediation baseline: record line counts for all five oversized or affected files using `(Get-Content <file>).Count` or equivalent. Save evidence artifact to `evidence/remediation-baseline/line-counts.<timestamp>.md` with fields `Timestamp:`, `Command:`, `Output Summary:` listing each file path and its line count.
  - Files: `src/OpenClaw.MailBridge/OutlookScanner.cs`, `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`, `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs`.
  - Acceptance: artifact exists, all three file paths and line counts are recorded, and each exceeds 500 lines.

---

### Phase 1 — Remediation Fixes

**Fix 1: Extract COM helpers from OutlookScanner.cs**

- [x] [P1-T1] Create `src/OpenClaw.MailBridge/OutlookComHelpers.cs` containing an `internal static class OutlookComHelpers` with the eight COM reflection helper methods extracted from `OutlookScanner.cs`: `GetOptionalString`, `GetOptionalInt`, `GetOptionalBool`, `GetOptionalDateTimeOffset`, `SetMemberValue`, `InvokeMember`, `GetMemberValue`, `GetOptionalMemberValue`. Each method signature must remain identical; the class must be in the `OpenClaw.MailBridge` namespace.
  - Acceptance: `OutlookComHelpers.cs` exists, contains all eight methods, class is `internal static`, namespace is `OpenClaw.MailBridge`, and file is under 500 lines.

- [x] [P1-T2] Update `src/OpenClaw.MailBridge/OutlookScanner.cs` to remove the eight extracted methods and replace all internal call sites with calls to `OutlookComHelpers.<MethodName>(...)`. No behavioral change; only call-site delegation changes.
  - Acceptance: `OutlookScanner.cs` no longer contains the eight extracted method bodies, all call sites reference `OutlookComHelpers`, and the file is under 500 lines.

- [x] [P1-T3] Verify the OutlookScanner extraction compiles: run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug` from the workspace root. Confirm exit code 0.
  - Acceptance: build succeeds with exit code 0 and no new warnings or errors.

**Fix 2: Split MailBridgeRuntimeTests.cs**

- [x] [P1-T4] Identify a cohesive subset of test methods in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs` suitable for extraction (configuration validation tests, bridge state tests, or similar grouping). Record the selected method names and target file name (e.g., `MailBridgeRuntimeTests.Config.cs`) in a brief note before proceeding.
  - Acceptance: a list of method names and target file name is documented, and the remaining `MailBridgeRuntimeTests.cs` would be under 500 lines after extraction.

- [x] [P1-T5] Create `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Config.cs` (or the chosen partial class file name) containing the `public partial class MailBridgeRuntimeTests` declaration with the selected test methods moved from `MailBridgeRuntimeTests.cs`. Namespace and class declaration must match the existing partial class.
  - Acceptance: new file exists, contains the moved test methods, uses correct namespace and partial class declaration, and is under 500 lines.

- [x] [P1-T6] Remove the moved test methods from `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: `MailBridgeRuntimeTests.cs` no longer contains the moved methods and is under 500 lines.

**Fix 3: Split MailBridgeRuntimeTests.OutlookScanner.cs**

- [x] [P1-T7] Identify calendar-related scanner test methods in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs` suitable for extraction. Record the selected method names and target file name (e.g., `MailBridgeRuntimeTests.Calendar.cs`) in a brief note before proceeding.
  - Acceptance: a list of method names and target file name is documented, and the remaining `MailBridgeRuntimeTests.OutlookScanner.cs` would be under 500 lines after extraction.

- [x] [P1-T8] Create `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs` (or the chosen partial class file name) containing the `public partial class MailBridgeRuntimeTests` declaration with the selected calendar test methods moved from `MailBridgeRuntimeTests.OutlookScanner.cs`. Namespace and class declaration must match the existing partial class.
  - Acceptance: new file exists, contains the moved test methods, uses correct namespace and partial class declaration, and is under 500 lines.

- [x] [P1-T9] Remove the moved calendar test methods from `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs`.
  - Acceptance: `MailBridgeRuntimeTests.OutlookScanner.cs` no longer contains the moved methods and is under 500 lines.

**Fix 4: Document temp-file policy exception**

- [x] [P1-T10] Update `.github/instructions/general-unit-test.instructions.md` to replace the line `Currently approved exceptions: none.` with a narrowly scoped exception entry for `CodexWebSetupScriptHarness.cs` and `CodexWebSetupScriptTests.cs`. The entry must document: (a) the harness name and purpose (testing `codex-web-setup.sh` bash script), (b) why filesystem interaction is unavoidable (the script under test modifies the filesystem), and (c) that the exception is limited to those two files only.
  - Acceptance: `general-unit-test.instructions.md` contains the new exception entry under the "Currently approved exceptions" subsection, the entry names exactly two files, and no other files are added to the exception list.

**Fix 5: Correct feature-completion evidence artifact**

- [x] [P1-T11] Update `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/feature-completion.2026-04-10T17-35.md` to correct the C# test count from 95 to 87 and the C# line coverage from 96.6% to 89.4%. Also update the coverage thresholds line to reflect 89.4% as the post-change value. No other content in the artifact should change.
  - Acceptance: the artifact contains `87 tests` and `89.4% line coverage` in the C# test + coverage line. The coverage thresholds line reflects 89.4%. No other lines are modified.

---

### Phase 2 — Final QC Loop

- [x] [P2-T1] Run `dotnet tool run csharpier --check .` from the workspace root. Save evidence artifact to `evidence/qa-gates/csharpier-check.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. If any files need formatting, run `dotnet tool run csharpier .` to fix, then restart Phase 2 from P2-T1.
  - Acceptance: artifact exists, EXIT_CODE is 0, no files needed formatting changes.

- [x] [P2-T2] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` from the workspace root. Save evidence artifact to `evidence/qa-gates/msbuild-analyzers.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. If build fails, fix issues and restart Phase 2 from P2-T1.
  - Acceptance: artifact exists, EXIT_CODE is 0, zero analyzer warnings and zero errors.

- [x] [P2-T3] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` from the workspace root. Save evidence artifact to `evidence/qa-gates/msbuild-nullable.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. If build fails, fix issues and restart Phase 2 from P2-T1.
  - Acceptance: artifact exists, EXIT_CODE is 0, zero nullable warnings.

- [x] [P2-T4] Run `dotnet test OpenClaw.MailBridge.sln -c Debug --collect:"XPlat Code Coverage"` from the workspace root. Save evidence artifact to `evidence/qa-gates/dotnet-test-coverage.<timestamp>.md` with fields `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (must include test count, pass/fail counts, and line coverage percentage). If any test fails, fix issues and restart Phase 2 from P2-T1.
  - Acceptance: artifact exists, EXIT_CODE is 0, all tests pass, test count and coverage percentage are recorded. Coverage must not regress below the remediation baseline captured in P0-T6.

- [x] [P2-T5] Verify post-remediation line counts for all affected files using `(Get-Content <file>).Count` or equivalent. Save evidence artifact to `evidence/qa-gates/line-counts.<timestamp>.md` with fields `Timestamp:`, `Command:`, `Output Summary:` listing each file path and its line count.
  - Files to verify: `src/OpenClaw.MailBridge/OutlookScanner.cs`, `src/OpenClaw.MailBridge/OutlookComHelpers.cs`, `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`, `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Config.cs` (or chosen name), `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs`, `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs` (or chosen name).
  - Acceptance: artifact exists, every listed file is under 500 lines, and no file is missing from the count.

- [x] [P2-T6] Verify the temp-file exception in `.github/instructions/general-unit-test.instructions.md` names exactly `CodexWebSetupScriptHarness.cs` and `CodexWebSetupScriptTests.cs` and no other files.
  - Acceptance: the exception entry exists, names exactly two files, and the rest of the policy document is unchanged.

- [x] [P2-T7] Verify `evidence/qa-gates/feature-completion.2026-04-10T17-35.md` reports 87 C# tests and 89.4% line coverage.
  - Acceptance: the artifact contains the corrected values and no other content was changed.
