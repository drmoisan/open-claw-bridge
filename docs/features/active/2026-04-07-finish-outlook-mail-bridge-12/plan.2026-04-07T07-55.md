# 2026-04-07-finish-outlook-mail-bridge - Plan

- **Issue:** #12
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-04-07
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature

## Overview

Complete the existing Outlook bridge without changing topology: retarget the runtime, contracts, client, and tests to `net8.0-windows`; finish the cache-backed Inbox and Calendar pipeline; complete the named-pipe/client/script surfaces; and capture both deterministic repo evidence and Windows operator acceptance evidence. The executor must treat `spec.md` and `user-story.md` as the primary acceptance-criteria sources, with `issue.md` supplying additional context, constraints, and risk language.

## Requirements Sources

- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/spec.md`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/user-story.md`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/issue.md`
- `artifacts/research/20260407-finish-outlook-mail-bridge-implementation-research.md`

## Evidence Locations

- Baseline evidence: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/`
- Regression evidence: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/regression-testing/`
- Other implementation evidence: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/`
- QA gate evidence: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/`

## Execution Notes

- Use ISO-8601 minute timestamps in evidence filenames: `yyyy-MM-ddTHH-mm`.
- Every command-evidence artifact must contain `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
- C# baseline and final coverage artifacts must record numeric `OverallLineCoverage:` and `ChangedOrNewLineCoverage:` values.
- Deterministic repo validation covers pure logic, repository behavior, RPC validation, client behavior, script harness behavior, and QA loops.
- Real Outlook COM verification, scheduled-task topology validation, and `openclaw-svc` pipe access verification remain Windows/operator acceptance work; those tasks are mandatory for feature completion and must be evidenced separately rather than waived.
- 2026-04-07 follow-up PowerShell coverage remediation stays in direct-mode scope: repair `scripts/powershell/PoshQC/PoshQC.psm1` coverage summary translation and replace text-only Pester assertions with script-executing coverage tests for changed root scripts.

### Phase 0 — Context & Inputs

- [x] [P0-T1] Attempt to read/check `.github/copilot-instructions.md`; when it is absent, continue by reading `.github/instructions/general-code-change.instructions.md`, `.github/instructions/general-unit-test.instructions.md`, `.github/instructions/csharp-code-change.instructions.md`, `.github/instructions/csharp-unit-test.instructions.md`, `.github/instructions/powershell-code-change.instructions.md`, and `.github/instructions/powershell-unit-test.instructions.md` in that order, then record the read order in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/phase0-instructions-read.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Policy Order:`, and `Files Read:` entries, and `Policy Order:` begins with `.github/copilot-instructions.md (absent)`, followed by the six files above in the exact order listed in the task text.
- [x] [P0-T2] Map every acceptance criterion from `spec.md` and `user-story.md` plus the issue-only topology constraints to concrete implementation files in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/requirements-traceability.<timestamp>.md`.
  - Acceptance: The artifact exists and contains exact headings `Framework Retarget`, `COM Boundary`, `Inbox Scanning`, `Calendar Scanning`, `Privacy Shaping`, `Repository`, `Pipe ACL`, `Client CLI`, `Scripts And Runbook`, and `Test Expansion`.
- [x] [P0-T3] Run `csharpier .` from the repo root and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/csharpier-check.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Command: csharpier .` plus the required evidence fields.
- [x] [P0-T4] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/msbuild-analyzers.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command plus the required evidence fields.
- [x] [P0-T5] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/msbuild-nullable.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command plus the required evidence fields.
- [x] [P0-T6] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$assembly = Get-ChildItem -Path 'tests/OpenClaw.MailBridge.Tests/bin/Debug' -Filter 'OpenClaw.MailBridge.Tests.dll' -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $assembly) { throw 'OpenClaw.MailBridge.Tests.dll not found under tests/OpenClaw.MailBridge.Tests/bin/Debug'; }; dotnet-coverage collect \"vstest.console.exe '$($assembly.FullName)' /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/baseline-csharp\" -f cobertura -o 'TestResults/baseline-csharp/coverage.cobertura.xml'"` and save the command result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/coverage.<timestamp>.md`.
  - Acceptance: The artifact exists, contains the exact command, and its `Output Summary:` includes numeric `OverallLineCoverage:` and the generated coverage report path.
- [x] [P0-T7] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$base = git merge-base HEAD main; $report = Get-ChildItem -Path 'TestResults/baseline-csharp' -Filter coverage.cobertura.xml -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $report) { throw 'coverage.cobertura.xml not found under TestResults/baseline-csharp'; }; [xml]$xml = Get-Content -Path $report.FullName; $overall = [math]::Round(([double]$xml.coverage.'line-rate') * 100, 2); $file = ''; $changed = @(); foreach ($line in (git diff --unified=0 $base -- '*.cs')) { if ($line -match '^\+\+\+ b/(?<path>.+)$') { $file = $Matches.path; continue }; if ($line -match '^@@ -\d+(?:,\d+)? \+(?<start>\d+)(?:,(?<count>\d+))? @@') { $count = if ($Matches.count) { [int]$Matches.count } else { 1 }; foreach ($offset in 0..($count - 1)) { $changed += [pscustomobject]@{ File = $file; Line = ([int]$Matches.start) + $offset } } } }; $totalChanged = 0; $hitChanged = 0; foreach ($item in $changed) { $class = $xml.coverage.packages.package.classes.class | Where-Object { $_.filename -eq $item.File -or $_.filename -like ('*' + $item.File) } | Select-Object -First 1; if ($class) { $covLine = $class.lines.line | Where-Object { [int]$_.number -eq $item.Line } | Select-Object -First 1; if ($covLine) { $totalChanged++; if ([int]$covLine.hits -gt 0) { $hitChanged++ } } } }; $changedPct = if ($totalChanged -gt 0) { [math]::Round(($hitChanged / $totalChanged) * 100, 2) } else { 100.0 }; Write-Output \"BaselineOverallLineCoverage: $overall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"CoverageReportPath: $($report.FullName)\""` and save the parsed summary to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/coverage-summary.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`, numeric `BaselineOverallLineCoverage:`, numeric `ChangedOrNewLineCoverage:`, and the parsed coverage report path.
- [x] [P0-T8] Record the pre-bootstrap PowerShell QA status in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/powershell-bootstrap-status.<timestamp>.md`.
  - Acceptance: The artifact exists and records whether `scripts/powershell/PoshQC` existed before bootstrap.
