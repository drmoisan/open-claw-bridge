Timestamp: 2026-04-12T05:41:10Z
Command: $env:MSIX_PUBLISH_DIR = (Resolve-Path 'artifacts/publish').Path; dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~PublishOutput_"
EXIT_CODE: 0
Output Summary:
- Result=PASS
- Passed=2
- Failed=0
- Skipped=0
- Total=2
- ExecutedAssertions=PublishOutput_BridgeDirectory_ContainsBridgeExecutable;PublishOutput_ClientDirectory_ContainsClientExecutable
- AssertInconclusiveReached=False
- PublishRoot=artifacts/publish
