# OpenClaw.MailBridge workspace setup

## Folder structure

```text
/workspace-root
  /src
    /OpenClaw.MailBridge
    /OpenClaw.MailBridge.Client
    /OpenClaw.MailBridge.Contracts
  /tests
    /OpenClaw.MailBridge.Tests
  /scripts
  /docs
  OpenClaw.MailBridge.sln
  OpenClaw.MailBridge.code-workspace
  global.json
  .vscode/
  .codex/
```

## Required SDK / workload setup

```powershell
dotnet --list-sdks
```

Required SDK:

```text
.NET SDK 10.0.201
```

Additional workloads:

```text
None required for this console/class library/NUnit solution.
```

## Required VS Code extensions

```text
ms-dotnettools.csharp
ms-dotnettools.csdevkit
ms-vscode.powershell
```

## Solution creation steps

```powershell
New-Item -ItemType Directory -Force -Path src, tests, scripts, docs, .vscode, .codex
New-Item -ItemType Directory -Force -Path src/OpenClaw.MailBridge, src/OpenClaw.MailBridge.Client, src/OpenClaw.MailBridge.Contracts, tests/OpenClaw.MailBridge.Tests

dotnet new sln --name OpenClaw.MailBridge --format sln

dotnet new console --framework net10.0 --use-program-main --output src/OpenClaw.MailBridge --name OpenClaw.MailBridge
dotnet new console --framework net10.0 --use-program-main --output src/OpenClaw.MailBridge.Client --name OpenClaw.MailBridge.Client
dotnet new classlib --framework net10.0 --output src/OpenClaw.MailBridge.Contracts --name OpenClaw.MailBridge.Contracts
dotnet new nunit --framework net10.0 --output tests/OpenClaw.MailBridge.Tests --name OpenClaw.MailBridge.Tests

dotnet sln OpenClaw.MailBridge.sln add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj

dotnet add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj reference src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj
dotnet add src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj reference src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj
dotnet add tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj reference src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj
```

Windows-only integration references require a Windows target framework. To keep the requested `net10.0` tests and still reference the Windows apps, the test project is dual-targeted to `net10.0;net10.0-windows`, with `MailBridge` and `Client` references attached only to the `net10.0-windows` target.

## Project creation commands

```powershell
dotnet add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj package Microsoft.Data.Sqlite
dotnet add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj package Microsoft.Extensions.Hosting
dotnet add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj package Microsoft.Extensions.Logging
dotnet add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj package Microsoft.Extensions.Configuration
dotnet add src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj package Microsoft.Extensions.Configuration.Json

dotnet add src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj package System.CommandLine

dotnet add tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj package NUnit
dotnet add tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj package NUnit3TestAdapter
dotnet add tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj package Microsoft.NET.Test.Sdk
dotnet add tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj package FluentAssertions
```

## NuGet dependencies per project

### OpenClaw.MailBridge

```text
Microsoft.Data.Sqlite 10.0.5
Microsoft.Extensions.Hosting 10.0.5
Microsoft.Extensions.Logging 10.0.5
Microsoft.Extensions.Configuration 10.0.5
Microsoft.Extensions.Configuration.Json 10.0.5
Microsoft.Office.Interop.Outlook (PIA assembly reference in workspace; COMReference instructions below)
```

### OpenClaw.MailBridge.Client

```text
System.CommandLine 2.0.5
```

### OpenClaw.MailBridge.Contracts

```text
No additional packages
```

### OpenClaw.MailBridge.Tests

```text
NUnit 4.4.0
NUnit3TestAdapter 4.6.0
Microsoft.NET.Test.Sdk 17.14.1
FluentAssertions 8.9.0
```

## Outlook COM setup

Prerequisites:

```text
Classic Outlook must be installed.
The Outlook PIA is typically installed with Microsoft Office.
```

### Option A (preferred): add COM reference in csproj using GUID

`dotnet build` cannot execute `ResolveComReference`, so the checked-in workspace uses a direct PIA assembly reference for CLI compatibility. The GUID-based COM reference below is the preferred Visual Studio/MSBuild configuration when you want COM metadata generated from the project file.

Add this block to `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`:

```xml
<ItemGroup Condition="'$(OutlookObjectLibraryPath)' != ''">
  <COMReference Include="Microsoft.Office.Interop.Outlook">
    <Guid>00062FFF-0000-0000-C000-000000000046</Guid>
    <VersionMajor>9</VersionMajor>
    <VersionMinor>6</VersionMinor>
    <Lcid>0</Lcid>
    <WrapperTool>primary</WrapperTool>
    <Isolated>false</Isolated>
    <EmbedInteropTypes>true</EmbedInteropTypes>
  </COMReference>
</ItemGroup>
```

