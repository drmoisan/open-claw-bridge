Describe 'register-mailbridge-task.ps1' {
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
        Remove-Item Function:\Global:schtasks -ErrorAction SilentlyContinue
        Remove-Item Function:\query -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScheduledTaskCalls' -Scope Global -ErrorAction SilentlyContinue
    }

    It 'preserves /sc onlogon and /it registration semantics' {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\register-mailbridge-task.ps1'
        $env:LOCALAPPDATA = 'C:\Users\PrimaryUser\AppData\Local'

        & $scriptPath -PrimaryUser 'PrimaryUser' -InstallRoot 'C:\Bridge' -Confirm:$false

        $createCall = @($global:ScheduledTaskCalls | Where-Object { $_ -like '/create*' })[0]
        $runCall = @($global:ScheduledTaskCalls | Where-Object { $_ -like '/run*' })[0]

        $createCall | Should -Match '/sc onlogon'
        $createCall | Should -Match '/it'
        $createCall | Should -Match '/ru PrimaryUser'
        $createCall | Should -Match ([regex]::Escape('"C:\Bridge\OpenClaw.MailBridge.exe" --config "C:\Users\PrimaryUser\AppData\Local\OpenClaw\MailBridge\bridge.settings.json"'))
        $runCall | Should -Match '/run /tn OpenClaw MailBridge'
    }
}
