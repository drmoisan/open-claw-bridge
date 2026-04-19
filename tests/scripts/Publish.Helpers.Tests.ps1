#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Publish.Helpers.psm1.

.DESCRIPTION
    Tests each exported helper using mocks and function shims for external
    executables so no real SDK tools, publish outputs, or temporary files are
    required. All inter-test state is held in $script: variables scoped to the
    outer Describe block; no $global: state is used.
#>

Describe 'Publish.Helpers.psm1' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Helpers.psm1'

        # Shim state for every external tool the module may invoke.
        $script:MakePriCallCount = 0
        $script:MakePriExitCode = 0
        $script:LastMakePriArgs = $null
        $script:MakeAppxCallCount = 0
        $script:MakeAppxExitCode = 0
        $script:LastMakeAppxArgs = $null
        $script:SignToolCallCount = 0
        $script:SignToolExitCode = 0
        $script:LastSignToolArgs = $null
        $script:DotnetCallCount = 0
        $script:DotnetExitCode = 0
        $script:LastDotnetArgs = $null

        # Define global shim functions for tools called via '& $tool args ...'.
        # Each writes its arguments into $script: state and honors a per-tool
        # exit-code variable set in BeforeEach.
        function global:makepri {
            $script:LastMakePriArgs = $args
            $script:MakePriCallCount++
            $global:LASTEXITCODE = $script:MakePriExitCode
        }
        function global:makeappx {
            $script:LastMakeAppxArgs = $args
            $script:MakeAppxCallCount++
            $global:LASTEXITCODE = $script:MakeAppxExitCode
        }
        function global:signtool {
            $script:LastSignToolArgs = $args
            $script:SignToolCallCount++
            $global:LASTEXITCODE = $script:SignToolExitCode
        }
        function global:dotnet {
            $script:LastDotnetArgs = $args
            $script:DotnetCallCount++
            $global:LASTEXITCODE = $script:DotnetExitCode
        }

        Import-Module $script:ModulePath -Force

        $script:FakeHashLower = ('a' * 64)
        $script:SampleManifestText = @'
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Identity Name="OpenClaw.MailBridge" Publisher="CN=OpenClaw" Version="0.0.0.0" ProcessorArchitecture="x64" />
</Package>
'@
        [xml]$script:SampleManifestXml = $script:SampleManifestText
    }

    AfterAll {
        foreach ($fn in 'makepri', 'makeappx', 'signtool', 'dotnet') {
            Remove-Item -Path "Function:\global:$fn" -ErrorAction SilentlyContinue
        }
    }

    BeforeEach {
        $script:MakePriCallCount = 0; $script:MakePriExitCode = 0; $script:LastMakePriArgs = $null
        $script:MakeAppxCallCount = 0; $script:MakeAppxExitCode = 0; $script:LastMakeAppxArgs = $null
        $script:SignToolCallCount = 0; $script:SignToolExitCode = 0; $script:LastSignToolArgs = $null
        $script:DotnetCallCount = 0; $script:DotnetExitCode = 0; $script:LastDotnetArgs = $null
    }

    Context 'Module exports' {
        It 'exports the expected 12 helper functions' {
            $expected = @(
                'Find-WindowsSdkTool', 'Get-StampedAppxManifestXml', 'Invoke-VersionStamp',
                'Invoke-LayoutAssembly', 'Invoke-MakePri', 'Invoke-MakeAppx',
                'Invoke-SignTool', 'Invoke-DotnetPublish', 'Copy-DockerArtifact',
                'Copy-InstallScriptsIntoBundle', 'New-ManifestEntry', 'Write-PublishManifest'
            ) | Sort-Object
            $actual = (Get-Command -Module Publish.Helpers).Name | Sort-Object
            ($actual -join ',') | Should -Be ($expected -join ',')
        }
    }

    Context 'Find-WindowsSdkTool' {
        It 'returns the SDK-bin match when ProgramFiles(x86) has a matching x64 binary' {
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Mock -ModuleName Publish.Helpers Get-ChildItem {
                [pscustomobject]@{ FullName = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\makeappx.exe' }
            }
            Find-WindowsSdkTool -ToolName 'makeappx.exe' |
                Should -Be 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\makeappx.exe'
        }
        It 'falls back to Get-Command when the SDK bin root is missing' {
            Mock -ModuleName Publish.Helpers Test-Path { $false }
            Mock -ModuleName Publish.Helpers Get-Command {
                [pscustomobject]@{ Source = 'C:\fake\on-path\makeappx.exe' }
            }
            Find-WindowsSdkTool -ToolName 'makeappx.exe' |
                Should -Be 'C:\fake\on-path\makeappx.exe'
        }
        It 'throws when the tool cannot be located anywhere' {
            Mock -ModuleName Publish.Helpers Test-Path { $false }
            Mock -ModuleName Publish.Helpers Get-Command { $null }
            { Find-WindowsSdkTool -ToolName 'nope.exe' } |
                Should -Throw -ExpectedMessage '*Cannot locate nope.exe*'
        }
    }

    Context 'Get-StampedAppxManifestXml' {
        It 'stamps the 4-part version into Package.Identity.Version' {
            (Get-StampedAppxManifestXml -ManifestXml $script:SampleManifestXml -Version '1.2.3.4').Package.Identity.Version |
                Should -Be '1.2.3.4'
        }
        It 'preserves other Identity attributes unchanged' {
            $r = Get-StampedAppxManifestXml -ManifestXml $script:SampleManifestXml -Version '9.8.7.6'
            $r.Package.Identity.Name | Should -Be 'OpenClaw.MailBridge'
            $r.Package.Identity.Publisher | Should -Be 'CN=OpenClaw'
            $r.Package.Identity.ProcessorArchitecture | Should -Be 'x64'
        }
        It 'rejects a 3-part version via ValidatePattern' {
            { Get-StampedAppxManifestXml -ManifestXml $script:SampleManifestXml -Version '1.2.3' } |
                Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
        }
    }

    Context 'Invoke-VersionStamp' {
        BeforeEach {
            Mock -ModuleName Publish.Helpers Get-Content { $script:SampleManifestText }
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Mock -ModuleName Publish.Helpers New-Item { }
            $script:WrittenPath = $null
            $script:WrittenValue = $null
            Mock -ModuleName Publish.Helpers Set-Content {
                $script:WrittenPath = $Path
                $script:WrittenValue = $Value
            }
        }
        It 'writes stamped XML to <stagingDir>/AppxManifest.xml' {
            $r = Invoke-VersionStamp -ManifestSourcePath 'C:\fake\source.xml' -StagingDir 'C:\fake\staging' -Version '1.2.3.4'
            $r | Should -Be (Join-Path 'C:\fake\staging' 'AppxManifest.xml')
            Assert-MockCalled -ModuleName Publish.Helpers Set-Content -Times 1 -Scope It
            $script:WrittenValue | Should -Match 'Version="1.2.3.4"'
        }
        It '-WhatIf leaves the staging file absent' {
            Invoke-VersionStamp -ManifestSourcePath 'C:\fake\source.xml' -StagingDir 'C:\fake\staging' -Version '2.0.0.0' -WhatIf
            Assert-MockCalled -ModuleName Publish.Helpers Set-Content -Times 0 -Scope It
        }
    }

    Context 'Invoke-LayoutAssembly' {
        It 'throws when bridge publish dir is missing' {
            Mock -ModuleName Publish.Helpers Test-Path { $false } -ParameterFilter { $Path -eq 'C:\missing\bridge' }
            Mock -ModuleName Publish.Helpers Test-Path { $true } -ParameterFilter { $Path -ne 'C:\missing\bridge' }
            { Invoke-LayoutAssembly -BridgePublishDir 'C:\missing\bridge' -ClientPublishDir 'C:\fake\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging' } |
                Should -Throw -ExpectedMessage '*Bridge publish directory not found*'
        }
        It 'throws when client publish dir is missing' {
            Mock -ModuleName Publish.Helpers Test-Path { $true } -ParameterFilter { $Path -eq 'C:\fake\bridge' }
            Mock -ModuleName Publish.Helpers Test-Path { $false } -ParameterFilter { $Path -eq 'C:\missing\client' }
            Mock -ModuleName Publish.Helpers Test-Path { $true } -ParameterFilter { $Path -ne 'C:\fake\bridge' -and $Path -ne 'C:\missing\client' }
            { Invoke-LayoutAssembly -BridgePublishDir 'C:\fake\bridge' -ClientPublishDir 'C:\missing\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging' } |
                Should -Throw -ExpectedMessage '*Client publish directory not found*'
        }
        It 'calls Copy-Item for bridge, client, and assets on success' {
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Mock -ModuleName Publish.Helpers Remove-Item { }
            Mock -ModuleName Publish.Helpers New-Item { }
            Mock -ModuleName Publish.Helpers Copy-Item { }
            Invoke-LayoutAssembly -BridgePublishDir 'C:\fake\bridge' -ClientPublishDir 'C:\fake\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging'
            Assert-MockCalled -ModuleName Publish.Helpers Copy-Item -Times 3 -Scope It
        }
        It '-WhatIf skips all copies' {
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Mock -ModuleName Publish.Helpers Remove-Item { }
            Mock -ModuleName Publish.Helpers New-Item { }
            Mock -ModuleName Publish.Helpers Copy-Item { }
            Invoke-LayoutAssembly -BridgePublishDir 'C:\fake\bridge' -ClientPublishDir 'C:\fake\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging' -WhatIf
            Assert-MockCalled -ModuleName Publish.Helpers Copy-Item -Times 0 -Scope It
        }
    }

    Context 'Invoke-MakePri' {
        BeforeEach { Mock -ModuleName Publish.Helpers Find-WindowsSdkTool { 'makepri' } }
        It 'invokes makepri createconfig then new' {
            Invoke-MakePri -StagingDir 'C:\fake\staging'
            $script:MakePriCallCount | Should -Be 2
        }
        It 'throws on non-zero exit' {
            $script:MakePriExitCode = 1
            { Invoke-MakePri -StagingDir 'C:\fake\staging' } |
                Should -Throw -ExpectedMessage '*makepri createconfig failed*'
        }
        It '-WhatIf does not invoke the tool' {
            Invoke-MakePri -StagingDir 'C:\fake\staging' -WhatIf
            $script:MakePriCallCount | Should -Be 0
        }
    }

    Context 'Invoke-MakeAppx' {
        BeforeEach {
            Mock -ModuleName Publish.Helpers Find-WindowsSdkTool { 'makeappx' }
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Mock -ModuleName Publish.Helpers New-Item { }
        }
        It 'passes /d /p /nv /o exactly and preserves OutputMsixPath' {
            Invoke-MakeAppx -StagingDir 'C:\fake\staging' -OutputMsixPath 'C:\fake\out\x.msix'
            $script:MakeAppxCallCount | Should -Be 1
            $a = @($script:LastMakeAppxArgs)
            $a[0] | Should -Be 'pack'
            $a[1] | Should -Be '/d'
            $a[2] | Should -Be 'C:\fake\staging'
            $a[3] | Should -Be '/p'
            $a[4] | Should -Be 'C:\fake\out\x.msix'
            $a[5] | Should -Be '/nv'
            $a[6] | Should -Be '/o'
        }
        It 'throws on non-zero exit' {
            $script:MakeAppxExitCode = 3
            { Invoke-MakeAppx -StagingDir 'C:\fake\staging' -OutputMsixPath 'C:\fake\out\x.msix' } |
                Should -Throw -ExpectedMessage '*makeappx pack failed*'
        }
        It '-WhatIf does not invoke the tool' {
            Invoke-MakeAppx -StagingDir 'C:\fake\staging' -OutputMsixPath 'C:\fake\out\x.msix' -WhatIf
            $script:MakeAppxCallCount | Should -Be 0
        }
    }

    Context 'Invoke-SignTool' {
        BeforeEach { Mock -ModuleName Publish.Helpers Find-WindowsSdkTool { 'signtool' } }
        It 'passes /sha1, /fd SHA256, /tr, /td SHA256 with msix path last' {
            Invoke-SignTool -MsixPath 'C:\fake\out\x.msix' -CertThumbprint 'ABCDEF0123'
            $script:SignToolCallCount | Should -Be 1
            $a = @($script:LastSignToolArgs)
            $a[0] | Should -Be 'sign'
            $a[1] | Should -Be '/sha1'
            $a[2] | Should -Be 'ABCDEF0123'
            $a[3] | Should -Be '/fd'
            $a[4] | Should -Be 'SHA256'
            $a[5] | Should -Be '/tr'
            $a[6] | Should -Be 'http://timestamp.digicert.com'
            $a[7] | Should -Be '/td'
            $a[8] | Should -Be 'SHA256'
            $a[9] | Should -Be 'C:\fake\out\x.msix'
        }
        It 'throws on non-zero exit' {
            $script:SignToolExitCode = 5
            { Invoke-SignTool -MsixPath 'C:\fake\out\x.msix' -CertThumbprint 'ABCDEF' } |
                Should -Throw -ExpectedMessage '*signtool sign failed*'
        }
        It '-WhatIf does not invoke the tool' {
            Invoke-SignTool -MsixPath 'C:\fake\out\x.msix' -CertThumbprint 'ABCDEF' -WhatIf
            $script:SignToolCallCount | Should -Be 0
        }
    }

    Context 'Invoke-DotnetPublish' {
        It 'passes -c -o /p:Deterministic=true verbatim' {
            Invoke-DotnetPublish -ProjectPath 'src/X/X.csproj' -OutputDir 'out/X' -Configuration 'Release'
            $a = @($script:LastDotnetArgs)
            $a[0] | Should -Be 'publish'
            $a[1] | Should -Be 'src/X/X.csproj'
            $a[2] | Should -Be '-c'
            $a[3] | Should -Be 'Release'
            $a[4] | Should -Be '-o'
            $a[5] | Should -Be 'out/X'
            $a[6] | Should -Be '/p:Deterministic=true'
        }
        It 'appends ExtraArgs after required args' {
            Invoke-DotnetPublish -ProjectPath 'src/Y/Y.csproj' -OutputDir 'out/Y' -Configuration 'Release' -ExtraArgs @('--self-contained', 'true', '-r', 'win-x64')
            $a = @($script:LastDotnetArgs)
            $a[7] | Should -Be '--self-contained'
            $a[8] | Should -Be 'true'
            $a[9] | Should -Be '-r'
            $a[10] | Should -Be 'win-x64'
        }
        It 'throws on non-zero exit' {
            $script:DotnetExitCode = 2
            { Invoke-DotnetPublish -ProjectPath 'src/Z/Z.csproj' -OutputDir 'out/Z' -Configuration 'Release' } |
                Should -Throw -ExpectedMessage '*dotnet publish failed*'
        }
    }

    Context 'Copy-DockerArtifact' {
        BeforeEach {
            $script:Copies = @()
            $script:Warns = 0
            Mock -ModuleName Publish.Helpers New-Item { }
            Mock -ModuleName Publish.Helpers Copy-Item {
                $script:Copies += [pscustomobject]@{ Source = $Path; Destination = $Destination }
            }
            Mock -ModuleName Publish.Helpers Write-Warning { $script:Warns++ }
        }
        It 'copies both compose files when present' {
            Mock -ModuleName Publish.Helpers Test-Path {
                if ($Path -like '*secrets*') { return $false } else { return $true }
            }
            Copy-DockerArtifact -RepoRoot 'C:\fake\repo' -DockerBundleDir 'C:\fake\bundle\docker'
            ($script:Copies.Source -join ',') | Should -Match 'docker-compose.yml'
            ($script:Copies.Source -join ',') | Should -Match 'docker-compose.dev.yml'
        }
        It 'copies .env.example when present' {
            Mock -ModuleName Publish.Helpers Test-Path {
                if ($Path -like '*secrets*') { return $false } else { return $true }
            }
            Copy-DockerArtifact -RepoRoot 'C:\fake\repo' -DockerBundleDir 'C:\fake\bundle\docker'
            ($script:Copies.Source -join ',') | Should -Match '\.env\.example'
        }
        It 'skips .env.example silently when absent' {
            Mock -ModuleName Publish.Helpers Test-Path {
                if ($Path -like '*secrets*') { return $false }
                if ($Path -like '*.env.example*') { return $false }
                return $true
            }
            Copy-DockerArtifact -RepoRoot 'C:\fake\repo' -DockerBundleDir 'C:\fake\bundle\docker'
            ($script:Copies.Source -join ',') | Should -Not -Match '\.env\.example'
        }
        It 'recursively copies deploy/docker/**' {
            Mock -ModuleName Publish.Helpers Test-Path {
                if ($Path -like '*secrets*') { return $false } else { return $true }
            }
            Copy-DockerArtifact -RepoRoot 'C:\fake\repo' -DockerBundleDir 'C:\fake\bundle\docker'
            (@($script:Copies) | Where-Object { $_.Source -like '*deploy*docker*' }).Count | Should -BeGreaterThan 0
        }
        It 'emits Write-Warning and does not copy when a secrets/ dir exists' {
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Copy-DockerArtifact -RepoRoot 'C:\fake\repo' -DockerBundleDir 'C:\fake\bundle\docker'
            $script:Warns | Should -BeGreaterThan 0
            (@($script:Copies) | Where-Object { $_.Source -like '*\secrets*' -and $_.Source -notlike '*.env.anthropic*' }).Count | Should -Be 0
        }
        It 'never copies secrets/.env.anthropic even if present' {
            Mock -ModuleName Publish.Helpers Test-Path { $true }
            Copy-DockerArtifact -RepoRoot 'C:\fake\repo' -DockerBundleDir 'C:\fake\bundle\docker'
            (@($script:Copies) | Where-Object { $_.Source -like '*.env.anthropic*' }).Count | Should -Be 0
        }
    }

    Context 'New-ManifestEntry' {
        BeforeEach {
            Mock -ModuleName Publish.Helpers Resolve-Path { [pscustomobject]@{ Path = 'C:\bundle' } }
        }
        It 'returns path as forward-slash-relative string' {
            Mock -ModuleName Publish.Helpers Get-Item { [pscustomobject]@{ FullName = 'C:\bundle\executables\X\file.dll'; Length = 42 } }
            Mock -ModuleName Publish.Helpers Get-FileHash { [pscustomobject]@{ Hash = $script:FakeHashLower.ToUpper() } }
            (New-ManifestEntry -FilePath 'C:\bundle\executables\X\file.dll' -BundleRoot 'C:\bundle').path |
                Should -Be 'executables/X/file.dll'
        }
        It 'returns size as non-negative integer' {
            Mock -ModuleName Publish.Helpers Get-Item { [pscustomobject]@{ FullName = 'C:\bundle\a.bin'; Length = 1024 } }
            Mock -ModuleName Publish.Helpers Get-FileHash { [pscustomobject]@{ Hash = $script:FakeHashLower } }
            $e = New-ManifestEntry -FilePath 'C:\bundle\a.bin' -BundleRoot 'C:\bundle'
            $e.size | Should -BeOfType [int]
            $e.size | Should -BeGreaterOrEqual 0
            $e.size | Should -Be 1024
        }
        It 'returns sha256 as 64-character lowercase hex string' {
            Mock -ModuleName Publish.Helpers Get-Item { [pscustomobject]@{ FullName = 'C:\bundle\b.bin'; Length = 7 } }
            Mock -ModuleName Publish.Helpers Get-FileHash { [pscustomobject]@{ Hash = $script:FakeHashLower.ToUpper() } }
            (New-ManifestEntry -FilePath 'C:\bundle\b.bin' -BundleRoot 'C:\bundle').sha256 |
                Should -Match '^[0-9a-f]{64}$'
        }
        It 'calls Get-FileHash with -Algorithm SHA256' {
            Mock -ModuleName Publish.Helpers Get-Item { [pscustomobject]@{ FullName = 'C:\bundle\c.bin'; Length = 8 } }
            $script:AlgoSeen = $null
            Mock -ModuleName Publish.Helpers Get-FileHash {
                $script:AlgoSeen = $Algorithm
                [pscustomobject]@{ Hash = $script:FakeHashLower }
            }
            $null = New-ManifestEntry -FilePath 'C:\bundle\c.bin' -BundleRoot 'C:\bundle'
            $script:AlgoSeen | Should -Be 'SHA256'
        }
    }

    Context 'Write-PublishManifest' {
        BeforeEach {
            $script:WrittenValue = $null
            Mock -ModuleName Publish.Helpers Set-Content { $script:WrittenValue = $Value }
        }
        It 'writes JSON with only { version, files } and excludes manifest.json' {
            $f1 = [pscustomobject]@{ FullName = 'C:\bundle\a.txt' }
            $f2 = [pscustomobject]@{ FullName = 'C:\bundle\b.txt' }
            $mf = [pscustomobject]@{ FullName = 'C:\bundle\manifest.json' }
            Mock -ModuleName Publish.Helpers Get-ChildItem { @($f1, $f2, $mf) }
            Mock -ModuleName Publish.Helpers New-ManifestEntry {
                [pscustomobject]@{ path = [System.IO.Path]::GetFileName($FilePath); size = 1; sha256 = ('b' * 64) }
            }
            $r = Write-PublishManifest -BundleRoot 'C:\bundle' -Version '1.2.3.4'
            $r | Should -Be (Join-Path 'C:\bundle' 'manifest.json')
            $p = $script:WrittenValue | ConvertFrom-Json
            $topLevelNames = ($p.PSObject.Properties | ForEach-Object { $_.Name }) | Sort-Object
            ($topLevelNames -join ',') | Should -Be 'files,version'
            $p.version | Should -Be '1.2.3.4'
            $p.files.Count | Should -Be 2
            ($p.files.path -contains 'manifest.json') | Should -BeFalse
        }
        It 'sorts files ascending by path' {
            $z = [pscustomobject]@{ FullName = 'C:\bundle\z.txt' }
            $a = [pscustomobject]@{ FullName = 'C:\bundle\a.txt' }
            $m = [pscustomobject]@{ FullName = 'C:\bundle\m.txt' }
            Mock -ModuleName Publish.Helpers Get-ChildItem { @($z, $a, $m) }
            Mock -ModuleName Publish.Helpers New-ManifestEntry {
                [pscustomobject]@{ path = [System.IO.Path]::GetFileName($FilePath); size = 1; sha256 = ('c' * 64) }
            }
            $null = Write-PublishManifest -BundleRoot 'C:\bundle' -Version '1.0.0.0'
            $p = $script:WrittenValue | ConvertFrom-Json
            $p.files[0].path | Should -Be 'a.txt'
            $p.files[1].path | Should -Be 'm.txt'
            $p.files[2].path | Should -Be 'z.txt'
        }
        It 'each file entry has exactly path, size, sha256' {
            $a = [pscustomobject]@{ FullName = 'C:\bundle\a.txt' }
            Mock -ModuleName Publish.Helpers Get-ChildItem { @($a) }
            Mock -ModuleName Publish.Helpers New-ManifestEntry {
                [pscustomobject]@{ path = 'a.txt'; size = 3; sha256 = ('d' * 64) }
            }
            $null = Write-PublishManifest -BundleRoot 'C:\bundle' -Version '1.0.0.0'
            $p = $script:WrittenValue | ConvertFrom-Json
            $names = ($p.files[0].PSObject.Properties | ForEach-Object { $_.Name }) | Sort-Object
            ($names -join ',') | Should -Be 'path,sha256,size'
        }
        It 'structural stability only (Q3) - 64-char lowercase hex, present path, non-negative size' {
            $a = [pscustomobject]@{ FullName = 'C:\bundle\x.dll' }
            Mock -ModuleName Publish.Helpers Get-ChildItem { @($a) }
            Mock -ModuleName Publish.Helpers New-ManifestEntry {
                [pscustomobject]@{ path = 'x.dll'; size = 42; sha256 = ('e' * 64) }
            }
            $null = Write-PublishManifest -BundleRoot 'C:\bundle' -Version '1.0.0.0'
            $p = $script:WrittenValue | ConvertFrom-Json
            $p.files[0].path | Should -Not -BeNullOrEmpty
            $p.files[0].size | Should -BeGreaterOrEqual 0
            $p.files[0].sha256 | Should -Match '^[0-9a-f]{64}$'
        }
    }

    Context 'Copy-InstallScriptsIntoBundle' {
        BeforeEach {
            $script:CopyCalls = New-Object System.Collections.Generic.List[object]
            Mock -ModuleName Publish.Helpers Copy-Item {
                $script:CopyCalls.Add([pscustomobject]@{ Src = $LiteralPath; Dst = $Destination })
            }
            Mock -ModuleName Publish.Helpers Test-Path { $true }
        }
        It 'copies Install.ps1, Uninstall.ps1, and Install.Helpers.psm1 in order' {
            Copy-InstallScriptsIntoBundle -RepoRoot 'C:\repo' -BundleRoot 'C:\bundle'
            $script:CopyCalls.Count | Should -Be 3
            $script:CopyCalls[0].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Install.ps1')
            $script:CopyCalls[0].Dst | Should -Be (Join-Path 'C:\bundle' 'Install.ps1')
            $script:CopyCalls[1].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Uninstall.ps1')
            $script:CopyCalls[1].Dst | Should -Be (Join-Path 'C:\bundle' 'Uninstall.ps1')
            $script:CopyCalls[2].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Install.Helpers.psm1')
            $script:CopyCalls[2].Dst | Should -Be (Join-Path 'C:\bundle' 'Install.Helpers.psm1')
        }
        It 'throws with the missing path when a source file is absent' {
            $missing = Join-Path 'C:\repo\scripts' 'Uninstall.ps1'
            Mock -ModuleName Publish.Helpers Test-Path {
                param($LiteralPath)
                if ($LiteralPath -eq $missing) { $false } else { $true }
            }
            { Copy-InstallScriptsIntoBundle -RepoRoot 'C:\repo' -BundleRoot 'C:\bundle' } |
                Should -Throw "*$missing*"
        }
        It 'produces zero Copy-Item invocations under -WhatIf' {
            Copy-InstallScriptsIntoBundle -RepoRoot 'C:\repo' -BundleRoot 'C:\bundle' -WhatIf
            $script:CopyCalls.Count | Should -Be 0
        }
    }
}