Default Office 16 path detection used in this workspace:

```xml
<PropertyGroup>
  <OutlookObjectLibraryPath Condition="'$(OutlookObjectLibraryPath)' == '' and Exists('$(ProgramFiles)\Microsoft Office\root\Office16\MSOUTL.OLB')">$(ProgramFiles)\Microsoft Office\root\Office16\MSOUTL.OLB</OutlookObjectLibraryPath>
  <OutlookObjectLibraryPath Condition="'$(OutlookObjectLibraryPath)' == '' and Exists('$(ProgramFiles(x86))\Microsoft Office\root\Office16\MSOUTL.OLB')">$(ProgramFiles(x86))\Microsoft Office\root\Office16\MSOUTL.OLB</OutlookObjectLibraryPath>
</PropertyGroup>
```

CLI-compatible PIA reference used in this workspace:

```xml
<ItemGroup Condition="'$(OutlookPiaPath)' != ''">
  <Reference Include="Microsoft.Office.Interop.Outlook">
    <HintPath>$(OutlookPiaPath)</HintPath>
    <EmbedInteropTypes>true</EmbedInteropTypes>
    <Private>false</Private>
  </Reference>
</ItemGroup>

<PropertyGroup>
  <OutlookPiaPath Condition="'$(OutlookPiaPath)' == '' and Exists('C:\Windows\Microsoft.NET\assembly\GAC_MSIL\Microsoft.Office.Interop.Outlook\v4.0_15.0.0.0__71e9bce111e9429c\Microsoft.Office.Interop.Outlook.dll')">C:\Windows\Microsoft.NET\assembly\GAC_MSIL\Microsoft.Office.Interop.Outlook\v4.0_15.0.0.0__71e9bce111e9429c\Microsoft.Office.Interop.Outlook.dll</OutlookPiaPath>
  <OutlookPiaPath Condition="'$(OutlookPiaPath)' == '' and Exists('$(ProgramFiles)\Microsoft Office\root\Office16\ADDINS\Microsoft.Office.Interop.Outlook.dll')">$(ProgramFiles)\Microsoft Office\root\Office16\ADDINS\Microsoft.Office.Interop.Outlook.dll</OutlookPiaPath>
  <OutlookPiaPath Condition="'$(OutlookPiaPath)' == '' and Exists('$(ProgramFiles(x86))\Microsoft Office\root\Office16\ADDINS\Microsoft.Office.Interop.Outlook.dll')">$(ProgramFiles(x86))\Microsoft Office\root\Office16\ADDINS\Microsoft.Office.Interop.Outlook.dll</OutlookPiaPath>
</PropertyGroup>
```

### Option B: add the COM reference from Visual Studio

```text
1. Open the solution in Visual Studio on Windows.
2. Right-click OpenClaw.MailBridge.
3. Select Add > Reference.
4. Open COM.
5. Select Microsoft Outlook 16.0 Object Library.
6. Confirm and save.
```

## PowerShell execution policy guidance

Check current policy:

```powershell
Get-ExecutionPolicy -List
```

Temporary session-only policy:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

Persistent current-user policy:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

## Validation steps

### 1. Build

```powershell
dotnet build .\OpenClaw.MailBridge.sln
```

### 2. Test

```powershell
dotnet test .\OpenClaw.MailBridge.sln
```

### 3. Run MailBridge

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project .\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj --configuration Debug
```

Expected startup output includes:

```text
Main thread apartment state: STA
SQLite ready: <version>
Hosting environment: Development
Bridge server ready. PipeName=openclaw-mail-bridge
```

### 4. Run Client

Open a second terminal:

```powershell
dotnet run --project .\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj --configuration Debug -- --pipe-name openclaw-mail-bridge --message ping
```

Expected output:

```json
{"success":true,"message":"Processed operation 'ping'.","payload":"{...}","timestampUtc":"..."}
```

### Verify Outlook COM loads

Successful COM detection at bridge startup:

```text
Outlook COM ProgID resolved successfully
```

If COM is not available, bridge startup prints:

```text
Outlook COM ProgID not found. Install classic Outlook and the Outlook PIAs before COM integration work.
```

### Confirm STA thread works

Successful STA confirmation at bridge startup:

```text
Main thread apartment state: STA
```

## Workspace files created

```text
.vscode/settings.json
.vscode/launch.json
.vscode/tasks.json
OpenClaw.MailBridge.code-workspace
.codex/config.toml
global.json
scripts/Build.ps1
scripts/Test.ps1
scripts/Run-Bridge.ps1
scripts/Run-Client.ps1
```