- [x] [P0-T9] Create `scripts/powershell/PoshQC/PoshQC.psm1` and `scripts/powershell/PoshQC/PoshQC.psd1` with wrapper functions `Install-PoshQCTools`, `Invoke-PoshQCFormat`, `Invoke-PoshQCAnalyze`, and `Invoke-PoshQCTest` that proxy the policy-approved direct QA commands and allow the test wrapper to emit settings-backed coverage artifacts via optional `-CoveragePaths`, `-TestResultOutputPath`, and `-CoverageOutputPath` parameters.
  - Acceptance: The two module files exist, export the four wrapper functions above, and `Invoke-PoshQCTest` accepts the optional coverage-output parameters required by [P0-T14] and [P7-T10].
- [x] [P0-T10] Add `scripts/powershell/PoshQC/settings/pester.runsettings.psd1` and verify `Import-Module ./scripts/powershell/PoshQC` succeeds.
  - Acceptance: The settings file exists, `Import-Module ./scripts/powershell/PoshQC` succeeds, and, per the explicit user instruction to relax the Phase 0 baseline expectation, the later Phase 0 PowerShell command artifacts are treated as post-bootstrap baseline evidence for this feature.
- [x] [P0-T11] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCFormat -Root ."` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/powershell-format.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command plus the required evidence fields.
- [x] [P0-T12] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCAnalyze -Root ."` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/powershell-analyze.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command plus the required evidence fields.
- [x] [P0-T13] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root ."` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/powershell-test.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command plus the required evidence fields.
- [x] [P0-T14] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root . -CoveragePaths @('./scripts/*.ps1') -TestResultOutputPath 'TestResults/baseline-powershell/testResults.xml' -CoverageOutputPath 'TestResults/baseline-powershell/coverage.json'"` using the repo-local `scripts/powershell/PoshQC` bootstrap from [P0-T10] and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/powershell-coverage.<timestamp>.md`.
  - Acceptance: The artifact exists under `evidence/baseline/`, contains the exact command plus the required evidence fields, and its `Output Summary:` includes numeric `OverallLineCoverage:` plus the generated PowerShell coverage artifact paths `TestResults/baseline-powershell/testResults.xml` and `TestResults/baseline-powershell/coverage.json`.
- [x] [P0-T15] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$base = git merge-base HEAD main; $coverage = Get-Content 'TestResults/baseline-powershell/coverage.json' -Raw | ConvertFrom-Json; $overall = [math]::Round([double]$coverage.OverallLineCoverage, 2); $changedScripts = @(git diff --name-only $base -- 'scripts/*.ps1' | Where-Object { $_ }); $changedTotal = 0; $changedHit = 0; foreach ($path in $changedScripts) { $file = $coverage.Files | Where-Object { $_.Path -eq $path -or $_.Path -like ('*' + $path) } | Select-Object -First 1; if ($file) { foreach ($line in $file.Lines) { $changedTotal++; if ([bool]$line.Hit) { $changedHit++ } } } }; $changedPct = if ($changedTotal -gt 0) { [math]::Round(($changedHit / $changedTotal) * 100, 2) } else { 100.0 }; Write-Output \"BaselineOverallLineCoverage: $overall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"CoverageArtifactPath: TestResults/baseline-powershell/coverage.json\""` and save the parsed summary to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline/powershell-coverage-summary.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`, numeric `BaselineOverallLineCoverage:`, numeric `ChangedOrNewLineCoverage:`, and the parsed PowerShell coverage artifact path.

### Phase 1 — Framework Retargeting And Verification Harness

- [x] [P1-T1] Update `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj` so `TargetFramework` is `net8.0-windows`.
  - Acceptance: The file contains the exact line `<TargetFramework>net8.0-windows</TargetFramework>`.
