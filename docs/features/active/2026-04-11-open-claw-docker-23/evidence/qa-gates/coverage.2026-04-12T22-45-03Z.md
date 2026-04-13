Timestamp: 2026-04-12T22-45-03Z
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ' = @(''tests/OpenClaw.MailBridge.Tests/bin/Debug/net10.0-windows/OpenClaw.MailBridge.Tests.dll'', ''tests/OpenClaw.HostAdapter.Tests/bin/Debug/net10.0/OpenClaw.HostAdapter.Tests.dll'', ''tests/OpenClaw.Core.Tests/bin/Debug/net10.0/OpenClaw.Core.Tests.dll''); foreach ( in ) { if (-not (Test-Path )) { throw "Missing test assembly: " } }; Write-Output "TestAssemblyPaths:  ";  = ( | ForEach-Object { "''''" }) -join '' ''; dotnet-coverage collect "vstest.console.exe  /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/qa-csharp" -f cobertura -o ''TestResults/qa-csharp/coverage.cobertura.xml''' 
EXIT_CODE: 1
Output Summary: Coverage run failed.
TestAssemblyPaths: tests/OpenClaw.MailBridge.Tests/bin/Debug/net10.0-windows/OpenClaw.MailBridge.Tests.dll; tests/OpenClaw.HostAdapter.Tests/bin/Debug/net10.0/OpenClaw.HostAdapter.Tests.dll; tests/OpenClaw.Core.Tests/bin/Debug/net10.0/OpenClaw.Core.Tests.dll
CoverageReportPath: TestResults/qa-csharp/coverage.cobertura.xml
Raw Output:
dotnet-coverage v18.5.2.0 [win-x64 - .NET 10.0.5]

SessionId: d4032821-224e-4d3d-8be7-cc0fbd8b9831
An error occurred trying to start process 'vstest.console.exe' with working directory 'C:\Users\DanMoisan\repos\open-claw-bridge'. The system cannot find the file specified.
