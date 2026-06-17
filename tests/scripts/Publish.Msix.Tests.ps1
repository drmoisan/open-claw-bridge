#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Publish.Msix.psm1.

.DESCRIPTION
    Tests each exported Windows SDK / MSIX helper using mocks and function shims
    for external executables so no real SDK tools, publish outputs, or temporary
    files are required. All inter-test state is held in $script: variables scoped
    to the outer Describe block; no $global: state is used.
#>

Describe 'Publish.Msix.psm1' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Msix.psm1'

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

        Import-Module $script:ModulePath -Force

        $script:SampleManifestText = @'
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Identity Name="OpenClaw.MailBridge" Publisher="CN=OpenClaw" Version="0.0.0.0" ProcessorArchitecture="x64" />
</Package>
'@
        [xml]$script:SampleManifestXml = $script:SampleManifestText
    }

    AfterAll {
        foreach ($fn in 'makepri', 'makeappx', 'signtool') {
            Remove-Item -Path "Function:\global:$fn" -ErrorAction SilentlyContinue
        }
    }

    BeforeEach {
        $script:MakePriCallCount = 0; $script:MakePriExitCode = 0; $script:LastMakePriArgs = $null
        $script:MakeAppxCallCount = 0; $script:MakeAppxExitCode = 0; $script:LastMakeAppxArgs = $null
        $script:SignToolCallCount = 0; $script:SignToolExitCode = 0; $script:LastSignToolArgs = $null
    }

    Context 'Module exports' {
        It 'exports the expected 7 helper functions' {
            $expected = @(
                'Find-WindowsSdkTool', 'Get-StampedAppxManifestXml', 'Invoke-VersionStamp',
                'Invoke-LayoutAssembly', 'Invoke-MakePri', 'Invoke-MakeAppx',
                'Invoke-SignTool'
            ) | Sort-Object
            $actual = (Get-Command -Module Publish.Msix).Name | Sort-Object
            ($actual -join ',') | Should -Be ($expected -join ',')
        }
    }

    Context 'Find-WindowsSdkTool' {
        It 'returns the SDK-bin match when ProgramFiles(x86) has a matching x64 binary' {
            Mock -ModuleName Publish.Msix Test-Path { $true }
            Mock -ModuleName Publish.Msix Get-ChildItem {
                [pscustomobject]@{ FullName = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\makeappx.exe' }
            }
            Find-WindowsSdkTool -ToolName 'makeappx.exe' |
                Should -Be 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\makeappx.exe'
        }
        It 'falls back to Get-Command when the SDK bin root is missing' {
            Mock -ModuleName Publish.Msix Test-Path { $false }
            Mock -ModuleName Publish.Msix Get-Command {
                [pscustomobject]@{ Source = 'C:\fake\on-path\makeappx.exe' }
            }
            Find-WindowsSdkTool -ToolName 'makeappx.exe' |
                Should -Be 'C:\fake\on-path\makeappx.exe'
        }
        It 'throws when the tool cannot be located anywhere' {
            Mock -ModuleName Publish.Msix Test-Path { $false }
            Mock -ModuleName Publish.Msix Get-Command { $null }
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
            Mock -ModuleName Publish.Msix Get-Content { $script:SampleManifestText }
            Mock -ModuleName Publish.Msix Test-Path { $true }
            Mock -ModuleName Publish.Msix New-Item { }
            $script:WrittenPath = $null
            $script:WrittenValue = $null
            Mock -ModuleName Publish.Msix Set-Content {
                $script:WrittenPath = $Path
                $script:WrittenValue = $Value
            }
        }
        It 'writes stamped XML to <stagingDir>/AppxManifest.xml' {
            $r = Invoke-VersionStamp -ManifestSourcePath 'C:\fake\source.xml' -StagingDir 'C:\fake\staging' -Version '1.2.3.4'
            $r | Should -Be (Join-Path 'C:\fake\staging' 'AppxManifest.xml')
            Assert-MockCalled -ModuleName Publish.Msix Set-Content -Times 1 -Scope It
            $script:WrittenValue | Should -Match 'Version="1.2.3.4"'
        }
        It '-WhatIf leaves the staging file absent' {
            Invoke-VersionStamp -ManifestSourcePath 'C:\fake\source.xml' -StagingDir 'C:\fake\staging' -Version '2.0.0.0' -WhatIf
            Assert-MockCalled -ModuleName Publish.Msix Set-Content -Times 0 -Scope It
        }
    }

    Context 'Invoke-LayoutAssembly' {
        It 'throws when bridge publish dir is missing' {
            Mock -ModuleName Publish.Msix Test-Path { $false } -ParameterFilter { $Path -eq 'C:\missing\bridge' }
            Mock -ModuleName Publish.Msix Test-Path { $true } -ParameterFilter { $Path -ne 'C:\missing\bridge' }
            { Invoke-LayoutAssembly -BridgePublishDir 'C:\missing\bridge' -ClientPublishDir 'C:\fake\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging' } |
                Should -Throw -ExpectedMessage '*Bridge publish directory not found*'
        }
        It 'throws when client publish dir is missing' {
            Mock -ModuleName Publish.Msix Test-Path { $true } -ParameterFilter { $Path -eq 'C:\fake\bridge' }
            Mock -ModuleName Publish.Msix Test-Path { $false } -ParameterFilter { $Path -eq 'C:\missing\client' }
            Mock -ModuleName Publish.Msix Test-Path { $true } -ParameterFilter { $Path -ne 'C:\fake\bridge' -and $Path -ne 'C:\missing\client' }
            { Invoke-LayoutAssembly -BridgePublishDir 'C:\fake\bridge' -ClientPublishDir 'C:\missing\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging' } |
                Should -Throw -ExpectedMessage '*Client publish directory not found*'
        }
        It 'calls Copy-Item for bridge, client, and assets on success' {
            Mock -ModuleName Publish.Msix Test-Path { $true }
            Mock -ModuleName Publish.Msix Remove-Item { }
            Mock -ModuleName Publish.Msix New-Item { }
            Mock -ModuleName Publish.Msix Copy-Item { }
            Invoke-LayoutAssembly -BridgePublishDir 'C:\fake\bridge' -ClientPublishDir 'C:\fake\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging'
            Assert-MockCalled -ModuleName Publish.Msix Copy-Item -Times 3 -Scope It
        }
        It '-WhatIf skips all copies' {
            Mock -ModuleName Publish.Msix Test-Path { $true }
            Mock -ModuleName Publish.Msix Remove-Item { }
            Mock -ModuleName Publish.Msix New-Item { }
            Mock -ModuleName Publish.Msix Copy-Item { }
            Invoke-LayoutAssembly -BridgePublishDir 'C:\fake\bridge' -ClientPublishDir 'C:\fake\client' -AssetsDir 'C:\fake\assets' -StagingDir 'C:\fake\staging' -WhatIf
            Assert-MockCalled -ModuleName Publish.Msix Copy-Item -Times 0 -Scope It
        }
    }

    Context 'Invoke-MakePri' {
        BeforeEach { Mock -ModuleName Publish.Msix Find-WindowsSdkTool { 'makepri' } }
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
            Mock -ModuleName Publish.Msix Find-WindowsSdkTool { 'makeappx' }
            Mock -ModuleName Publish.Msix Test-Path { $true }
            Mock -ModuleName Publish.Msix New-Item { }
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
        BeforeEach { Mock -ModuleName Publish.Msix Find-WindowsSdkTool { 'signtool' } }
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
}
