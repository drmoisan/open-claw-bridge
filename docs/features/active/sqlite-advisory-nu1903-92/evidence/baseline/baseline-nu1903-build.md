# Baseline NU1903 Build (Fail-Before) — Issue #92

Timestamp: 2026-07-01T19-46

Command: dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror

Note on invocation: In the Git Bash shell, the token `/warnaserror` is rewritten to a Windows path by MSYS path conversion. The equivalent MSBuild switch `-warnaserror` was used to invoke the same behavior. `/warnaserror` and `-warnaserror` are the same MSBuild property switch; the recorded gate command is `dotnet build OpenClaw.MailBridge.sln -c Release /warnaserror`.

EXIT_CODE: 1

Output Summary:
- Build FAILED.
- NU1903 promoted to error (via /warnaserror + NuGet audit) on four projects:
  - `error NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q` — src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj
  - same NU1903/GHSA-2m69-gcr7-jv3q — src/OpenClaw.Core/OpenClaw.Core.csproj
  - same NU1903/GHSA-2m69-gcr7-jv3q — tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj
  - same NU1903/GHSA-2m69-gcr7-jv3q — tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj
- 0 Warning(s) reported (the advisory is emitted as an error, not a warning, under warnings-as-errors).
- This establishes the fail-before state for AC-2: the gate command fails specifically on the SQLitePCLRaw.lib.e_sqlite3 2.1.6 advisory.
