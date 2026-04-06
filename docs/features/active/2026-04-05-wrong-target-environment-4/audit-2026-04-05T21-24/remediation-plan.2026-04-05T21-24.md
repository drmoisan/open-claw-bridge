# wrong-target-environment (Remediation Plan)

- **Issue:** #4
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-05T21-24
- **Status:** Complete
- **Version:** 0.1
- **Work Mode:** minor-audit
- **Plan Scope:** Migrate the touched MailBridge test project from NUnit to MSTest, preserve the existing test scenarios and the `DOTNET_*` harness isolation, and verify the repo still passes formatter, build, nullable, and test gates on `net10.0-windows`.

### Phase 0 — Policy & Baseline
- [x] [P0-T1] Search for `.github/copilot-instructions.md` and record either the file contents or a negative-evidence claim with `SearchScope:`, `SearchPatterns:`, and `SearchResult:` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/copilot-instructions-check.2026-04-05T21-24.md`.
- [x] [P0-T2] Read `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/csharp-code-change.instructions.md`, and `.github/instructions/csharp-unit-test.instructions.md`, then record the applicable policy notes and required commands in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/policy-read.2026-04-05T21-24.md`.
- [x] [P0-T3] Record the current branch and HEAD commit in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/git-baseline.2026-04-05T21-24.md`.
- [x] [P0-T4] Record `git status --short` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/git-baseline.2026-04-05T21-24.md`.
- [x] [P0-T5] Record `dotnet --version` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/dotnet-version.2026-04-05T21-24.md`.
- [x] [P0-T6] Record `dotnet --list-sdks` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/dotnet-sdks.2026-04-05T21-24.md`.
- [x] [P0-T7] Record `dotnet --list-runtimes` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/dotnet-runtimes.2026-04-05T21-24.md`.

### Phase 1 — Current Test Stack Audit
- [x] [P1-T1] Record the current NUnit and test-framework package references from `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/current-test-stack.2026-04-05T21-24.md`.
- [x] [P1-T2] Record the current NUnit attributes and namespace imports in `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/current-test-attributes-codexweb.2026-04-05T21-24.md`.
- [x] [P1-T3] Record the current NUnit attributes and namespace imports in `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/current-test-attributes-mailbridge.2026-04-05T21-24.md`.

### Phase 2 — MSTest Migration
- [x] [P2-T1] Replace the NUnit test packages in `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` with MSTest packages while keeping `Microsoft.NET.Test.Sdk` and the existing project reference intact.
- [x] [P2-T2] Convert `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs` from NUnit to MSTest by changing the test namespace import and attributes, while preserving all scenarios, assertions, and the `DOTNET_*` harness isolation behavior.
- [x] [P2-T3] Convert `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs` from NUnit to MSTest by changing the test namespace import and attributes while preserving the existing assertions.

### Phase 3 — Verification Loop
- [x] [P3-T1] Run `csharpier .` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/format.2026-04-05T21-24.md`; if formatting changes files, restart Phase 3 from this task.
- [x] [P3-T2] Run `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/analyzer-build.2026-04-05T21-24.md`; if the build fails, fix the issue and restart Phase 3 from [P3-T1].
- [x] [P3-T3] Run `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/nullable-build.2026-04-05T21-24.md`; if the build fails, fix the issue and restart Phase 3 from [P3-T1].
- [x] [P3-T4] Run `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/regression-testing/dotnet-test.2026-04-05T21-24.md`; if the test run fails, fix the issue and restart Phase 3 from [P3-T1].
- [x] [P3-T5] Run `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/vstest.2026-04-05T21-24.md`; if the test run fails, fix the issue and restart Phase 3 from [P3-T1].

### Phase 4 — End-State Evidence & Handoff
- [x] [P4-T1] Record `git status --short` and `git diff -- src tests docs/features/active/2026-04-05-wrong-target-environment-4` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/end-state.2026-04-05T21-24.md`.
- [x] [P4-T2] Mirror the remediation outcome for issue #4 in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/issue-updates/issue-4.2026-04-05T21-24.md`, summarizing the MSTest migration, verification commands, and any residual caveats.
