#Requires -Version 5.1
<#
.SYNOPSIS
    Pester unit tests for scripts/New-MsixDevCert.ps1.

.DESCRIPTION
    Tests the helper functions defined in New-MsixDevCert.ps1 using global function shims
    for New-SelfSignedCertificate, Export-PfxCertificate, Export-Certificate, and
    Import-Certificate so that no actual certificate operations are performed during testing.
#>

Describe 'New-MsixDevCert.ps1' {

    BeforeAll {
        # --- Define global shims for certificate cmdlets BEFORE dot-sourcing ---
        $script:SelfSignedCertArgs = @{}
        $script:ExportPfxArgs = @{}

        # Create a fake certificate object for return values
        $script:FakeCert = [pscustomobject]@{
            Thumbprint = 'AABBCCDDEEFF00112233445566778899AABBCCDD'
            Subject    = 'CN=OpenClaw, O=OpenClaw, C=US'
        }

        # Shim New-SelfSignedCertificate: capture bound parameters and return fake cert
        function global:New-SelfSignedCertificate {
            [CmdletBinding()]
            param(
                [string]$Subject,
                [string]$CertStoreLocation,
                [string]$Type,
                [string]$KeyUsage,
                [string]$KeyAlgorithm,
                [int]$KeyLength,
                [string]$HashAlgorithm,
                [datetime]$NotAfter
            )
            $script:SelfSignedCertArgs = $PSBoundParameters
            return $script:FakeCert
        }

        # Shim Export-PfxCertificate: capture parameters and create the output file
        function global:Export-PfxCertificate {
            [CmdletBinding()]
            param(
                $Cert,
                [string]$FilePath,
                [System.Security.SecureString]$Password
            )
            $script:ExportPfxArgs = $PSBoundParameters
            # Use $Cert to satisfy PSReviewUnusedParameter (shim pattern: param declared for signature compatibility)
            $null = $Cert
            # Create the file so that path-based assertions in calling code succeed
            New-Item -ItemType File -Force -Path $FilePath | Out-Null
        }

        # Shim Export-Certificate: create output file
        function global:Export-Certificate {
            [CmdletBinding()]
            param($Cert, [string]$FilePath, [string]$Type)
            # Use parameters to satisfy PSReviewUnusedParameter in shim context
            $null = $Cert
            $null = $Type
            New-Item -ItemType File -Force -Path $FilePath | Out-Null
        }

        # Shim Import-Certificate: no-op (avoids LocalMachine\Root write)
        function global:Import-Certificate {
            [CmdletBinding()]
            param([string]$FilePath, [string]$CertStoreLocation)
            # Use parameters to satisfy PSReviewUnusedParameter in shim context
            $null = $FilePath
            $null = $CertStoreLocation
            return $script:FakeCert
        }

        # Dot-source the script to load its helper functions into the current scope
        . (Join-Path $PSScriptRoot '../../scripts/New-MsixDevCert.ps1')
    }

    BeforeEach {
        # Reset captured arguments before each test
        $script:SelfSignedCertArgs = @{}
        $script:ExportPfxArgs = @{}
    }

    It 'passes -Subject CN to New-SelfSignedCertificate' {
        # Arrange: a custom subject to verify it is forwarded verbatim
        $expectedSubject = 'CN=TestSubject, O=Test, C=CA'

        # Act: call the helper function directly with the custom subject
        New-SigningCertificate -Subject $expectedSubject

        # Assert: the captured Subject arg must exactly equal our expected value
        $script:SelfSignedCertArgs['Subject'] | Should -Be $expectedSubject
    }

    It 'exports PFX to the specified OutputDir' {
        # Arrange: an isolated output directory for the export
        $testOutputDir = Join-Path $TestDrive 'cert-export-test'
        New-Item -ItemType Directory -Force -Path $testOutputDir | Out-Null
        $pfxPwd = New-Object System.Security.SecureString

        # Act: call the certificate export helper
        Export-SigningCertificate -Cert $script:FakeCert -OutputDir $testOutputDir -PfxPassword $pfxPwd

        # Assert: the captured FilePath for the PFX must be inside testOutputDir
        $pfxFilePath = $script:ExportPfxArgs['FilePath']
        $pfxFilePath | Should -Not -BeNullOrEmpty
        [System.IO.Path]::GetDirectoryName($pfxFilePath) | Should -Be $testOutputDir
    }
}
