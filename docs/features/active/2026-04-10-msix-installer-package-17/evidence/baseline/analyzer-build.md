Timestamp: 2026-04-12T05:31:16Z
Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0
Output Summary: `msbuild` was resolved to `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` because it was not on `PATH` in this shell. The analyzer-enabled build succeeded with `2` warnings and `0` errors. Both warnings were `MSB3270` architecture-mismatch warnings in `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` for references to the `win-x64` bridge assemblies.
