# Phase 0 Baseline 02 — dotnet build (compile + nullable type-check) (Issue #120)

Timestamp: 2026-07-06T23-11
Command: `dotnet build` (run from repository root)
EXIT_CODE: 0

Output Summary: Build succeeded. 0 Warning(s), 0 Error(s). All eight projects restored and
compiled (OpenClaw.MailBridge.Contracts, OpenClaw.HostAdapter.Contracts,
OpenClaw.MailBridge.Client, OpenClaw.HostAdapter, OpenClaw.Core, OpenClaw.HostAdapter.Tests,
OpenClaw.MailBridge, OpenClaw.MailBridge.Tests, OpenClaw.Core.Tests).

Notes: This repository has no `Directory.Build.props`, no third-party analyzer stack, and
`TreatWarningsAsErrors` is not set, so `dotnet build` covers the compile and nullable
reference-type type-check stages only — there is no separate lint/analyzer stage. Warning
count was read from the build log (0 warnings); because the build exits 0 even with
warnings present, the zero-warning confirmation is made by log inspection, not exit code.
Baseline is clean.
