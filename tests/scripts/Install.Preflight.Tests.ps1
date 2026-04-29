#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for the new preflight helpers and Stage 8.5 rollback.

.DESCRIPTION
    Split out from Install.Tests.ps1 to keep both files under the 500-line
    policy in .claude/rules/general-code-change.md. Covers AC-01..AC-06 and
    the Stage 8.5 rollback contract for AC-06.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Mock script blocks run in the orchestrator script scope; $global: is required to share counters across scopes.'
)]
param()

Describe 'Invoke-HostAdapterStart identity check' {
    BeforeAll {
        $script:ScriptPath = Join-Path $PSScriptRoot '..\..\scripts\Install.ps1'
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        $script:PreflightPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1'
        Import-Module $script:HelpersPath -Force
        Import-Module $script:PreflightPath -Force
        # Pre-register globals that Install.ps1's `if (-not (Get-Command ...))` shims
        # check at dot-source time, so the script does NOT define its own production
        # implementations. Keeping these defined makes the dot-sourced
        # Invoke-HostAdapterStart resolve to our test stubs.
        function global:Test-TcpPortOpen {
            param($IpAddress, $Port)
            $null = $IpAddress; $null = $Port
            $true
        }
        function global:Invoke-HostAdapterProcess {
            param($ProcessStartInfo)
            $null = $ProcessStartInfo
            $global:installTestInvokeProcessCount++
        }
        # Dot-source the script in test scope so Invoke-HostAdapterStart is defined
        # without running the orchestrator main block.
        . $script:ScriptPath -SourcePath 'C:\dot-source-noop' -ErrorAction SilentlyContinue 2>&1 | Out-Null
    }

    AfterAll {
        Remove-Item -Path 'Function:\Test-TcpPortOpen' -Force -ErrorAction SilentlyContinue
        Remove-Item -Path 'Function:\Invoke-HostAdapterProcess' -Force -ErrorAction SilentlyContinue
    }

    BeforeEach {
        Mock Test-Path { $true }
        $global:installTestInvokeProcessCount = 0
    }

    Context 'matching path' {
        It 'returns without invoking Invoke-HostAdapterProcess when bound listener path matches the bundle path (case-insensitive)' {
            $bundleExe = 'C:\Bundle\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
            Mock Get-ListeningProcessId { 12345 }
            Mock Get-ProcessMainModulePath {
                param($ProcessId)
                $null = $ProcessId
                'c:\bundle\EXECUTABLES\OpenClaw.HostAdapter\OPENCLAW.HOSTADAPTER.EXE'
            }

            $messages = & {
                Invoke-HostAdapterStart -HostAdapterExePath $bundleExe -AspNetCoreUrls 'http://127.0.0.1:4319/'
            } 6>&1
            $global:installTestInvokeProcessCount | Should -Be 0
            (@($messages | Where-Object { ([string]$_) -like '*HostAdapter already running on port*' }).Count) | Should -BeGreaterThan 0
        }
    }

    Context 'stale process throws' {
        It 'throws with the stale PID and observed path when the listener path does not match the bundle path' {
            $bundleExe = 'C:\Bundle\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
            Mock Get-ListeningProcessId { 32948 }
            Mock Get-ProcessMainModulePath {
                param($ProcessId)
                $null = $ProcessId
                'C:\src\OpenClaw.HostAdapter\bin\Release\net10.0\OpenClaw.HostAdapter.exe'
            }

            { Invoke-HostAdapterStart -HostAdapterExePath $bundleExe -AspNetCoreUrls 'http://127.0.0.1:4319/' } |
                Should -Throw -ExpectedMessage '*PID 32948*Release\net10.0*Bundle\executables*'
            $global:installTestInvokeProcessCount | Should -Be 0
        }
    }

    Context '-Force stops stale process and proceeds with bundle HostAdapter launch' {
        It 'calls Stop-Process with the stale PID and then calls Invoke-HostAdapterProcess exactly once' {
            # Arrange: port is bound by a process whose path does not match the bundle exe.
            $bundleExe = 'C:\Bundle\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
            Mock Get-ListeningProcessId { 32948 }
            Mock Get-ProcessMainModulePath {
                param($ProcessId)
                $null = $ProcessId
                'C:\src\OpenClaw.HostAdapter\bin\Release\net10.0\OpenClaw.HostAdapter.exe'
            }
            # Stop-Process is a built-in cmdlet; mock without a typed param block to avoid
            # Int32[] coercion failure (Pester passes -Id as the cmdlet's actual [int[]] type).
            Mock Stop-Process { }
            Mock Invoke-HostAdapterProcess { }

            # Act: -Force must not throw.
            { Invoke-HostAdapterStart -HostAdapterExePath $bundleExe -AspNetCoreUrls 'http://127.0.0.1:4319/' -Force } |
                Should -Not -Throw

            # Assert: Stop-Process was called exactly once with the stale PID (32948).
            Should -Invoke Stop-Process -Times 1 -Exactly -ParameterFilter { $Id -eq 32948 }
            # Assert: the bundle HostAdapter was subsequently launched via Invoke-HostAdapterProcess
            # with the bundle exe path.
            Should -Invoke Invoke-HostAdapterProcess -Times 1 -Exactly -ParameterFilter {
                $ProcessStartInfo.FileName -eq 'C:\Bundle\executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
            }
        }
    }
}

