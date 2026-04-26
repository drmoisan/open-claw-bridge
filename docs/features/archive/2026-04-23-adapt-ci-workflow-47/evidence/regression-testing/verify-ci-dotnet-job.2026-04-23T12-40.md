---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command '$patterns = @(...); foreach ($p in $patterns) { Select-String -LiteralPath .github/workflows/ci.yml -Pattern $p -SimpleMatch }'
EXIT_CODE: 0
---

# verify-ci-dotnet-job — AC-3 evidence

Required literal patterns per AC-3 and their matches in `.github/workflows/ci.yml`:

| Pattern | Line | Matched text |
|---|---|---|
| `actions/setup-dotnet@v4` | 19 | `uses: actions/setup-dotnet@v4` |
| `dotnet-version: 10.0.x` | 21 | `dotnet-version: 10.0.x` |
| `runs-on: windows-latest` | 13 | `runs-on: windows-latest` (dotnet-build-test job) |
| `runs-on: windows-latest` | 42 | `runs-on: windows-latest` (powershell-quality job — invariant, also Windows) |
| `dotnet restore OpenClaw.MailBridge.sln` | 24 | `run: dotnet restore OpenClaw.MailBridge.sln` |
| `dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror` | 27 | `run: dotnet build OpenClaw.MailBridge.sln -c Release --no-restore /warnaserror` |
| `XPlat Code Coverage` | 30 | `run: dotnet test OpenClaw.MailBridge.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults` |

Output Summary:
- All six AC-3 literal patterns are present in the `dotnet-build-test` job.
- `dotnet test ... --collect:"XPlat Code Coverage"` is present verbatim (shown above with surrounding flags).
- `runs-on: windows-latest` appears on both Windows jobs (AC-3 specifies Windows-bound for the .NET job; the PowerShell job also requires Windows due to the Outlook-COM test coverage implied by the repo's `publish.yml` precedent).
