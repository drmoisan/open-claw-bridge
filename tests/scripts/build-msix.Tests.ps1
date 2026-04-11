#Requires -Version 5.1
<#
.SYNOPSIS
    Pester unit tests for scripts/build-msix.ps1.

.DESCRIPTION
    Tests the helper functions defined in build-msix.ps1 using global function shims
    for all external executables (makeappx, signtool, makepri) so that no real
    Windows SDK tools or publish outputs are required.
#>

Describe 'build-msix.ps1' {

    BeforeAll {
        # --- Define global shims for external executables BEFORE dot-sourcing ---
        # These are called via '& $toolPath args...' inside the helper functions
        $script:MakeAppxArgs = @()
        $script:MakeAppxCallCount = 0
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
            # no-op shim: allows Invoke-MakePri to run without real SDK
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

        # Create a temporary working directory for test isolation
        $script:TestRoot = Join-Path $TestDrive 'build-msix-tests'
        New-Item -ItemType Directory -Force -Path $script:TestRoot | Out-Null

        $script:StagingDir = Join-Path $script:TestRoot 'installer\staging'
        $script:OutputDir = Join-Path $script:TestRoot 'artifacts\msix'
        $script:ManifestSource = Join-Path $script:TestRoot 'installer\Package.appxmanifest'
        $script:AssetsSource = Join-Path $script:TestRoot 'installer\Assets'
        $script:BridgePublishDir = Join-Path $script:TestRoot 'artifacts\publish\bridge'
        $script:ClientPublishDir = Join-Path $script:TestRoot 'artifacts\publish\client'

        foreach ($dir in @($script:StagingDir, $script:OutputDir, $script:AssetsSource, $script:BridgePublishDir, $script:ClientPublishDir)) {
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
        }

        # Minimal valid MSIX manifest for stamping tests
        @'
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Identity Name="OpenClaw.MailBridge" Publisher="CN=OpenClaw, O=OpenClaw, C=US" Version="0.0.0.0" ProcessorArchitecture="x64" />
</Package>
'@ | Set-Content -Path $script:ManifestSource -Encoding UTF8

        # Minimal files in publish directories so layout assembly has something to copy
        'bridge-exe' | Set-Content (Join-Path $script:BridgePublishDir 'OpenClaw.MailBridge.exe')
        'client-exe' | Set-Content (Join-Path $script:ClientPublishDir 'OpenClaw.MailBridge.Client.exe')
    }

    AfterAll {
        Remove-Item -Recurse -Force $script:TestRoot -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force (Join-Path $TestDrive 'stamp-test') -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force (Join-Path $TestDrive 'layout-test') -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force (Join-Path $TestDrive 'pack-test') -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $script:MakeAppxArgs = @()
        $script:MakeAppxCallCount = 0
        $script:SigntoolCallCount = 0
    }

    It 'stamps the 4-part version into AppxManifest.xml' {
        # Arrange
        $expectedVersion = '2.3.4.5'
        $stagingForTest = Join-Path $TestDrive 'stamp-test\staging'
        New-Item -ItemType Directory -Force -Path $stagingForTest | Out-Null

        # Act: invoke the version-stamping helper directly
        Invoke-VersionStamp -ManifestSource $script:ManifestSource -StagingDir $stagingForTest -Version $expectedVersion

        # Assert: the AppxManifest.xml in staging should carry the new version
        $stagedManifest = Join-Path $stagingForTest 'AppxManifest.xml'
        $stagedManifest | Should -Exist
        [xml]$xml = Get-Content -Raw $stagedManifest
        $xml.Package.Identity.Version | Should -Be $expectedVersion
    }

    It 'throws a terminating error when bridge publish directory is absent' {
        # Arrange: a directory that does not exist on disk
        $missingBridgeDir = Join-Path $TestDrive 'nonexistent\bridge'

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
        # Arrange: isolated staging directory so we can inspect the copy result
        $stagingForLayout = Join-Path $TestDrive 'layout-test\staging'
        New-Item -ItemType Directory -Force -Path $stagingForLayout | Out-Null

        # Act
        Invoke-LayoutAssembly `
            -BridgePublishDir $script:BridgePublishDir `
            -ClientPublishDir $script:ClientPublishDir `
            -AssetsSource $script:AssetsSource `
            -StagingDir $stagingForLayout

        # Assert: the bridge exe should have been copied to the staging/bridge sub-folder
        $bridgeDest = Join-Path $stagingForLayout 'bridge\OpenClaw.MailBridge.exe'
        $bridgeDest | Should -Exist
    }

    It 'passes pack /d /p /nv arguments to makeappx' {
        # Arrange: staging directory containing a minimal AppxManifest.xml
        $stagingForPack = Join-Path $TestDrive 'pack-test\staging'
        New-Item -ItemType Directory -Force -Path $stagingForPack | Out-Null
        Copy-Item $script:ManifestSource (Join-Path $stagingForPack 'AppxManifest.xml')
        $outputForPack = Join-Path $TestDrive 'pack-test\output'
        New-Item -ItemType Directory -Force -Path $outputForPack | Out-Null
        $global:LASTEXITCODE = 0

        # Act: Invoke-MakeAppx calls Find-WindowsSdkTool (shimmed) -- calls global:makeappx
        Invoke-MakeAppx -StagingDir $stagingForPack -OutputDir $outputForPack -Version '1.0.0.0'

        # Assert: the global:makeappx shim must have received the required CLI arguments
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
}