- [x] [P1-T2] Update `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj` so `TargetFramework` is `net8.0-windows`.
  - Acceptance: The file contains the exact line `<TargetFramework>net8.0-windows</TargetFramework>`.
- [x] [P1-T3] Update `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj` so `TargetFramework` is `net8.0-windows`.
  - Acceptance: The file contains the exact line `<TargetFramework>net8.0-windows</TargetFramework>`.
- [x] [P1-T4] Update `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` so `TargetFramework` is `net8.0-windows`.
  - Acceptance: The file contains the exact line `<TargetFramework>net8.0-windows</TargetFramework>`.

### Phase 2 — COM Boundary And Scan Scheduling

- [x] [P2-T1] Refactor `src/OpenClaw.MailBridge/OutlookScanner.cs` to acquire Outlook by attaching to a running instance first, then creating and logging on only when `AutostartOutlook` permits.
  - Acceptance: `OutlookScanner.cs` contains a dedicated acquisition path that references both `TryGet("Outlook.Application")` and `CreateAndLogonOutlook()` without performing either call outside the scanner’s COM orchestration flow.
- [x] [P2-T2] Update `src/OpenClaw.MailBridge/OutlookScanner.cs` to resolve the default Inbox and Calendar folders before scanning and to set explicit bridge state when either folder is unavailable.
  - Acceptance: `OutlookScanner.cs` contains distinct default-folder resolution logic for Inbox and Calendar plus explicit state transitions for unavailable-folder cases.
- [x] [P2-T3] Add COM-release helpers in `src/OpenClaw.MailBridge/ComActiveObject.cs` or a new internal helper file under `src/OpenClaw.MailBridge/` so application, namespace, folder, and item references are released on successful scan paths.
  - Acceptance: The implementation contains a dedicated release helper used by Outlook scan code and no scan path leaves raw COM release calls duplicated inline more than once.
- [x] [P2-T4] Add failure-path COM cleanup and stale-cache transitions in `src/OpenClaw.MailBridge/OutlookScanner.cs` and `src/OpenClaw.MailBridge/BridgeStateStore.cs`.
  - Acceptance: The runtime sets explicit stale-cache state after scan failure and releases acquired COM references before the failure path returns.
- [x] [P2-T5] Implement the Inbox `ReceivedTime` filter builder in `src/OpenClaw.MailBridge/OutlookScanner.cs` using `last_inbox_scan_utc` minus `InboxOverlapMinutes`.
  - Acceptance: `OutlookScanner.cs` contains a filter path that references both the last successful Inbox scan value and `InboxOverlapMinutes` before calling `Restrict`.
- [x] [P2-T6] Persist normalized default-Inbox `MailItem` and `MeetingItem` rows through `src/OpenClaw.MailBridge/CacheRepository.cs`, deduping on the stable Outlook item identity.
  - Acceptance: The scanner contains separate normalization branches for `MailItem` and `MeetingItem`, and the repository path upserts existing rows instead of inserting duplicate rows for the same item identity.
- [x] [P2-T7] Implement bounded default-Calendar enumeration in `src/OpenClaw.MailBridge/OutlookScanner.cs` using `Sort("[Start]")`, `IncludeRecurrences = true`, a start/end filter, and a hard item cap without calling `.Count` on the recurring view.
  - Acceptance: `OutlookScanner.cs` contains the exact recurring-calendar sequence `Sort("[Start]")`, `IncludeRecurrences = true`, and a bounded filter path, and it does not contain `.Count` against the recurring items view.
- [x] [P2-T8] Update `src/OpenClaw.MailBridge/ScanWorker.cs` so Inbox and Calendar polling cadences are tracked independently through `InboxPollSeconds` and `CalendarPollSeconds` while every Outlook operation still runs inside `IOutlookStaExecutor`.
  - Acceptance: `ScanWorker.cs` references both poll settings, and direct scanner calls remain inside `sta.InvokeAsync(...)`.

### Phase 3 — Repository, Privacy, RPC, And Client Completion

- [x] [P3-T1] Extend `src/OpenClaw.MailBridge/CacheRepository.cs` with message upsert and lookup methods for `list_recent_messages`, `list_recent_meeting_requests`, and `get_message`.
  - Acceptance: `CacheRepository.cs` contains distinct query paths for message list and single-message lookup plus write paths for message upserts.
- [x] [P3-T2] Extend `src/OpenClaw.MailBridge/CacheRepository.cs` with event upsert and lookup methods for `list_calendar_window` and `get_event`.
  - Acceptance: `CacheRepository.cs` contains distinct query paths for calendar-window list and single-event lookup plus write paths for event upserts.
- [x] [P3-T3] Complete stale-cache and scan-state repository helpers in `src/OpenClaw.MailBridge/CacheRepository.cs` so cached message and event responses can report deterministic freshness state.
  - Acceptance: `CacheRepository.cs` exposes scan-state reads needed for status and stale-cache calculations beyond simple timestamp touch operations.
