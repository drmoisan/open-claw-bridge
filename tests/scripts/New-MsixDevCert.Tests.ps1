#Requires -Version 5.1
<#
.SYNOPSIS
    Pester unit tests for scripts/New-MsixDevCert.ps1.

.DESCRIPTION
    Tests the helper functions defined in New-MsixDevCert.ps1 using global function shims
    so that no actual certificate operations or temporary files are required during testing.
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

        # Shim Export-PfxCertificate: capture parameters
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
        }

        # Shim Export-Certificate: capture parameters
        function global:Export-Certificate {
            [CmdletBinding()]
            param($Cert, [string]$FilePath, [string]$Type)
            # Use parameters to satisfy PSReviewUnusedParameter in shim context
            $null = $Cert
            $script:ExportCerArgs = $PSBoundParameters
            $null = $Type
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

        # Dot-source the script to load its helper functions into the current
        # scope. The Main block does not run under dot-source, so
        # Save-CertThumbprintToEnv (defined above the Main guard) is available.
        . (Join-Path $PSScriptRoot '../../scripts/New-MsixDevCert.ps1')
    }

    BeforeEach {
        # Reset captured arguments before each test
        $script:SelfSignedCertArgs = @{}
        $script:ExportPfxArgs = @{}
        $script:ExportCerArgs = @{}
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
        # Arrange
        $testOutputDir = 'artifacts/cert-export-test'
        $pfxPwd = New-Object System.Security.SecureString

        # Act: call the certificate export helper
        $exported = Export-SigningCertificate -Cert $script:FakeCert -OutputDir $testOutputDir -PfxPassword $pfxPwd

        # Assert
        $pfxFilePath = $script:ExportPfxArgs['FilePath']
        $pfxFilePath | Should -Not -BeNullOrEmpty
        [System.IO.Path]::GetDirectoryName($pfxFilePath) | Should -Be ([System.IO.Path]::GetDirectoryName((Join-Path $testOutputDir 'OpenClaw.MailBridge.pfx')))
        $exported.PfxPath | Should -Be (Join-Path $testOutputDir 'OpenClaw.MailBridge.pfx')
        $exported.CerPath | Should -Be (Join-Path $testOutputDir 'OpenClaw.MailBridge.cer')
    }

    Context 'Save-CertThumbprintToEnv (AC-5)' {
        BeforeEach {
            # Mock the .env file seam so nothing is read from or written to disk.
            $script:SetEnvArgs = $null
            $script:WriteEnvContent = $null
            Mock Read-EnvFileContent {
                param([string]$Path)
                $null = $Path
                return [string[]]@('OPENCLAW_PACKAGE_VERSION=1.0.2.0', '# comment')
            }
            Mock Set-EnvFileValue {
                param([string[]]$Content, [string]$Key, [string]$Value)
                $script:SetEnvArgs = [pscustomobject]@{ Content = @($Content); Key = $Key; Value = $Value }
                return [string[]]@(@($Content) + "$Key=$Value")
            }
            Mock Write-EnvFileContent {
                param([string]$Path, [string[]]$Content)
                $null = $Path
                $script:WriteEnvContent = @($Content)
            }
        }

        It 'persists OPENCLAW_CERT_THUMBPRINT with the cert thumbprint via Set-EnvFileValue' {
            $thumb = 'AABBCCDDEEFF00112233445566778899AABBCCDD'
            Save-CertThumbprintToEnv -Thumbprint $thumb -EnvPath 'C:\fake\.env'

            Assert-MockCalled Set-EnvFileValue -Times 1 -Scope It
            $script:SetEnvArgs.Key | Should -Be 'OPENCLAW_CERT_THUMBPRINT'
            $script:SetEnvArgs.Value | Should -Be $thumb
        }

        It 'writes the updated content via the Write-EnvFileContent seam (no disk write)' {
            $thumb = 'AABBCCDDEEFF00112233445566778899AABBCCDD'
            Save-CertThumbprintToEnv -Thumbprint $thumb -EnvPath 'C:\fake\.env'

            Assert-MockCalled Write-EnvFileContent -Times 1 -Scope It
            ($script:WriteEnvContent -join "`n") | Should -Match "OPENCLAW_CERT_THUMBPRINT=$thumb"
        }

        It 'preserves existing keys when persisting (passes prior content to Set-EnvFileValue)' {
            Save-CertThumbprintToEnv -Thumbprint 'DEAD00' -EnvPath 'C:\fake\.env'
            ($script:SetEnvArgs.Content -contains 'OPENCLAW_PACKAGE_VERSION=1.0.2.0') | Should -BeTrue
        }

        It '-WhatIf does not write to the .env seam' {
            Save-CertThumbprintToEnv -Thumbprint 'DEAD00' -EnvPath 'C:\fake\.env' -WhatIf
            Assert-MockCalled Write-EnvFileContent -Times 0 -Scope It
        }
    }
}