Describe 'Format-HostAdapterPreflightFailure' {
    BeforeAll {
        $script:PreflightPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1'
        Import-Module $script:PreflightPath -Force
    }

    It 'includes error.code and error.message when the body parses with a non-null error block' {
        $body = '{"ok":false,"data":null,"meta":{"requestId":"r1","adapterVersion":"1.0.0.0","bridge":null},"error":{"code":"TRANSPORT_FAILURE","message":"The operation has timed out.","retryable":true}}'
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body $body
        $msg | Should -Match 'TRANSPORT_FAILURE'
        $msg | Should -Match 'The operation has timed out\.'
    }

    It 'falls back to HTTP-status-only message when the body is not JSON' {
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body '<html>500</html>'
        $msg | Should -Match 'HTTP 502'
        $msg | Should -Not -Match 'error\.code='
    }

    It 'falls back to HTTP-status-only message when the body has no error block' {
        $body = '{"ok":true,"data":null,"meta":{"adapterVersion":"1.0.0.0"},"error":null}'
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body $body
        $msg | Should -Match 'HTTP 502'
        $msg | Should -Not -Match 'error\.code='
    }
}

Describe 'Assert-HostAdapterRespondingPreflight and Assert-HostAdapterBridgeReadyPreflight' {
    BeforeAll {
        $script:PreflightPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1'
        Import-Module $script:PreflightPath -Force
    }

    BeforeEach {
        Mock Test-Path { $true } -ModuleName Install.Preflight
        Mock Get-Content { 'tok' } -ModuleName Install.Preflight
    }

    Context 'Assert-HostAdapterRespondingPreflight' {
        It 'accepts a 502 response when meta.adapterVersion is present (responsive but bridge down)' {
            Mock Invoke-HostAdapterStatusRequest {
                [pscustomobject]@{ StatusCode = 502; Headers = @{}; Content = '{"ok":false,"data":null,"meta":{"requestId":"r1","adapterVersion":"1.0.0.0","bridge":null},"error":{"code":"TRANSPORT_FAILURE","message":"timeout","retryable":true}}' }
            } -ModuleName Install.Preflight
            { Assert-HostAdapterRespondingPreflight -DestDockerDir 'C:\fake\docker' } | Should -Not -Throw
        }

        It 'throws with HTTP <code> when the body is not JSON' {
            Mock Invoke-HostAdapterStatusRequest {
                [pscustomobject]@{ StatusCode = 502; Headers = @{}; Content = '<html/>' }
            } -ModuleName Install.Preflight
            { Assert-HostAdapterRespondingPreflight -DestDockerDir 'C:\fake\docker' } |
                Should -Throw -ExpectedMessage '*HTTP 502*'
        }
    }

    Context 'Assert-HostAdapterBridgeReadyPreflight' {
        It 'returns without throwing when status is 200 and data.state is a non-not-ready value' {
            Mock Invoke-HostAdapterStatusRequest {
                [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{"ok":true,"data":{"state":"running","mode":"x"},"meta":{"adapterVersion":"1.0.0.0"},"error":null}' }
            } -ModuleName Install.Preflight
            { Assert-HostAdapterBridgeReadyPreflight -DestDockerDir 'C:\fake\docker' } | Should -Not -Throw
        }

        It 'throws when data.state is waiting_for_outlook' {
            Mock Invoke-HostAdapterStatusRequest {
                [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{"ok":true,"data":{"state":"waiting_for_outlook"},"meta":{"adapterVersion":"1.0.0.0"},"error":null}' }
            } -ModuleName Install.Preflight
            { Assert-HostAdapterBridgeReadyPreflight -DestDockerDir 'C:\fake\docker' } | Should -Throw
        }

        It 'throws when data.state is starting' {
            Mock Invoke-HostAdapterStatusRequest {
                [pscustomobject]@{ StatusCode = 200; Headers = @{}; Content = '{"ok":true,"data":{"state":"starting"},"meta":{"adapterVersion":"1.0.0.0"},"error":null}' }
            } -ModuleName Install.Preflight
            { Assert-HostAdapterBridgeReadyPreflight -DestDockerDir 'C:\fake\docker' } | Should -Throw
        }
    }
}

Describe 'Stage 8.5 rollback (Invoke-Stage8Point5BridgeReadyOrRollback)' {
    BeforeAll {
        $script:HelpersPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        $script:PreflightPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1'
        Import-Module $script:HelpersPath -Force
        Import-Module $script:PreflightPath -Force
    }

    It 'invokes Invoke-MsixRemove with the captured PackageFullName before the throw propagates' {
        Mock Assert-HostAdapterBridgeReadyPreflight { throw 'TRANSPORT_FAILURE timeout' } -ModuleName Install.Preflight
        Mock Invoke-MsixRemove { } -ModuleName Install.Preflight
        $packageFullName = 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
        { Invoke-Stage8Point5BridgeReadyOrRollback -DestDockerDir 'C:\fake\docker' -PackageFullName $packageFullName } |
            Should -Throw -ExpectedMessage '*TRANSPORT_FAILURE*'
        Should -Invoke Invoke-MsixRemove -Times 1 -ModuleName Install.Preflight -ParameterFilter { $PackageFullName -eq 'OpenClaw.MailBridge_1.2.3.0_x64__abc' }
    }

    It 'tolerates Invoke-MsixRemove failure and still throws the original bridge error' {
        Mock Assert-HostAdapterBridgeReadyPreflight { throw 'TRANSPORT_FAILURE timeout' } -ModuleName Install.Preflight
        Mock Invoke-MsixRemove { throw 'msix rollback boom' } -ModuleName Install.Preflight
        { Invoke-Stage8Point5BridgeReadyOrRollback -DestDockerDir 'C:\fake\docker' -PackageFullName 'pfn' } |
            Should -Throw -ExpectedMessage '*TRANSPORT_FAILURE*'
    }
}

Describe 'Assert-HostAdapterBridgeReadyPreflight JSON-parse-failure' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1') -Force
    }
    BeforeEach {
        Mock Get-PreflightTokenAndUri -ModuleName Install.Preflight -MockWith {
            @{ Token = 'tkn'; StatusUri = [uri]'http://127.0.0.1:4319/v1/status' }
        } -ParameterFilter { $DestDockerDir -eq 'TestDrive:\stage85' }
    }
    It 'throws via Format-HostAdapterPreflightFailure when the response body is not JSON' {
        Mock Invoke-HostAdapterStatusRequest -ModuleName Install.Preflight -MockWith {
            [pscustomobject]@{ StatusCode = 200; Content = '<not-json>' }
        } -ParameterFilter { $StatusUri -eq [uri]'http://127.0.0.1:4319/v1/status' -and $Token -eq 'tkn' }
        { Assert-HostAdapterBridgeReadyPreflight -DestDockerDir 'TestDrive:\stage85' } |
            Should -Throw -ExpectedMessage '*HostAdapter preflight failed before starting Docker. GET http://127.0.0.1:4319/v1/status returned HTTP 200.*'
    }
}

Describe 'Format-HostAdapterPreflightFailure boundary cases' {
    BeforeAll {
        Import-Module (Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1') -Force
    }
    It 'returns the status-only fallback when Body is empty' {
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body ''
        $msg | Should -Match 'HostAdapter preflight failed before starting Docker\. GET http://127\.0\.0\.1:4319/v1/status returned HTTP 502\.'
        $msg | Should -Not -Match 'error\.code='
    }
    It 'returns the status-only fallback when error block has null code and null message' {
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body '{"error":{"code":null,"message":null}}'
        $msg | Should -Not -Match 'error\.code='
    }
    It 'surfaces error.code with (missing) message when only code is set' {
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body '{"error":{"code":"TRANSPORT_FAILURE"}}'
        $msg | Should -Match 'error\.code=TRANSPORT_FAILURE; error\.message=\(missing\)'
    }
    It 'surfaces error.message with (missing) code when only message is set' {
        $msg = Format-HostAdapterPreflightFailure -StatusUri ([uri]'http://127.0.0.1:4319/v1/status') -StatusCode 502 -Body '{"error":{"message":"timed out"}}'
        $msg | Should -Match 'error\.code=\(missing\); error\.message=timed out'
    }
}
