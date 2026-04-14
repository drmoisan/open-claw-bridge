Timestamp: 2026-04-07T09-08
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$assembly = Get-ChildItem -Path 'tests/OpenClaw.MailBridge.Tests/bin/Debug' -Filter 'OpenClaw.MailBridge.Tests.dll' -Recurse | Sort-Object LastWriteTimeUtc | Select-Object -Last 1; if (-not $assembly) { throw 'OpenClaw.MailBridge.Tests.dll not found under tests/OpenClaw.MailBridge.Tests/bin/Debug'; }; dotnet-coverage collect \"vstest.console.exe '$($assembly.FullName)' /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/baseline-csharp\" -f cobertura -o 'TestResults/baseline-csharp/coverage.cobertura.xml'"
EXIT_CODE: 1
Output Summary:
- `dotnet-coverage` executed and wrote `TestResults/baseline-csharp/coverage.cobertura.xml`.
- `vstest.console.exe` test discovery failed because the quoted DLL path was passed through literally.
- OverallLineCoverage: 100.00
- CoverageReportPath: C:\Users\DanMoisan\repos\open-claw-bridge\TestResults\baseline-csharp\coverage.cobertura.xml
