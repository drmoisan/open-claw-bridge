Describe 'register-mailbridge-task.ps1' {
    BeforeEach {
        $registerTaskShimState = [pscustomobject]@{
            ScheduledTaskCalls        = [System.Collections.Generic.List[string]]::new()
            SimulatedSchTasksExitCode = 0
        }
        $script:RegisterTaskShimState = $registerTaskShimState

        function schtasks {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            $registerTaskShimState.ScheduledTaskCalls.Add(($Arguments -join ' '))

            if (($Arguments -join ' ') -like '/create*' -and $registerTaskShimState.SimulatedSchTasksExitCode -ne 0) {
                $global:LASTEXITCODE = $registerTaskShimState.SimulatedSchTasksExitCode
                'ERROR: Access is denied.'
                return
            }

            $global:LASTEXITCODE = 0
        }

        function query {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            'PrimaryUser console'
        }

        Mock Test-Path { $true }
    }

    AfterEach {
        Remove-Item Function:\schtasks -ErrorAction SilentlyContinue
        Remove-Item Function:\query -ErrorAction SilentlyContinue
        Remove-Variable -Name 'RegisterTaskShimState' -Scope Script -ErrorAction SilentlyContinue
    }

    It 'preserves /sc onlogon and /it registration semantics' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\register-mailbridge-task.ps1'
        $env:LOCALAPPDATA = 'C:\Users\PrimaryUser\AppData\Local'

        & $scriptPath -PrimaryUser 'PrimaryUser' -InstallRoot 'C:\Bridge' -Confirm:$false

        $createCall = @($script:RegisterTaskShimState.ScheduledTaskCalls | Where-Object { $_ -like '/create*' })[0]
        $runCall = @($script:RegisterTaskShimState.ScheduledTaskCalls | Where-Object { $_ -like '/run*' })[0]

        $createCall | Should -Match '/sc onlogon'
        $createCall | Should -Match '/it'
        $createCall | Should -Match '/ru PrimaryUser'
        $createCall | Should -Match ([regex]::Escape('"C:\Bridge\OpenClaw.MailBridge.exe" --config "C:\Users\PrimaryUser\AppData\Local\OpenClaw\MailBridge\bridge.settings.json"'))
        $runCall | Should -Match '/run /tn OpenClaw MailBridge'
    }

    It 'runs the task when PrimaryUser is machine-qualified and the local session is active' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\register-mailbridge-task.ps1'
        $env:LOCALAPPDATA = 'C:\Users\PrimaryUser\AppData\Local'

        function query {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            ' PrimaryUser           console             1  Active'
        }

        & $scriptPath -PrimaryUser 'WORKSTATION\PrimaryUser' -InstallRoot 'C:\Bridge' -Confirm:$false

        $runCall = @($script:RegisterTaskShimState.ScheduledTaskCalls | Where-Object { $_ -like '/run*' })[0]
        $runCall | Should -Match '/run /tn OpenClaw MailBridge'
    }

    It 'fails explicitly when schtasks create returns a non-zero exit code' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\register-mailbridge-task.ps1'
        $env:LOCALAPPDATA = 'C:\Users\PrimaryUser\AppData\Local'
        $script:RegisterTaskShimState.SimulatedSchTasksExitCode = 5

        {
            & $scriptPath -PrimaryUser 'PrimaryUser' -InstallRoot 'C:\Bridge' -Confirm:$false
        } | Should -Throw '*Access is denied*'
    }
}

