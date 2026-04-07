Describe 'uninstall-mailbridge.ps1' {
    BeforeEach {
        $script:ScheduledTaskCalls = [System.Collections.Generic.List[string]]::new()

        function schtasks {
            [CmdletBinding()]
            [OutputType([void])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            $script:ScheduledTaskCalls.Add(($Arguments -join ' '))
        }
    }

    AfterEach {
        Remove-Item Function:\schtasks -ErrorAction SilentlyContinue
    }

    It 'removes the configured scheduled task name' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\uninstall-mailbridge.ps1'
        $output = & $scriptPath -TaskName 'Configured Task' -Confirm:$false

        @($script:ScheduledTaskCalls) | Should -Contain '/end /tn Configured Task'
        @($script:ScheduledTaskCalls) | Should -Contain '/delete /tn Configured Task /f'
        @($output) | Should -Contain 'OpenClaw MailBridge scheduled task removed. Cache, logs, and settings were intentionally left in place.'
    }
}
