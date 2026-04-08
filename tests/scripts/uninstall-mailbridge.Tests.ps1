Describe 'uninstall-mailbridge.ps1' {
    BeforeEach {
        $global:ScheduledTaskCalls = [System.Collections.Generic.List[string]]::new()

        function global:schtasks {
            [CmdletBinding()]
            [OutputType([void])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            $global:ScheduledTaskCalls.Add(($Arguments -join ' '))
        }
    }

    AfterEach {
        Remove-Item Function:\Global:schtasks -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScheduledTaskCalls' -Scope Global -ErrorAction SilentlyContinue
    }

    It 'removes the configured scheduled task name' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\uninstall-mailbridge.ps1'
        $output = & $scriptPath -TaskName 'Configured Task' -Confirm:$false

        @($global:ScheduledTaskCalls) | Should -Contain '/end /tn Configured Task'
        @($global:ScheduledTaskCalls) | Should -Contain '/delete /tn Configured Task /f'
        @($output) | Should -Contain 'OpenClaw MailBridge scheduled task removed. Cache, logs, and settings were intentionally left in place.'
    }
}
