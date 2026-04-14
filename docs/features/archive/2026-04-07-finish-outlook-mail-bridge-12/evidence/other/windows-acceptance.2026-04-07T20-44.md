Timestamp: 2026-04-07T20-44
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-mailbridge.ps1
EXIT_CODE: 1
Output Summary:
- AutomatedSuitesPassed: NOT_REACHED
- OperatorEvidencePath: docs\features\active\2026-04-07-finish-outlook-mail-bridge-12\evidence\other\windows-operator-evidence.2026-04-07T20-44.txt
- InstallRoot: C:\Program Files\OpenClaw\MailBridge
- ClientPath: C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.exe

## Install Output
  Determining projects to restore...
  Restored C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Contracts\OpenClaw.MailBridge.Contracts.csproj (in 259 ms).
  Restored C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj (in 300 ms).
  OpenClaw.MailBridge.Contracts -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Contracts\bin\Debug\net8.0-windows\OpenClaw.MailBridge.Contracts.dll
  OpenClaw.MailBridge -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge\bin\Debug\net8.0-windows\win-x64\OpenClaw.MailBridge.dll
  OpenClaw.MailBridge -> C:\Program Files\OpenClaw\MailBridge\
  Determining projects to restore...
  Restored C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj (in 170 ms).
  1 of 2 projects are up-to-date for restore.
  OpenClaw.MailBridge.Contracts -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Contracts\bin\Debug\net8.0-windows\OpenClaw.MailBridge.Contracts.dll
  OpenClaw.MailBridge.Client -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Client\bin\Debug\net8.0-windows\win-x64\OpenClaw.MailBridge.Client.dll
  OpenClaw.MailBridge.Client -> C:\Program Files\OpenClaw\MailBridge\

## Test Output
Outlook profile preflight failed. Confirm classic Outlook is installed, a profile is configured, and the default Inbox and Calendar exist.
