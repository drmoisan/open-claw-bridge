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

        # Shim state for the external tool the module may invoke.
        $script:DotnetCallCount = 0
        $script:DotnetExitCode = 0
        $script:LastDotnetArgs = $null

        # Define a global shim function for the dotnet CLI called via
        # '& dotnet args ...'. It writes its arguments into $script: state and
        # honors the exit-code variable set in BeforeEach.
        function global:dotnet {
            $script:LastDotnetArgs = $args
            $script:DotnetCallCount++
            $global:LASTEXITCODE = $script:DotnetExitCode
        }

        Import-Module $script:ModulePath -Force

        $script:FakeHashLower = ('a' * 64)
    }

    AfterAll {
        Remove-Item -Path 'Function:\global:dotnet' -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $script:DotnetCallCount = 0; $script:DotnetExitCode = 0; $script:LastDotnetArgs = $null
    }

    Context 'Module exports' {
        It 'exports the expected 7 helper functions' {
            $expected = @(
                'Invoke-DotnetPublish', 'Invoke-DotnetExe',
                'Resolve-CertThumbprint', 'Copy-DockerArtifact',
                'Copy-InstallScriptsIntoBundle', 'New-ManifestEntry', 'Write-PublishManifest'
            ) | Sort-Object
            $actual = (Get-Command -Module Publish.Helpers).Name | Sort-Object
            ($actual -join ',') | Should -Be ($expected -join ',')
        }
    }

    # The Windows SDK / MSIX tooling tests (Find-WindowsSdkTool,
    # Get-StampedAppxManifestXml, Invoke-VersionStamp, Invoke-LayoutAssembly,
    # Invoke-MakePri, Invoke-MakeAppx, Invoke-SignTool) were relocated to the
    # sibling file tests/scripts/Publish.Msix.Tests.ps1 alongside the extracted
    # scripts/Publish.Msix.psm1 module.

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

    # Resolve-CertThumbprint tests were extracted to the sibling file
    # tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1 so both files stay
    # under the 500-line cap.

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
        It 'copies Install.ps1, Uninstall.ps1, Install.Helpers.psm1, and Install.Preflight.psm1 in order' {
            Copy-InstallScriptsIntoBundle -RepoRoot 'C:\repo' -BundleRoot 'C:\bundle'
            $script:CopyCalls.Count | Should -Be 4
            $script:CopyCalls[0].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Install.ps1')
            $script:CopyCalls[0].Dst | Should -Be (Join-Path 'C:\bundle' 'Install.ps1')
            $script:CopyCalls[1].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Uninstall.ps1')
            $script:CopyCalls[1].Dst | Should -Be (Join-Path 'C:\bundle' 'Uninstall.ps1')
            $script:CopyCalls[2].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Install.Helpers.psm1')
            $script:CopyCalls[2].Dst | Should -Be (Join-Path 'C:\bundle' 'Install.Helpers.psm1')
            $script:CopyCalls[3].Src | Should -Be (Join-Path 'C:\repo\scripts' 'Install.Preflight.psm1')
            $script:CopyCalls[3].Dst | Should -Be (Join-Path 'C:\bundle' 'Install.Preflight.psm1')
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

