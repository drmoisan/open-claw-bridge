Timestamp: 2026-04-12T22-46-53Z
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -Command '$assemblies = @(''tests/OpenClaw.MailBridge.Tests/bin/Debug/net10.0-windows/OpenClaw.MailBridge.Tests.dll'', ''tests/OpenClaw.HostAdapter.Tests/bin/Debug/net10.0/OpenClaw.HostAdapter.Tests.dll'', ''tests/OpenClaw.Core.Tests/bin/Debug/net10.0/OpenClaw.Core.Tests.dll''); foreach ($assembly in $assemblies) { if (-not (Test-Path $assembly)) { throw "Missing test assembly: $assembly" } }; Write-Output "TestAssemblyPaths: $($assemblies -join ''; '')"; $vstestArgs = ($assemblies | ForEach-Object { "''$_''" }) -join '' ''; dotnet-coverage collect "vstest.console.exe $vstestArgs /EnableCodeCoverage /Logger:trx /ResultsDirectory:TestResults/qa-csharp" -f cobertura -o ''TestResults/qa-csharp/coverage.cobertura.xml'''
EXIT_CODE: 1
Output Summary: The VSTest run started and executed discovered tests, but dotnet-coverage did not initialize the profiler and canceled without producing usable coverage data.
TestAssemblyPaths: tests/OpenClaw.MailBridge.Tests/bin/Debug/net10.0-windows/OpenClaw.MailBridge.Tests.dll; tests/OpenClaw.HostAdapter.Tests/bin/Debug/net10.0/OpenClaw.HostAdapter.Tests.dll; tests/OpenClaw.Core.Tests/bin/Debug/net10.0/OpenClaw.Core.Tests.dll
CoverageReportPath: TestResults/qa-csharp/coverage.cobertura.xml
Raw Output:
dotnet-coverage v18.5.2.0 [win-x64 - .NET 10.0.5]
SessionId: 672ec4d2-3856-494f-8cae-c48000b7d02e
VSTest version 18.4.0 (x64)
Starting test execution, please wait...
A total of 3 test files matched the specified pattern.
Tests executed successfully before coverage collection failed.
Canceling...
No code coverage data available. Profiler was not initialized.
Code coverage results: TestResults/qa-csharp/coverage.cobertura.xml.
