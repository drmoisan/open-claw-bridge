Timestamp: 2026-04-05T21:00:19.7376916-04:00
Command: Select-String -Path 'src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj','src\OpenClaw.MailBridge.Contracts\OpenClaw.MailBridge.Contracts.csproj','src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj','tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj' -Pattern '<TargetFramework>.*</TargetFramework>'
EXIT_CODE: 0
Output Summary: All four MailBridge project files target `net10.0-windows`.

```text
C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj:4: <TargetFramework>net10.0-windows</TargetFramework>
C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Contracts\OpenClaw.MailBridge.Contracts.csproj:3: <TargetFramework>net10.0-windows</TargetFramework>
C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj:4: <TargetFramework>net10.0-windows</TargetFramework>
C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj:3: <TargetFramework>net10.0-windows</TargetFramework>
```
