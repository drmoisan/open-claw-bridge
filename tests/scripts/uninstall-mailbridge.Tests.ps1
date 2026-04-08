Describe 'uninstall-mailbridge.ps1' {
    BeforeEach {
        $script:ScheduledTaskCalls = [System.Collections.Generic.List[string]]::new()
        $scheduledTaskCalls = $script:ScheduledTaskCalls

        Set-Item -Path Function:\Global:schtasks -Value ({
                [CmdletBinding()]
                [OutputType([void])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $null = $Arguments.Count
                $scheduledTaskCalls.Add(($Arguments -join ' '))
            }.GetNewClosure())
    }

    AfterEach {
        Remove-Item Function:\Global:schtasks -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScheduledTaskCalls' -Scope Script -ErrorAction SilentlyContinue
    }

    It 'removes the configured scheduled task name' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\uninstall-mailbridge.ps1'
        $output = & $scriptPath -TaskName 'Configured Task' -Confirm:$false

        @($script:ScheduledTaskCalls) | Should -Contain '/end /tn Configured Task'
        @($script:ScheduledTaskCalls) | Should -Contain '/delete /tn Configured Task /f'
        @($output) | Should -Contain 'OpenClaw MailBridge scheduled task removed. Cache, logs, and settings were intentionally left in place.'
    }
}

