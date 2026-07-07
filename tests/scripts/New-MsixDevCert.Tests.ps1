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
                # Production-parity return shape: Publish.Env.psm1's real
                # Read-EnvFileContent returns via the unary-comma idiom
                # (return , ([string[]]@($lines))), which yields a single,
                # pipeline-safe string[] rather than letting PowerShell unroll it.
                return , ([string[]]@('OPENCLAW_PACKAGE_VERSION=1.0.2.0', '# comment'))
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

    Context 'Save-CertThumbprintToEnv multi-line regression (AC-5)' {
        BeforeEach {
            # Mock only the .env file-I/O seam (Read-EnvFileContent,
            # Write-EnvFileContent). Set-EnvFileValue is left unmocked so this
            # context exercises the REAL implementation from Publish.Env.psm1
            # (imported by New-MsixDevCert.ps1's dot-sourced module import) as
            # the update mechanism.
            $script:WriteEnvContent = $null
            Mock Read-EnvFileContent {
                param([string]$Path)
                $null = $Path
                # Production-parity return shape: Publish.Env.psm1's real
                # Read-EnvFileContent returns via the unary-comma idiom
                # (return , ([string[]]@($lines))), which yields a single,
                # pipeline-safe string[] rather than letting PowerShell unroll
                # it.
                return , ([string[]]@(
                        '# leading comment',
                        'OPENCLAW_PACKAGE_VERSION=1.0.2.0',
                        'OPENCLAW_CERT_THUMBPRINT=OLDVALUE'
                    ))
            }
            Mock Write-EnvFileContent {
                param([string]$Path, [string[]]$Content)
                $null = $Path
                $script:WriteEnvContent = @($Content)
            }
        }

        It 'preserves a multi-line .env verbatim and updates only OPENCLAW_CERT_THUMBPRINT in place (regression: redundant @() wrap)' {
            # Regression fixture: a comment line plus two KEY=value lines. A
            # redundant @(Read-EnvFileContent ...) wrap at the call site nests
            # the mocked return in a one-element array; binding that to a
            # [string[]] parameter joins every element with $OFS (space),
            # collapsing all lines into one and appending the target key as a
            # duplicate second line. This test fails under that regression and
            # passes once the call site assigns the seam's return value
            # directly, using the real Set-EnvFileValue to perform the update.
            $thumb = 'AABBCCDDEEFF00112233445566778899AABBCCDD'
            Save-CertThumbprintToEnv -Thumbprint $thumb -EnvPath 'C:\fake\.env'

            Assert-MockCalled Write-EnvFileContent -Times 1 -Scope It
            $written = @($script:WriteEnvContent)

            # Not collapsed into a single space-joined line: line count matches
            # the original fixture's line count.
            $written.Count | Should -Be 3

            # Every original line is preserved verbatim except the updated key.
            ($written -contains '# leading comment') | Should -BeTrue
            ($written -contains 'OPENCLAW_PACKAGE_VERSION=1.0.2.0') | Should -BeTrue

            # The target key is updated in place with no duplicate.
            $thumbprintLines = @($written | Where-Object { $_ -like 'OPENCLAW_CERT_THUMBPRINT=*' })
            $thumbprintLines.Count | Should -Be 1
            $thumbprintLines[0] | Should -Be "OPENCLAW_CERT_THUMBPRINT=$thumb"
        }
    }
}