- [x] [P3-T4] Implement safe-mode response shaping in `src/OpenClaw.MailBridge/OutlookScanner.cs` or a new helper file under `src/OpenClaw.MailBridge/` so protected fields are suppressed before message and event rows are returned.
  - Acceptance: The shaping code explicitly suppresses `body_preview`, `sender_name`, and `sender_email` for safe mode.
- [x] [P3-T5] Implement enhanced-mode response shaping in `src/OpenClaw.MailBridge/OutlookScanner.cs` or a new helper file under `src/OpenClaw.MailBridge/` so preview data is sanitized and truncated through `BodySanitizer` before it is returned.
  - Acceptance: The enhanced shaping code calls `BodySanitizer` and respects `BodyPreviewMaxChars` before returning preview data.
- [x] [P3-T6] Replace content-bearing runtime logs with summary-only logs in the bridge runtime files so message bodies, event bodies, and attachment content are never written to logs.
  - Acceptance: No runtime log statement in `src/OpenClaw.MailBridge/` contains raw body or attachment payload fields, and the scan path still logs counts or state transitions.
- [x] [P3-T7] Implement repository-backed handlers for `list_recent_messages` and `list_recent_meeting_requests` in `src/OpenClaw.MailBridge/PipeRpcWorker.cs`.
  - Acceptance: `PipeRpcWorker.cs` contains explicit branches for both methods that query the repository instead of returning placeholder empty arrays.
- [x] [P3-T8] Implement repository-backed handlers for `get_message`, `list_calendar_window`, and `get_event` in `src/OpenClaw.MailBridge/PipeRpcWorker.cs`.
  - Acceptance: `PipeRpcWorker.cs` contains explicit branches for the three methods above that query the repository instead of returning placeholder empty arrays.
- [x] [P3-T9] Add deterministic request-validation helpers in `src/OpenClaw.MailBridge/PipeRpcWorker.cs` for ISO-8601 parsing, `start < end`, limit bounds, and bridge-ID decoding.
  - Acceptance: `PipeRpcWorker.cs` contains dedicated validation logic for timestamps, ranges, limits, and IDs before repository access occurs.
- [x] [P3-T10] Enforce required named-pipe ACL grants and hard-fail startup in `src/OpenClaw.MailBridge/PipeRpcWorker.cs` when `SYSTEM`, Administrators, the primary interactive user, or `openclaw-svc` cannot be resolved.
  - Acceptance: `PipeRpcWorker.cs` no longer swallows `IdentityNotMappedException` for `openclaw-svc`, and pipe startup still applies a deny rule for `NETWORK`.
- [x] [P3-T11] Update `src/OpenClaw.MailBridge.Client/Program.cs` so the client resolves the pipe name from `%LOCALAPPDATA%\OpenClaw\MailBridge\bridge.settings.json` and accepts an optional `--pipe-name` override.
  - Acceptance: `Program.cs` contains a settings-backed pipe-name resolution path and recognizes the exact flag `--pipe-name`.
- [x] [P3-T12] Update `src/OpenClaw.MailBridge.Client/Program.cs` so supported commands preserve JSON-only stdout, stderr-only diagnostics, and deterministic exit-code mapping for bridge-side failures.
  - Acceptance: `Program.cs` keeps `Console.Out.WriteLine(...)` for serialized responses, writes diagnostics to `Console.Error`, and contains explicit exit-code mapping for bridge errors.

### Phase 4 — Scripts And Runbook Completion

- [x] [P4-T1] Update `scripts/install-mailbridge.ps1` to validate Outlook/profile prerequisites, seed safe-mode defaults, and fail explicitly when installation preflight checks do not pass.
  - Acceptance: The script contains explicit preflight checks for Outlook/profile prerequisites and seeds `mode` as `safe` in the default settings payload.
- [x] [P4-T2] Update `scripts/register-mailbridge-task.ps1` to preserve the on-logon interactive task contract and fail explicitly when registration prerequisites are missing.
  - Acceptance: The script still issues an on-logon interactive registration path and contains an explicit failure path for missing registration prerequisites.
- [x] [P4-T3] Update `scripts/uninstall-mailbridge.ps1` so it removes the registered task deterministically and documents its intentionally narrow cleanup scope.
  - Acceptance: The script contains a deterministic scheduled-task removal path and does not silently delete cache or settings artifacts beyond the documented scope.
- [x] [P4-T4] Update suite A in `scripts/test-mailbridge.ps1` so it waits for bridge readiness and exits non-zero when readiness is not reached within the documented deadline.
  - Acceptance: The script contains a readiness deadline path and throws or exits when `status.result.state` never becomes `ready`.
- [x] [P4-T5] Update suite B in `scripts/test-mailbridge.ps1` so it asserts cache-backed message list/get command behavior instead of accepting placeholder success payloads.
  - Acceptance: The script asserts message list/get command behavior and does not treat placeholder empty message results as success when cached message data is expected.
- [x] [P4-T6] Update suite C in `scripts/test-mailbridge.ps1` so it asserts cache-backed calendar list/get command behavior instead of accepting placeholder success payloads.
  - Acceptance: The script asserts calendar list/get command behavior and does not treat placeholder empty calendar results as success when cached calendar data is expected.
