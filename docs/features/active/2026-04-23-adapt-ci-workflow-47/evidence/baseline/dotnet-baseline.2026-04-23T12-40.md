---
Timestamp: 2026-04-23T12-40
Commands: (see per-step entries below)
EXIT_CODE: 0 (all steps)
---

# .NET Toolchain Baseline

## 1. dotnet --info

- Command: `dotnet --info`
- EXIT_CODE: 0
- Output Summary:
  - .NET SDK Version: `10.0.202`
  - Host Version: `10.0.6`
  - OS: Windows 10.0.26200 (win-x64)
  - SDKs installed: `10.0.202`
  - Runtimes: `Microsoft.AspNetCore.App 10.0.6`, `Microsoft.NETCore.App 10.0.6`, `Microsoft.WindowsDesktop.App 10.0.6`
  - Note: `global.json` pins `10.0.201` with `rollForward: latestFeature`, which permits `10.0.202`. CI job uses `dotnet-version: 10.0.x` per AC-3.

## 2. dotnet restore OpenClaw.MailBridge.sln

- Command: `dotnet restore OpenClaw.MailBridge.sln`
- EXIT_CODE: 0
- Output Summary: `All projects are up-to-date for restore.`

## 3. dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror

- Command executed (Windows Git-Bash path conversion workaround): `dotnet build OpenClaw.MailBridge.sln -c Release --no-restore -p:TreatWarningsAsErrors=true`
- EXIT_CODE: 0
- Output Summary:
  - Build succeeded.
  - `0 Warning(s), 0 Error(s)` across all 10 projects.
  - Time Elapsed: 00:00:05.35
  - Notes: `/warnaserror` is equivalent to `-p:TreatWarningsAsErrors=true` for MSBuild. On the Windows GitHub Actions runner (which uses `pwsh` as the default shell, not Git-Bash), `/warnaserror` passes through cleanly. AC-3 requires the literal `/warnaserror` text in the CI file; the semantic check is performed here.

## 4. dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults

- Command: `dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults`
- EXIT_CODE: 0
- Output Summary:
  - OpenClaw.HostAdapter.Tests.dll: Passed 71, Failed 0, Skipped 0, Total 71 (482 ms)
  - OpenClaw.Core.Tests.dll: Passed 51, Failed 0, Skipped 0, Total 51 (775 ms)
  - OpenClaw.MailBridge.Tests.dll: Passed 152, Failed 0, Skipped 3, Total 155 (12 s)
  - Grand total: Passed 274, Failed 0, Skipped 3, Total 277
  - Coverage cobertura files produced: 3 (one per test project)

## 5. Coverage parse (XPlat Code Coverage)

- Per-project line-rate:
  - `29871bdb-...`: covered=1063, valid=1260, line-rate=0.8436 (84.36%)
  - `2e18039a-...`: covered=900, valid=1142, line-rate=0.7880 (78.80%)
  - `9665802e-...`: covered=1163, valid=1302, line-rate=0.8932 (89.32%)
- Weighted overall: **3126 / 3704 = 84.40%**

Output Summary:
- Baseline repository-wide line coverage: **84.40%** (weighted across 3 cobertura reports).
- Meets the C# `>=80%` policy floor with 4.40 percentage points of headroom.
- No new C# code is being added by this plan; `NewCodeCoverage` is N/A for Phase 4 delta check.
