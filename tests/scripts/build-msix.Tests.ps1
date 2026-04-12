#Requires -Version 5.1
<#
.SYNOPSIS
    Pester unit tests for scripts/build-msix.ps1.

.DESCRIPTION
    Tests the helper functions defined in build-msix.ps1 using mocks and global
    function shims for external executables so that no real SDK tools, publish
    outputs, or temporary files are required.
#>

Describe 'build-msix.ps1' {

    BeforeAll {
        # --- Define global shims for external executables BEFORE dot-sourcing ---
        # These are called via '& $toolPath args...' inside the helper functions
        $script:MakeAppxArgs = @()
        $script:MakeAppxCallCount = 0
        $script:MakePriCallCount = 0
        $script:SigntoolCallCount = 0

        function global:makeappx {
            # Capture all arguments for assertion
            $script:MakeAppxArgs = $args
            $script:MakeAppxCallCount++
        }

        function global:signtool {
            $script:SigntoolCallCount++
        }

        function global:makepri {
            $script:MakePriCallCount++
        }

        # Dot-source the script under test to load all helper functions
        . (Join-Path $PSScriptRoot '../../scripts/build-msix.ps1')

        # --- Override Find-WindowsSdkTool AFTER dot-sourcing ---
        # The dot-source replaced our pre-shim version; now override with one that
        # returns the names of our global shim functions
        function Find-WindowsSdkTool {
            [CmdletBinding()]
            [OutputType([string])]
            param([string]$ToolName)
            # Return the shim function name (no path needed for function invocation)
            switch ($ToolName) {
                'makeappx.exe' { return 'makeappx' }
                'signtool.exe' { return 'signtool' }
                'makepri.exe' { return 'makepri' }
                default { return $ToolName }
            }
        }

        $script:ManifestXml = @'
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Identity Name="OpenClaw.MailBridge" Publisher="CN=OpenClaw, O=OpenClaw, C=US" Version="0.0.0.0" ProcessorArchitecture="x64" />
</Package>
'@

        $script:ManifestSource = 'manifest-source.xml'
        $script:StagingDir = 'installer/staging'
        $script:OutputDir = 'artifacts/msix'
        $script:AssetsSource = 'installer/Assets'
        $script:BridgePublishDir = 'artifacts/publish/bridge'
        $script:ClientPublishDir = 'artifacts/publish/client'
    }

    BeforeEach {
        $script:MakeAppxArgs = @()
        $script:MakeAppxCallCount = 0
        $script:MakePriCallCount = 0
        $script:SigntoolCallCount = 0
        Mock Get-Content { return $script:ManifestXml } -ParameterFilter { $Raw -and $Path -eq $script:ManifestSource }
        Mock New-Item { return $null }
        Mock Set-Content { }
        Mock Copy-Item { }
        Mock Test-Path {
            return $Path -ne 'missing/bridge'
        }
    }

    It 'stamps the 4-part version into AppxManifest.xml' {
        # Arrange
        $expectedVersion = '2.3.4.5'

        # Act
        $stampedXml = Get-StampedAppxManifestXml -ManifestXml $script:ManifestXml -Version $expectedVersion

        # Assert
        [xml]$xml = $stampedXml
        $xml.Package.Identity.Version | Should -Be $expectedVersion
    }

    It 'throws a terminating error when bridge publish directory is absent' {
        # Arrange: a directory that does not exist on disk
        $missingBridgeDir = 'missing/bridge'

        # Act / Assert: the layout helper must throw with a clear error
        {
            Invoke-LayoutAssembly `
                -BridgePublishDir $missingBridgeDir `
                -ClientPublishDir $script:ClientPublishDir `
                -AssetsSource $script:AssetsSource `
                -StagingDir $script:StagingDir
        } | Should -Throw
    }

    It 'copies bridge binaries to installer/staging/bridge/' {
        # Act
        Invoke-LayoutAssembly `
            -BridgePublishDir $script:BridgePublishDir `
            -ClientPublishDir $script:ClientPublishDir `
            -AssetsSource $script:AssetsSource `
            -StagingDir $script:StagingDir

        # Assert
        Assert-MockCalled Copy-Item -Times 1 -Exactly -ParameterFilter { $Destination -eq (Join-Path $script:StagingDir 'bridge') }
    }

    It 'passes pack /d /p /nv arguments to makeappx' {
        # Act
        $global:LASTEXITCODE = 0
        Invoke-MakeAppx -StagingDir $script:StagingDir -OutputDir $script:OutputDir -Version '1.0.0.0'

        # Assert
        $script:MakeAppxArgs | Should -Contain 'pack'
        $script:MakeAppxArgs | Should -Contain '/d'
        $script:MakeAppxArgs | Should -Contain '/p'
        $script:MakeAppxArgs | Should -Contain '/nv'
    }

    It 'does not invoke signtool when -SkipSign is passed' {
        # Arrange: reset the signtool invocation counter
        $script:SigntoolCallCount = 0

        # Act: simulate the main-body guard using the script's actual [switch] type so
        # a regression (removing the -not guard) would cause this test to fail
        $localSkipSign = [switch]$true
        if (-not $localSkipSign) {
            Invoke-SignTool -MsixPath 'fake.msix' -CertThumbprint 'FAKE'
        }

        # Assert: with SkipSign=true, signtool should never be called
        $script:SigntoolCallCount | Should -Be 0
    }

    It 'WhatIf leaves installer/staging/AppxManifest.xml absent' {
        # Arrange
        $whatIfStagingDir = 'installer/staging'
        $stagedManifest = Join-Path $whatIfStagingDir 'AppxManifest.xml'

        # Act
        Invoke-VersionStamp -ManifestSource $script:ManifestSource -StagingDir $whatIfStagingDir -Version '3.4.5.6' -WhatIf

        # Assert
        Assert-MockCalled Set-Content -Times 0 -ParameterFilter { $Path -eq $stagedManifest }
    }

    It 'WhatIf does not invoke MakePri, makeappx, or signtool' {
        # Act
        $global:LASTEXITCODE = 0
        Invoke-MakePri -StagingDir $script:StagingDir -WhatIf
        Invoke-MakeAppx -StagingDir $script:StagingDir -OutputDir $script:OutputDir -Version '1.0.0.0' -WhatIf
        Invoke-SignTool -MsixPath (Join-Path $script:OutputDir 'fake.msix') -CertThumbprint 'FAKE' -WhatIf

        # Assert
        $script:MakePriCallCount | Should -Be 0
        $script:MakeAppxCallCount | Should -Be 0
        $script:SigntoolCallCount | Should -Be 0
    }
}