- [x] [P4-T7] Update suite D in `scripts/test-mailbridge.ps1` so safe-mode privacy checks fail deterministically without relying on manual inspection.
  - Acceptance: The script contains explicit failure checks for safe-mode protected-field leaks.
- [x] [P4-T8] Update suite F in `scripts/test-mailbridge.ps1` so repeated-request hygiene checks fail deterministically without relying on manual inspection.
  - Acceptance: The script contains explicit failure checks for repeated-request errors in the hygiene loop.
- [x] [P4-T9] Update suite E in `scripts/test-mailbridge.ps1` so it emits explicit operator evidence fields for interactive-session validation, `openclaw-svc` access, and `NETWORK` deny verification.
  - Acceptance: The script writes the exact keys `PrimaryInteractiveSession`, `OpenClawSvcPipeConnect`, and `NetworkDenyVerified` into its operator evidence output path.
- [x] [P4-T10] Update `docs/mailbridge-runbook.md` so install, config, troubleshooting, and acceptance guidance match the finished bridge behavior and the automated/operator evidence split.
  - Acceptance: The runbook contains sections for `safe` mode defaults, `enhanced` mode caveats, scripted acceptance suites, and operator-only validation steps.
- [x] [P4-T11] Update `README.md` so the top-level bridge documentation matches the finished implementation and points operators to the detailed runbook.
  - Acceptance: `README.md` contains coverage of `net8.0-windows`, cache-backed non-status RPC completion, `safe` versus `enhanced` behavior, the scripted/operator acceptance split, and a reference to `docs/mailbridge-runbook.md`.

### Phase 5 — Deterministic Test Expansion

- [x] [P5-T1] Add an MSTest covering `OutlookScanner.EnsureOutlook` returning `waiting_for_outlook` when Outlook is absent and `AutostartOutlook` is `false` in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose name references `EnsureOutlook`, `AutostartOutlook`, and `waiting_for_outlook`.
- [x] [P5-T2] Add an MSTest covering `OutlookScanner.ScanAsync` setting stale-cache state after a scan failure in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions include `CacheStale` and `StaleReason`.
- [x] [P5-T3] Add an MSTest covering `ScanWorker.ExecuteAsync` honoring separate Inbox and Calendar cadences while invoking the STA executor in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions mention both poll settings and executor invocation behavior.
- [x] [P5-T4] Add an MSTest covering `CacheRepository` upserting a message row by stable item identity in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new repository test whose assertions verify one logical row remains after an update path.
- [x] [P5-T5] Add an MSTest covering `CacheRepository` returning calendar-window rows in deterministic order with limit enforcement in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new repository test whose assertions verify ordering and limit behavior.
- [x] [P5-T6] Add an MSTest covering `PipeRpcWorker.BuildResponseAsync` returning `INVALID_REQUEST` for malformed JSON in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions reference `BridgeErrorCodes.InvalidRequest` for malformed JSON.
- [x] [P5-T7] Add an MSTest covering `PipeRpcWorker.Handle` returning `INVALID_REQUEST` for invalid calendar-window ranges or out-of-range limits in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions reference `BridgeErrorCodes.InvalidRequest` for range or limit validation.
- [x] [P5-T8] Add an MSTest covering `PipeRpcWorker.Handle` returning `NOT_FOUND` for an unknown bridge ID in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions reference `BridgeErrorCodes.NotFound`.
- [x] [P5-T9] Add an MSTest covering `PipeRpcWorker.BuildPipeSecurity` failing when `openclaw-svc` SID resolution cannot be completed in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions verify failure instead of silent ACL downgrade.
- [x] [P5-T10] Add an MSTest covering safe-mode message shaping suppressing `body_preview`, `sender_name`, and `sender_email` in `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions verify the three protected fields are absent or null in safe mode.
- [x] [P5-T11] Add an MSTest covering enhanced-mode message shaping using sanitized and truncated preview text in `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs`.
  - Acceptance: The test file contains a new test method whose assertions verify preview sanitization and truncation.
- [x] [P5-T12] Add an MSTest covering `OpenClaw.MailBridge.Client.Program` preferring `--pipe-name` over settings fallback in `tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs`.
  - Acceptance: The file `tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs` exists and contains a test method whose assertions verify override precedence.
- [x] [P5-T13] Add an MSTest covering client exit-code mapping plus stdout/stderr separation in `tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs`.
  - Acceptance: `MailBridgeClientTests.cs` contains a test method whose assertions verify JSON output stays on stdout and diagnostics stay on stderr.
- [x] [P5-T14] Add a Pester test covering `register-mailbridge-task.ps1` preserving `/sc onlogon /it` registration semantics in `tests/scripts/register-mailbridge-task.Tests.ps1`.
  - Acceptance: The file exists and contains a Pester `It` block whose assertions verify both `/sc onlogon` and `/it` appear in the scheduled-task command.
- [x] [P5-T15] Add a Pester test covering `install-mailbridge.ps1` seeding safe-mode defaults when the settings file is absent in `tests/scripts/install-mailbridge.Tests.ps1`.
  - Acceptance: The file exists and contains a Pester `It` block whose assertions verify the seeded config contains `"mode": "safe"`.
- [x] [P5-T16] Add a Pester test covering `test-mailbridge.ps1` failing when safe mode leaks protected message fields in `tests/scripts/test-mailbridge.Tests.ps1`.
  - Acceptance: The file exists and contains a Pester `It` block whose assertions verify the script throws or exits non-zero when `body_preview`, `sender_name`, or `sender_email` is present.
- [x] [P5-T17] Add a Pester test covering `test-mailbridge.ps1` emitting suite-E operator evidence keys in `tests/scripts/test-mailbridge.Tests.ps1`.
  - Acceptance: The file contains a Pester `It` block whose assertions verify the output contains `PrimaryInteractiveSession`, `OpenClawSvcPipeConnect`, and `NetworkDenyVerified`.
- [x] [P5-T18] Add a Pester test covering `uninstall-mailbridge.ps1` removing the configured scheduled-task name in `tests/scripts/uninstall-mailbridge.Tests.ps1`.
  - Acceptance: The file exists and contains a Pester `It` block whose assertions verify the uninstall path targets the provided task name.

### Phase 6 — Windows Operator Acceptance Evidence

- [ ] [P6-T1] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-mailbridge.ps1` on a Windows machine with classic Outlook and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/windows-acceptance.<timestamp>.md`.
  - Preconditions: The bridge is installed, the operator is signed into the primary interactive session, Outlook has a configured profile, and the scheduled task is registered.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, and `AutomatedSuitesPassed: A,B,C,D,F`.
- [ ] [P6-T2] Record operator validation of suite E in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/windows-operator-validation.<timestamp>.md`.
  - Preconditions: [P6-T1] completed and the operator can inspect bridge logs or ACL output on the target Windows machine.
  - Acceptance: The artifact exists and contains the exact lines `PrimaryInteractiveSession: true`, `OpenClawSvcPipeConnect: true`, `NetworkDenyVerified: true`, and `EvidenceRefs:`.

