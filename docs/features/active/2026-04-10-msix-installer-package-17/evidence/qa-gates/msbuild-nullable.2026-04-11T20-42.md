Timestamp: 2026-04-11T20:42:00-04:00
Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary:
- InvocationPath=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe
- Result=PASS
- Warnings=2
- WarningCodes=MSB3270;MSB3270
- WarningSummary=Test project targets MSIL while referenced bridge binaries are AMD64
