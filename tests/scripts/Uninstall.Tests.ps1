#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Uninstall.ps1.

.DESCRIPTION
    Exercises the uninstall orchestrator by invoking it via '& $ScriptPath'
    with the helpers mocked and a synthetic install record returned from
    Read-InstallRecord. Asserts stage ordering, skipDocker branching,
    partial-state tolerance, failure collection, and preservation of the
    MailBridge user-config sibling directory.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share a call log across scopes.'
)]
param()

Describe 'scripts/Uninstall.ps1' {

    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Uninstall.ps1'
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        Import-Module $script:HelpersPath -Force
        $global:UninstallTestCalls = [System.Collections.ArrayList]::new()
        $global:UninstallRemoveItemPaths = [System.Collections.ArrayList]::new()
        $global:OriginalLOCALAPPDATA = $env:LOCALAPPDATA
        $env:LOCALAPPDATA = 'C:\TestAppData\Local'
    }

    AfterAll {
        if ($null -ne $global:OriginalLOCALAPPDATA) {
            $env:LOCALAPPDATA = $global:OriginalLOCALAPPDATA
        }
        Remove-Variable -Name UninstallTestCalls -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name UninstallRemoveItemPaths -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name OriginalLOCALAPPDATA -Scope Global -ErrorAction SilentlyContinue
    }

    BeforeEach {
        $global:UninstallTestCalls.Clear()
        $global:UninstallRemoveItemPaths.Clear()

        Mock Read-InstallRecord {
            [void]$global:UninstallTestCalls.Add('Read-InstallRecord')
            [pscustomobject]@{
                version            = '1.2.3.0'
                destinationPath    = 'C:\TestAppData\Local\OpenClaw\1.2.3.0'
                packageFullName    = 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
                composeProjectName = 'openclaw'
                composeFilePath    = 'C:\TestAppData\Local\OpenClaw\1.2.3.0\docker\docker-compose.yml'
                skipDocker         = $false
                allowUnsigned      = $false
            }
        }
        Mock Invoke-ComposeDown { [void]$global:UninstallTestCalls.Add('Invoke-ComposeDown') }
        Mock Invoke-MsixRemove { [void]$global:UninstallTestCalls.Add('Invoke-MsixRemove') }
        Mock Test-Path { $true }
        Mock Remove-Item {
            param($LiteralPath)
            [void]$global:UninstallTestCalls.Add('Remove-Item')
            [void]$global:UninstallRemoveItemPaths.Add([string]$LiteralPath)
        }
    }

    Context 'missing install record' {
        It 'throws with "no prior install" when the record is absent and skips all other helpers' {
            Mock Read-InstallRecord { throw "No prior install recorded. Expected install record at 'X'." }
            { & $script:ScriptPath } | Should -Throw -ExpectedMessage '*No prior install*'
            $global:UninstallTestCalls -contains 'Invoke-ComposeDown' | Should -BeFalse
            $global:UninstallTestCalls -contains 'Invoke-MsixRemove' | Should -BeFalse
        }
    }

    Context 'stage ordering (happy path)' {
        It 'invokes helpers in order and exits 0 with no thrown error' {
            { & $script:ScriptPath } | Should -Not -Throw
            $nonTest = $global:UninstallTestCalls | Where-Object { $_ -ne 'Remove-Item' }
            $expected = @('Read-InstallRecord', 'Invoke-ComposeDown', 'Invoke-MsixRemove')
            ($nonTest -join ',') | Should -Be ($expected -join ',')
        }
    }

    Context 'skipDocker = true' {
        It 'does NOT invoke Invoke-ComposeDown; other steps still run' {
            Mock Read-InstallRecord {
                [void]$global:UninstallTestCalls.Add('Read-InstallRecord')
                [pscustomobject]@{
                    destinationPath    = 'C:\TestAppData\Local\OpenClaw\1.2.3.0'
                    packageFullName    = 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
                    composeProjectName = 'openclaw'
                    composeFilePath    = 'C:\TestAppData\Local\OpenClaw\1.2.3.0\docker\docker-compose.yml'
                    skipDocker         = $true
                    allowUnsigned      = $false
                }
            }
            & $script:ScriptPath | Out-Null
            $global:UninstallTestCalls -contains 'Invoke-ComposeDown' | Should -BeFalse
            $global:UninstallTestCalls -contains 'Invoke-MsixRemove' | Should -BeTrue
            $global:UninstallRemoveItemPaths.Count | Should -BeGreaterThan 0
        }
    }

    Context 'partial state tolerance' {
        It 'Invoke-MsixRemove returning silently does not register a failure' {
            { & $script:ScriptPath } | Should -Not -Throw
        }

        It 'Remove-Item for a missing destination is treated as success' {
            Mock Test-Path {
                param($LiteralPath)
                # destination missing; record file present
                if ($LiteralPath -like '*1.2.3.0') { return $false }
                $true
            }
            { & $script:ScriptPath } | Should -Not -Throw
        }

        It 'all four steps run when one step fails mid-way' {
            Mock Invoke-MsixRemove { [void]$global:UninstallTestCalls.Add('Invoke-MsixRemove'); throw 'MSIX remove failed' }
            { & $script:ScriptPath } | Should -Throw
            $global:UninstallTestCalls -contains 'Invoke-ComposeDown' | Should -BeTrue
            $global:UninstallTestCalls -contains 'Invoke-MsixRemove' | Should -BeTrue
            $global:UninstallRemoveItemPaths.Count | Should -BeGreaterThan 0
        }
    }

    Context 'failure collection' {
        It 'runs all four steps when two of them throw' {
            Mock Invoke-ComposeDown { [void]$global:UninstallTestCalls.Add('Invoke-ComposeDown'); throw 'compose down failed' }
            Mock Invoke-MsixRemove { [void]$global:UninstallTestCalls.Add('Invoke-MsixRemove'); throw 'msix remove failed' }
            $thrown = { & $script:ScriptPath } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'compose-down'
            $thrown.Exception.Message | Should -Match 'msix-remove'
            # Remove-Item for both destination and record still ran.
            $global:UninstallRemoveItemPaths.Count | Should -BeGreaterOrEqual 2
        }

        It 'runs the install-record-remove step even when destination-remove fails' {
            Mock Remove-Item {
                param($LiteralPath)
                [void]$global:UninstallTestCalls.Add('Remove-Item')
                [void]$global:UninstallRemoveItemPaths.Add([string]$LiteralPath)
                if ($LiteralPath -like '*1.2.3.0*' -and $LiteralPath -notlike '*install-record.json') {
                    throw 'destination remove failed'
                }
            }
            $thrown = { & $script:ScriptPath } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'remove-destination'
            # Both destination and install-record were attempted.
            ($global:UninstallRemoveItemPaths | Where-Object { $_ -like '*install-record.json' }).Count | Should -BeGreaterThan 0
        }
    }

    Context 'preserves user config' {
        It 'Remove-Item is never invoked against %LOCALAPPDATA%\OpenClaw\MailBridge\' {
            & $script:ScriptPath | Out-Null
            foreach ($p in $global:UninstallRemoveItemPaths) {
                $p | Should -Not -Match 'OpenClaw[\\/]MailBridge'
            }
        }
    }
}