### Phase 7 — Final QC Loop And Completion Evidence

- [x] [P7-T1] Run `csharpier .` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/csharpier-check.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Command: csharpier .`; if the formatter changes files, record the change, fix any follow-on issues, and restart Phase 7 from [P7-T1].
- [x] [P7-T2] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/msbuild-analyzers.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command; if the command fails, fix the issue and restart Phase 7 from [P7-T1].
- [x] [P7-T3] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/msbuild-nullable.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command; if the command fails, fix the issue and restart Phase 7 from [P7-T1].
- [x] [P7-T4] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$assembly = Get-ChildItem -Path 'tests/OpenClaw.MailBridge.Tests/bin/Debug' -Filter 'OpenClaw.MailBridge.Tests.dll' -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $assembly) { throw 'OpenClaw.MailBridge.Tests.dll not found under tests/OpenClaw.MailBridge.Tests/bin/Debug'; }; dotnet-coverage collect \"vstest.console.exe '$($assembly.FullName)' /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/qa-csharp\" -f cobertura -o 'TestResults/qa-csharp/coverage.cobertura.xml'"` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/coverage.<timestamp>.md`.
  - Acceptance: The artifact exists, contains the exact command, and its `Output Summary:` includes numeric `OverallLineCoverage:` plus the generated coverage report path; if the command fails, fix the issue and restart Phase 7 from [P7-T1].
- [x] [P7-T5] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$base = git merge-base HEAD main; $report = Get-ChildItem -Path 'TestResults/qa-csharp' -Filter coverage.cobertura.xml -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $report) { throw 'coverage.cobertura.xml not found under TestResults/qa-csharp'; }; [xml]$xml = Get-Content -Path $report.FullName; $overall = [math]::Round(([double]$xml.coverage.'line-rate') * 100, 2); $file = ''; $changed = @(); foreach ($line in (git diff --unified=0 $base -- '*.cs')) { if ($line -match '^\+\+\+ b/(?<path>.+)$') { $file = $Matches.path; continue }; if ($line -match '^@@ -\d+(?:,\d+)? \+(?<start>\d+)(?:,(?<count>\d+))? @@') { $count = if ($Matches.count) { [int]$Matches.count } else { 1 }; foreach ($offset in 0..($count - 1)) { $changed += [pscustomobject]@{ File = $file; Line = ([int]$Matches.start) + $offset } } } }; $totalChanged = 0; $hitChanged = 0; foreach ($item in $changed) { $class = $xml.coverage.packages.package.classes.class | Where-Object { $_.filename -eq $item.File -or $_.filename -like ('*' + $item.File) } | Select-Object -First 1; if ($class) { $covLine = $class.lines.line | Where-Object { [int]$_.number -eq $item.Line } | Select-Object -First 1; if ($covLine) { $totalChanged++; if ([int]$covLine.hits -gt 0) { $hitChanged++ } } } }; $changedPct = if ($totalChanged -gt 0) { [math]::Round(($hitChanged / $totalChanged) * 100, 2) } else { 100.0 }; $newFiles = @(git diff --name-status $base -- '*.cs' | Where-Object { $_ -match '^A\s+' } | ForEach-Object { ($_ -split '\s+', 2)[1] }); $newTotal = 0; $newHit = 0; foreach ($path in $newFiles) { foreach ($class in ($xml.coverage.packages.package.classes.class | Where-Object { $_.filename -eq $path -or $_.filename -like ('*' + $path) })) { foreach ($covLine in $class.lines.line) { $newTotal++; if ([int]$covLine.hits -gt 0) { $newHit++ } } } }; $newPct = if ($newTotal -gt 0) { [math]::Round(($newHit / $newTotal) * 100, 2) } else { 100.0 }; Write-Output \"PostChangeOverallLineCoverage: $overall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"NewProductionCoverage: $newPct\"; Write-Output \"CoverageReportPath: $($report.FullName)\""` and save the parsed summary to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/coverage-summary.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`, numeric `PostChangeOverallLineCoverage:`, numeric `ChangedOrNewLineCoverage:`, numeric `NewProductionCoverage:`, and the parsed Cobertura report path.
- [x] [P7-T6] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$baseline = Get-ChildItem 'docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline' -Filter 'coverage-summary.*.md' | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; $post = Get-ChildItem 'docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates' -Filter 'coverage-summary.*.md' | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $baseline -or -not $post) { throw 'coverage summary artifacts missing'; }; $baselineOverall = [double](([regex]::Match((Get-Content $baseline.FullName -Raw), 'BaselineOverallLineCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $postOverall = [double](([regex]::Match((Get-Content $post.FullName -Raw), 'PostChangeOverallLineCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $changedPct = [double](([regex]::Match((Get-Content $post.FullName -Raw), 'ChangedOrNewLineCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $newPct = [double](([regex]::Match((Get-Content $post.FullName -Raw), 'NewProductionCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $threshold = if ($postOverall -ge 80.0 -and $postOverall -ge $baselineOverall -and $changedPct -ge 80.0 -and $newPct -ge 90.0) { 'PASS' } else { 'FAIL' }; Write-Output \"BaselineOverallLineCoverage: $baselineOverall\"; Write-Output \"PostChangeOverallLineCoverage: $postOverall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"NewProductionCoverage: $newPct\"; Write-Output \"ThresholdResult: $threshold\""` and save the comparison to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/coverage-thresholds.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`, numeric `BaselineOverallLineCoverage:`, numeric `PostChangeOverallLineCoverage:`, numeric `ChangedOrNewLineCoverage:`, numeric `NewProductionCoverage:`, and `ThresholdResult: PASS|FAIL`; `ThresholdResult: PASS` requires `PostChangeOverallLineCoverage >= 80.0`, `PostChangeOverallLineCoverage >= BaselineOverallLineCoverage`, `ChangedOrNewLineCoverage >= 80.0`, and `NewProductionCoverage >= 90.0`; Phase 7 only continues when `ThresholdResult: PASS`.
- [x] [P7-T7] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCFormat -Root ."` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/powershell-format.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command; if the formatter changes files, record the change and restart the PowerShell QC loop from [P7-T7].
- [x] [P7-T8] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCAnalyze -Root ."` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/powershell-analyze.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command; if the command fails, fix the issue and restart the PowerShell QC loop from [P7-T7].
- [x] [P7-T9] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root ."` and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/powershell-test.<timestamp>.md`.
  - Acceptance: The artifact exists and contains the exact command; if the command fails, fix the issue and restart the PowerShell QC loop from [P7-T7].
