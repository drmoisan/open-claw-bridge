Timestamp: 2026-04-12T22-44-07Z
Command: msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Nullable build passed with warnings treated as errors.
Raw Output:
MSBuild version 18.3.0-release-26153-122+4d3023de6 for .NET
  OpenClaw.MailBridge.Contracts -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Contracts\bin\Debug\net10.0\OpenClaw.MailBridge.Contracts.dll
  OpenClaw.HostAdapter.Contracts -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.HostAdapter.Contracts\bin\Debug\net10.0\OpenClaw.HostAdapter.Contracts.dll
  OpenClaw.MailBridge.Client -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Client\bin\Debug\net10.0-windows\win-x64\OpenClaw.MailBridge.Client.dll
  OpenClaw.HostAdapter -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.HostAdapter\bin\Debug\net10.0\OpenClaw.HostAdapter.dll
  OpenClaw.Core -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.Core\bin\Debug\net10.0\OpenClaw.Core.dll
  OpenClaw.MailBridge -> C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge\bin\Debug\net10.0-windows\win-x64\OpenClaw.MailBridge.dll
  OpenClaw.HostAdapter.Tests -> C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.HostAdapter.Tests\bin\Debug\net10.0\OpenClaw.HostAdapter.Tests.dll
  OpenClaw.Core.Tests -> C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.Core.Tests\bin\Debug\net10.0\OpenClaw.Core.Tests.dll
C:\Program Files\dotnet\sdk\10.0.201\Microsoft.Common.CurrentVersion.targets(2451,5): warning MSB3270: There was a mismatch between the processor architecture of the project being built "MSIL" and the processor architecture of the reference "C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge.Client\bin\Debug\net10.0-windows\win-x64\OpenClaw.MailBridge.Client.dll", "AMD64". This mismatch may cause runtime failures. Please consider changing the targeted processor architecture of your project through the Configuration Manager so as to align the processor architectures between your project and references, or take a dependency on references with a processor architecture that matches the targeted processor architecture of your project. [C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj]
C:\Program Files\dotnet\sdk\10.0.201\Microsoft.Common.CurrentVersion.targets(2451,5): warning MSB3270: There was a mismatch between the processor architecture of the project being built "MSIL" and the processor architecture of the reference "C:\Users\DanMoisan\repos\open-claw-bridge\src\OpenClaw.MailBridge\bin\Debug\net10.0-windows\win-x64\OpenClaw.MailBridge.dll", "AMD64". This mismatch may cause runtime failures. Please consider changing the targeted processor architecture of your project through the Configuration Manager so as to align the processor architectures between your project and references, or take a dependency on references with a processor architecture that matches the targeted processor architecture of your project. [C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj]
  OpenClaw.MailBridge.Tests -> C:\Users\DanMoisan\repos\open-claw-bridge\tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll
