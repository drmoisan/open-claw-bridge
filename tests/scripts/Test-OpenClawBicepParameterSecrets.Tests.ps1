#Requires -Version 7
<#
.SYNOPSIS
    Pester unit tests for scripts/Test-OpenClawBicepParameterSecrets.ps1.

.DESCRIPTION
    Tests the `Test-OpenClawBicepParameterSecrets` function using mocked
    filesystem cmdlets (`Test-Path`, `Get-ChildItem`, `Get-Content`) so that no
    real files are created or read during testing. Covers: a clean parameter
    file, a file containing an inlined secret-shaped value, an empty/missing
    parameters directory, the default `-Path` value, and (via direct,
    non-dot-sourced invocation of the script) the main entry-point's
    `exit 0`/`exit 1` branches.
#>

Describe 'Test-OpenClawBicepParameterSecrets' {

    BeforeAll {
        # Dot-source the script to load the function into scope. The Main block
        # does not run under dot-source ($MyInvocation.InvocationName -eq '.').
        . (Join-Path $PSScriptRoot '../../scripts/Test-OpenClawBicepParameterSecrets.ps1')
    }

    Context 'clean parameter file' {
        It 'reports a passing/clean result when no secret-shaped content is present' {
            # Arrange
            Mock Test-Path { return $true } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-ChildItem {
                [pscustomobject]@{
                    FullName  = 'deploy/azure/parameters/main.dev.bicepparam'
                    Extension = '.bicepparam'
                }
            } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-Content {
                "using 'main.bicep'`nparam environmentName = 'dev'`nparam containerImage = 'REPLACE_AT_DEPLOY_TIME/openclaw-core:unset'"
            } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters/main.dev.bicepparam' }

            # Act
            $result = Test-OpenClawBicepParameterSecrets -Path 'deploy/azure/parameters'

            # Assert
            $result.IsClean | Should -BeTrue
            $result.FileCount | Should -Be 1
            @($result.Findings).Count | Should -Be 0
        }
    }

    Context 'file containing a secret-shaped literal' {
        It 'reports a failing result naming the offending file' {
            # Arrange
            Mock Test-Path { return $true } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-ChildItem {
                [pscustomobject]@{
                    FullName  = 'deploy/azure/parameters/main.prod.bicepparam'
                    Extension = '.bicepparam'
                }
            } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-Content {
                "using 'main.bicep'`nparam serviceBusConnection = 'Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AbCdEfGhIjKlMnOpQrStUvWxYz0123456789=='"
            } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters/main.prod.bicepparam' }

            # Act
            $result = Test-OpenClawBicepParameterSecrets -Path 'deploy/azure/parameters'

            # Assert
            $result.IsClean | Should -BeFalse
            @($result.Findings).Count | Should -BeGreaterThan 0
            ($result.Findings | Select-Object -First 1).FilePath | Should -Be 'deploy/azure/parameters/main.prod.bicepparam'
        }
    }

    Context 'empty or missing parameters directory' {
        It 'handles a missing directory without an unhandled exception and reports a clean result' {
            # Arrange
            Mock Test-Path { return $false } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }

            # Act
            $threw = $false
            try {
                $result = Test-OpenClawBicepParameterSecrets -Path 'deploy/azure/parameters'
            }
            catch {
                $threw = $true
            }

            # Assert
            $threw | Should -BeFalse
            $result.IsClean | Should -BeTrue
            $result.FileCount | Should -Be 0
            @($result.Findings).Count | Should -Be 0
        }

        It 'handles an existing but empty directory without an unhandled exception and reports a clean result' {
            # Arrange
            Mock Test-Path { return $true } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-ChildItem { @() } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }

            # Act
            $threw = $false
            try {
                $result = Test-OpenClawBicepParameterSecrets -Path 'deploy/azure/parameters'
            }
            catch {
                $threw = $true
            }

            # Assert
            $threw | Should -BeFalse
            $result.IsClean | Should -BeTrue
            $result.FileCount | Should -Be 0
        }
    }

    Context 'default parameter value' {
        It 'defaults -Path to deploy/azure/parameters when not supplied' {
            # Arrange
            Mock Test-Path { return $false } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }

            # Act
            $result = Test-OpenClawBicepParameterSecrets

            # Assert
            $result.ScannedPath | Should -Be 'deploy/azure/parameters'
            $result.IsClean | Should -BeTrue
        }
    }

    Context 'main entry point (script invoked directly, not dot-sourced)' {
        # The script's "if ($MyInvocation.InvocationName -ne '.')" main block only
        # runs when the script is invoked directly (e.g. `& $ScriptPath`), not
        # when dot-sourced. In PowerShell 7, a script's top-level `exit` inside
        # a call-operator (`&`) invocation terminates only that nested script
        # invocation, not the calling process, so it is safe to invoke the
        # script directly here (verified empirically before authoring this
        # test); this also keeps execution in the same runspace Pester's
        # coverage collector instruments, unlike a separate runspace/process.
        # No file is created on disk; the filesystem is simulated via Pester
        # mocks, matching this repo's existing pattern in
        # tests/scripts/Uninstall.Tests.ps1.

        BeforeAll {
            $script:ScriptPath = Join-Path $PSScriptRoot '../../scripts/Test-OpenClawBicepParameterSecrets.ps1'
        }

        It 'exits 0 and reports the clean message when the target directory does not exist' {
            # Arrange
            Mock Test-Path { return $false } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }

            # Act
            $output = & $script:ScriptPath
            $exitCode = $LASTEXITCODE

            # Assert
            $exitCode | Should -Be 0
            ($output -join "`n") | Should -Match 'Clean: scanned 0 parameter file'
        }

        It 'exits 1 and writes an error naming the offending file when a secret-shaped literal is found' {
            # Arrange
            Mock Test-Path { return $true } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-ChildItem {
                [pscustomobject]@{
                    FullName  = 'deploy/azure/parameters/main.prod.bicepparam'
                    Extension = '.bicepparam'
                }
            } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters' }
            Mock Get-Content {
                "using 'main.bicep'`nparam serviceBusConnection = 'Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AbCdEfGhIjKlMnOpQrStUvWxYz0123456789=='"
            } -ParameterFilter { $LiteralPath -eq 'deploy/azure/parameters/main.prod.bicepparam' }

            # Act - redirect the error stream into the captured output so the
            # non-terminating Write-Error record can be asserted on below.
            $allOutput = & $script:ScriptPath 2>&1
            $exitCode = $LASTEXITCODE
            $errorRecords = @($allOutput | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] })

            # Assert
            $exitCode | Should -Be 1
            $errorRecords.Count | Should -BeGreaterThan 0
            $errorRecords[0].ToString() | Should -Match 'Secret-shaped literal found'
        }
    }
}