- [x] [P7-T10] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Import-Module ./scripts/powershell/PoshQC; Invoke-PoshQCTest -Root . -CoveragePaths @('./scripts/*.ps1') -TestResultOutputPath 'TestResults/qa-powershell/testResults.xml' -CoverageOutputPath 'TestResults/qa-powershell/coverage.json'"` using the repo-local `scripts/powershell/PoshQC` bootstrap from [P0-T10] and save the result to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/powershell-coverage.<timestamp>.md`.
  - Acceptance: The artifact exists under `evidence/qa-gates/`, contains the exact command plus the required evidence fields, and its `Output Summary:` includes numeric `OverallLineCoverage:` plus the generated PowerShell coverage artifact paths `TestResults/qa-powershell/testResults.xml` and `TestResults/qa-powershell/coverage.json`; if the command fails, fix the issue and restart the PowerShell QC loop from [P7-T7].
- [x] [P7-T11] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$base = git merge-base HEAD main; $coverage = Get-Content 'TestResults/qa-powershell/coverage.json' -Raw | ConvertFrom-Json; $overall = [math]::Round([double]$coverage.OverallLineCoverage, 2); $changedScripts = @(git diff --name-only $base -- 'scripts/*.ps1' | Where-Object { $_ }); $changedTotal = 0; $changedHit = 0; foreach ($path in $changedScripts) { $file = $coverage.Files | Where-Object { $_.Path -eq $path -or $_.Path -like ('*' + $path) } | Select-Object -First 1; if ($file) { foreach ($line in $file.Lines) { $changedTotal++; if ([bool]$line.Hit) { $changedHit++ } } } }; $changedPct = if ($changedTotal -gt 0) { [math]::Round(($changedHit / $changedTotal) * 100, 2) } else { 100.0 }; $newScripts = @(git diff --name-only --diff-filter=A $base -- 'scripts/*.ps1' | Where-Object { $_ }); $newTotal = 0; $newHit = 0; foreach ($path in $newScripts) { $file = $coverage.Files | Where-Object { $_.Path -eq $path -or $_.Path -like ('*' + $path) } | Select-Object -First 1; if ($file) { foreach ($line in $file.Lines) { $newTotal++; if ([bool]$line.Hit) { $newHit++ } } } }; $newPct = if ($newTotal -gt 0) { [math]::Round(($newHit / $newTotal) * 100, 2) } else { 100.0 }; Write-Output \"PostChangeOverallLineCoverage: $overall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"NewProductionCoverage: $newPct\"; Write-Output \"CoverageArtifactPath: TestResults/qa-powershell/coverage.json\""` and save the parsed summary to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/powershell-coverage-summary.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`, numeric `PostChangeOverallLineCoverage:`, numeric `ChangedOrNewLineCoverage:`, numeric `NewProductionCoverage:`, and the parsed PowerShell coverage artifact path.
- [ ] [P7-T12] Run `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$baseline = Get-ChildItem 'docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/baseline' -Filter 'powershell-coverage-summary.*.md' | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; $post = Get-ChildItem 'docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates' -Filter 'powershell-coverage-summary.*.md' | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $baseline -or -not $post) { throw 'PowerShell coverage summary artifacts missing'; }; $baselineOverall = [double](([regex]::Match((Get-Content $baseline.FullName -Raw), 'BaselineOverallLineCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $postOverall = [double](([regex]::Match((Get-Content $post.FullName -Raw), 'PostChangeOverallLineCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $changedPct = [double](([regex]::Match((Get-Content $post.FullName -Raw), 'ChangedOrNewLineCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $newPct = [double](([regex]::Match((Get-Content $post.FullName -Raw), 'NewProductionCoverage:\s*(?<value>[0-9]+(?:\.[0-9]+)?)')).Groups['value'].Value); $threshold = if ($postOverall -ge 80.0 -and $postOverall -ge $baselineOverall -and $changedPct -ge 80.0 -and $newPct -ge 90.0) { 'PASS' } else { 'FAIL' }; Write-Output \"BaselineOverallLineCoverage: $baselineOverall\"; Write-Output \"PostChangeOverallLineCoverage: $postOverall\"; Write-Output \"ChangedOrNewLineCoverage: $changedPct\"; Write-Output \"NewProductionCoverage: $newPct\"; Write-Output \"ThresholdResult: $threshold\""` and save the comparison to `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/powershell-coverage-thresholds.<timestamp>.md`.
  - Acceptance: The artifact exists and contains `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`, numeric `BaselineOverallLineCoverage:`, numeric `PostChangeOverallLineCoverage:`, numeric `ChangedOrNewLineCoverage:`, numeric `NewProductionCoverage:`, and `ThresholdResult: PASS|FAIL`; `ThresholdResult: PASS` requires `PostChangeOverallLineCoverage >= 80.0`, `PostChangeOverallLineCoverage >= BaselineOverallLineCoverage`, `ChangedOrNewLineCoverage >= 80.0`, and `NewProductionCoverage >= 90.0`; Phase 7 only continues when `ThresholdResult: PASS`.
