# QA Gate — OutlookScanner autostart-failure robustness fix

Timestamp: 2026-06-06T14-40

## Scope (files touched)
- src/OpenClaw.MailBridge/OutlookScanner.cs (production; 1 file)
- tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs (3 new tests)
- tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs (added FakeOutlookApplicationWithNullNamespace)
- tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs (corrected 1 pre-existing test to new contract)
- tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Phase5.cs (corrected 1 pre-existing test to new contract)
- tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs (corrected 1 pre-existing test to new contract)

## Format
Command: csharpier check .
EXIT_CODE: 0
Output Summary: Checked 94 files; no formatting diffs.

## Build (analyzers + nullable as errors) — affected project
Command: dotnet build src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj
EXIT_CODE: 0
Output Summary: Build succeeded, 0 Warning(s), 0 Error(s).

## Build — full solution (zero-regression)
Command: dotnet build
EXIT_CODE: 0
Output Summary: Build succeeded, 0 Warning(s), 0 Error(s).

## Architecture tests
Output Summary: No `*.ArchitectureTests` project exists in this repository; step not applicable.

## Test + coverage
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: Failed: 0, Passed: 176, Skipped: 3, Total: 179 (baseline was 173 passed; +3 new tests).

## Coverage delta (OutlookScanner.cs)
- Baseline: line-rate 0.9277 (92.77%), branch-rate 0.8571 (85.71%)
- Post-change: line-rate 0.9304 (93.04%), branch-rate 0.8863 (88.63%)
- Delta: line +0.27pp, branch +2.92pp. No regression; both above policy floors (line >= 85%, branch >= 75%).

## Canonical coverage artifact
- artifacts/csharp/coverage.xml (copied from newest TestResults cobertura).

## Zero-Regression Gate
- Analyzer delta: 0 new findings (full-solution build clean, TreatWarningsAsErrors=true).
- Compiler/nullable delta: 0 new diagnostics.
- xUnit/MSTest delta: 0 new failing tests (3 pre-existing tests corrected to assert the fixed contract; 3 tests added).
- Per-file coverage delta: OutlookScanner.cs >= baseline (improved).

## Pre-existing test correction rationale
Three pre-existing tests (Outlook_scanner_should_degrade_when_scan_throws,
OutlookScanner_ScanAsync_should_set_CacheStale_and_StaleReason_after_scan_failure,
ScanAsync_should_clear_outlook_ref_after_exception) used AutostartOutlook=true (the
BridgeSettings.Default), processCount=>0, ThrowOnCreate=true. Under the old defect, the autostart
logon exception propagated to ExecuteScanAsync's catch and produced degraded/scan_failure. That is
exactly the behavior the fix corrects. The tests were re-pointed to a genuine post-attach scan
failure (running instance with a null MAPI namespace) so they continue to assert the scan_failure /
degraded / outlook-ref-cleared paths without depending on the removed propagation. Assertions were
not weakened.
