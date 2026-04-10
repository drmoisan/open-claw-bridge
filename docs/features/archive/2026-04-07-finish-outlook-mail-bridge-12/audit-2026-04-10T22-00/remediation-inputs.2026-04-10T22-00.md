# Remediation Inputs

- Timestamp: 2026-04-10T22-00
- Feature: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Triggering artifacts:
  - `policy-audit.2026-04-10T22-00.md`: 3 FAIL findings (file size violations), 1 FAIL (temp file usage)
  - `code-review.2026-04-10T22-00.md`: 3 Blocker, 2 Major findings
  - `feature-audit.2026-04-10T22-00.md`: NEEDS REVISION (blocked by structural violations)

---

## Required Fixes

### 1. Split `OutlookScanner.cs` to under 500 lines

- **File:** `src/OpenClaw.MailBridge/OutlookScanner.cs` (currently 580 lines)
- **Expected behavior:** Extract the COM reflection helper methods (`GetOptionalString`, `GetOptionalInt`, `GetOptionalBool`, `GetOptionalDateTimeOffset`, `SetMemberValue`, `InvokeMember`, `GetMemberValue`, `GetOptionalMemberValue`) into a new `internal static` class in a separate file (e.g., `OutlookComHelpers.cs`). The scanner must continue to function identically.
- **Acceptance criteria:**
  - `OutlookScanner.cs` is under 500 lines after extraction.
  - `OutlookComHelpers.cs` is under 500 lines.
  - All existing tests pass without modification.
  - No new public API surface is introduced (helpers must be `internal static`).
- **Verification commands:**
  - `csharpier check .`
  - `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true /p:Nullable=enable /p:TreatWarningsAsErrors=true`
  - `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`
  - Line count verification: both files under 500 lines.

### 2. Split `MailBridgeRuntimeTests.cs` to under 500 lines

- **File:** `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs` (currently 687 lines)
- **Expected behavior:** Move a cohesive subset of test methods (e.g., configuration validation tests or bridge state tests) into a new partial class file (e.g., `MailBridgeRuntimeTests.Config.cs` or `MailBridgeRuntimeTests.State.cs`). The partial class attribute already exists on `MailBridgeRuntimeTests`, so new files just need the matching partial class declaration.
- **Acceptance criteria:**
  - `MailBridgeRuntimeTests.cs` is under 500 lines after the split.
  - The new file is under 500 lines.
  - All existing tests pass without modification.
  - No test logic is changed; only file boundaries move.
- **Verification commands:**
  - `csharpier check .`
  - `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true /p:Nullable=enable /p:TreatWarningsAsErrors=true`
  - `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`
  - Line count verification: all partial files under 500 lines.

### 3. Split `MailBridgeRuntimeTests.OutlookScanner.cs` to under 500 lines

- **File:** `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs` (currently 652 lines)
- **Expected behavior:** Move calendar-related scanner tests into a new partial class file (e.g., `MailBridgeRuntimeTests.Calendar.cs`). The partial class attribute already exists on `MailBridgeRuntimeTests`, so new files just need the matching partial class declaration.
- **Acceptance criteria:**
  - `MailBridgeRuntimeTests.OutlookScanner.cs` is under 500 lines after the split.
  - The new file is under 500 lines.
  - All existing tests pass without modification.
  - No test logic is changed; only file boundaries move.
- **Verification commands:**
  - `csharpier check .`
  - `dotnet build OpenClaw.MailBridge.sln -c Debug /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true /p:Nullable=enable /p:TreatWarningsAsErrors=true`
  - `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build`
  - Line count verification: all partial files under 500 lines.

### 4. Address `CodexWebSetupScriptHarness.cs` temp file usage

- **File:** `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptHarness.cs` (line 16)
- **Expected behavior:** This harness tests a Codex `codex-web-setup.sh` bash script and requires filesystem interaction by nature. The recommended disposition is to add an explicit, narrowly scoped exception in `general-unit-test.instructions.md` for this specific test harness, documenting:
  - The harness name and purpose.
  - Why filesystem interaction is unavoidable (testing a bash setup script that modifies the filesystem).
  - That the exception is limited to `CodexWebSetupScriptHarness.cs` and `CodexWebSetupScriptTests.cs` only.
- **Acceptance criteria:**
  - Exception is documented in `general-unit-test.instructions.md` under the "Currently approved exceptions" subsection.
  - No other test files are added to the exception list.
- **Verification commands:**
  - Inspect `general-unit-test.instructions.md` for the new exception entry.

### 5. Correct feature-completion evidence artifact

- **File:** `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/feature-completion.2026-04-10T17-35.md`
- **Expected behavior:** Update the C# test count from 95 to 87 and the C# line coverage from 96.6% to 89.4% to match the verified `dotnet test` and coverage output.
- **Acceptance criteria:**
  - Feature-completion artifact reflects 87 C# tests and 89.4% line coverage.
  - No other content in the artifact is changed.
- **Verification commands:**
  - Inspect the artifact and compare against `dotnet test` output.

---

## Acceptance Criteria Not Yet Met

All 10 functional acceptance criteria from `user-story.md` evaluate as PASS. The remediation items above address **structural policy compliance** rather than functional AC gaps.

The feature audit verdict will remain NEEDS REVISION until the policy audit FAIL findings (file-size violations, temp-file usage) are resolved.

---

## Do Not Do

- Do NOT change any test assertions or test behavior. Only file boundaries should change.
- Do NOT refactor the scanner logic. Only extract static COM helper methods.
- Do NOT add new features or change API surfaces.
- Do NOT weaken policy constraints. The temp-file exception must be narrowly scoped.
- Do NOT modify the feature's functional behavior.
- Do NOT touch files that are not listed above.
