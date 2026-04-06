# 2026-04-05-wrong-target-environment (Plan)

- **Issue:** #4
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-05T20-40
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** minor-audit
- **Plan Scope:** Audit the MailBridge target-framework migration to `net10.0-windows`, capture baseline and verification evidence, and mirror the issue update for issue #4 without expanding scope beyond the target-environment defect.

### Phase 0 — Policy & Baseline
- [x] [P0-T1] Search for `.github/copilot-instructions.md` and record either the file contents or a negative-evidence claim with `SearchScope:`, `SearchPatterns:`, and `SearchResult:` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/copilot-instructions-check.2026-04-05T20-40.md`.
- [x] [P0-T2] Read `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/csharp-code-change.instructions.md`, and `.github/instructions/csharp-unit-test.instructions.md`, then record the applicable C# commands and constraints in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/policy-read.2026-04-05T20-40.md`.
- [x] [P0-T3] Record `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, and `git status --short` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/git-baseline.2026-04-05T20-40.md`.
- [x] [P0-T4] Record `dotnet --version` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/dotnet-version.2026-04-05T20-40.md`.
- [x] [P0-T5] Record `dotnet --list-sdks` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/dotnet-sdks.2026-04-05T20-40.md`.
- [x] [P0-T6] Record `dotnet --list-runtimes` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/baseline/dotnet-runtimes.2026-04-05T20-40.md`.

### Phase 1 — Target-Framework Audit
- [x] [P1-T1] Record the exact `<TargetFramework>` lines from `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`, `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj`, `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj`, and `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/target-framework-lines.2026-04-05T20-40.md`.
- [x] [P1-T2] Record a repo-wide search for `net8.0-windows`, `net8.0`, `.NET 8`, and `dotnet 8`; if the search is empty, include `SearchScope:`, `SearchPatterns:`, and `SearchResult:` fields in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/net8-search.2026-04-05T20-40.md`.

### Phase 2 — Targeted Verification Evidence
- [x] [P2-T1] Record a successful `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal` run in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/regression-testing/dotnet-test-success.2026-04-05T20-40.md`, and confirm the output references `net10.0-windows` rather than `net8.0-windows`.
- [x] [P2-T2] Record a successful `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` run in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/analyzer-build.2026-04-05T20-40.md`.

### Phase 3 — Final QA Loop
- [x] [P3-T1] Run `csharpier .` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/format.2026-04-05T20-40.md`; if formatting changes files, restart Phase 3 from this task.
- [x] [P3-T2] Run `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/analyzer-build-final.2026-04-05T20-40.md`; if the build fails, fix the issue and restart Phase 3 from [P3-T1].
- [x] [P3-T3] Run `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/nullable-build-final.2026-04-05T20-40.md`; if the build fails, fix the issue and restart Phase 3 from [P3-T1].
- [x] [P3-T4] Run `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` and record the final clean pass in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/qa-gates/vstest-final.2026-04-05T20-40.md`; if the test run fails, fix the issue and restart Phase 3 from [P3-T1].

### Phase 4 — End-State Evidence & Handoff
- [x] [P4-T1] Record `git status --short` and `git diff -- src tests docs/features/active/2026-04-05-wrong-target-environment-4` in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/other/end-state.2026-04-05T20-40.md`.
- [x] [P4-T2] Mirror the issue update for issue #4 in `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/issue-updates/issue-4.2026-04-05T20-40.md`, summarizing the retargeted projects, the verification commands, and any remaining environment/tooling caveats discovered during the audit.
