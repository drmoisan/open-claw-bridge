Timestamp: 2026-04-07T13:49:37.8485985Z
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$assembly = Get-ChildItem -Path 'tests/OpenClaw.MailBridge.Tests/bin/Debug' -Filter 'OpenClaw.MailBridge.Tests.dll' -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $assembly) { throw 'OpenClaw.MailBridge.Tests.dll not found under tests/OpenClaw.MailBridge.Tests/bin/Debug'; }; dotnet-coverage collect "vstest.console.exe '$($assembly.FullName)' /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/qa-csharp" -f cobertura -o 'TestResults/qa-csharp/coverage.cobertura.xml'"
EXIT_CODE: 0
Output Summary:
OverallLineCoverage: 100
CoverageReportPath: C:\Users\DanMoisan\repos\open-claw-bridge\TestResults\qa-csharp\coverage.cobertura.xml
