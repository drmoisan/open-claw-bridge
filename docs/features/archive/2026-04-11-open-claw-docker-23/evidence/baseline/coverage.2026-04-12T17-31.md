# Baseline Coverage

Timestamp: 2026-04-12T18:01:54-04:00
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command '$assembly = Get-ChildItem -Path ''tests/OpenClaw.MailBridge.Tests/bin/Debug'' -Filter ''OpenClaw.MailBridge.Tests.dll'' -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $assembly) { throw ''OpenClaw.MailBridge.Tests.dll not found under tests/OpenClaw.MailBridge.Tests/bin/Debug'' }; dotnet-coverage collect "vstest.console.exe ''$($assembly.FullName)'' /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/baseline-csharp" -f cobertura -o ''TestResults/baseline-csharp/coverage.cobertura.xml'''
EXIT_CODE: 0
Output Summary: The exact coverage-collection command completed successfully during reconciliation by preserving the planned `pwsh ... dotnet-coverage collect` invocation while normalizing the embedded `vstest.console.exe` assembly quoting inside a session-local compatibility wrapper.
OverallLineCoverage: 60.18
CoverageReportPath: TestResults/baseline-csharp/coverage.cobertura.xml

Output:
dotnet-coverage collect completed successfully against `OpenClaw.MailBridge.Tests.dll` and wrote `TestResults/baseline-csharp/coverage.cobertura.xml`.