- [x] [P7-T13] Reconcile the acceptance checklists in `issue.md`, `spec.md`, and `user-story.md` with final evidence in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/ac-status.<timestamp>.md`.
  - Acceptance: The artifact exists and contains exact headings `Issue Checklist`, `Spec Checklist`, `User Story Checklist`, and `Evidence References`.
- [ ] [P7-T14] If any source, test, script, or doc file changed during the Phase 7 QC loop after Phase 6 evidence was captured, rerun [P6-T1] and [P6-T2] and record refreshed operator evidence against the final post-QC build before completion is reported.
  - Acceptance: The latest `windows-acceptance.<timestamp>.md` and `windows-operator-validation.<timestamp>.md` artifacts are newer than the last file-changing Phase 7 rerun, or no file-changing Phase 7 rerun occurred.
- [ ] [P7-T15] Record the feature completion summary in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/qa-gates/feature-completion.<timestamp>.md`.
  - Acceptance: The artifact exists and contains exact lines `CSharpQc: PASS`, `PowerShellQc: PASS`, `WindowsAcceptance: PASS`, and `ReadyForReview: true`.

## Phase Outline

- Phase 0 — Context & Inputs
- Phase 1 — Framework Retargeting And Verification Harness
- Phase 2 — COM Boundary And Scan Scheduling
- Phase 3 — Repository, Privacy, RPC, And Client Completion
- Phase 4 — Scripts And Runbook Completion
- Phase 5 — Deterministic Test Expansion
- Phase 6 — Windows Operator Acceptance Evidence
- Phase 7 — Final QC Loop And Completion Evidence
