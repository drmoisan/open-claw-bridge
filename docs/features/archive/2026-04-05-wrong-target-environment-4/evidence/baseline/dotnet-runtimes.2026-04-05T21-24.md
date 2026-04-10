# Dotnet Runtimes Baseline

- **Task:** P0-T7
- **Timestamp:** 2026-04-05T21-24
- **Feature:** wrong-target-environment (Issue #4)

## Evidence

Command: `dotnet --list-runtimes`
EXIT_CODE: 0
Output Summary:
```
Microsoft.AspNetCore.App 10.0.5 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
Microsoft.NETCore.App 10.0.5 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
Microsoft.WindowsDesktop.App 10.0.5 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
```
