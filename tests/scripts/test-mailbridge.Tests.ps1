Describe 'test-mailbridge.ps1' {
    BeforeEach {
        $global:ScheduledTaskCalls = [System.Collections.Generic.List[string]]::new()
        $global:ClientRequests = [System.Collections.Generic.List[string]]::new()
        $global:WrittenEvidence = [System.Collections.Generic.List[string]]::new()
        $global:WrittenEvidencePath = $null

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

        function global:query {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            "{0} console" -f $env:USERNAME
        }

        Mock New-Item { [pscustomobject]@{ FullName = 'virtual:\operator-evidence.txt' } }
        Mock Set-Content {
            param(
                [string]$Path,
                [object[]]$Value
            )

            $global:WrittenEvidencePath = $Path
            foreach ($line in @($Value)) {
                $global:WrittenEvidence.Add([string]$line)
            }
        }
    }

    AfterEach {
        Remove-Item Function:\Global:schtasks -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:query -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-FakeClient -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-LeakyClient -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-NotReadyClient -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScheduledTaskCalls' -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ClientRequests' -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name 'WrittenEvidence' -Scope Global -ErrorAction SilentlyContinue
        Remove-Variable -Name 'WrittenEvidencePath' -Scope Global -ErrorAction SilentlyContinue
    }

    It 'fails when safe mode leaks protected message fields' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        function global:Invoke-LeakyClient {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $commandName = [string]$Arguments[0]
            $global:ClientRequests.Add($commandName)

            switch ($commandName) {
                'status' { '{"ok":true,"result":{"state":"ready","mode":"safe"}}' }
                'list-messages' { '{"ok":true,"result":{"items":[{"bridgeId":"msg-1","body_preview":"secret"}]}}' }
                'get-message' { '{"ok":true,"result":{"bridgeId":"msg-1","body_preview":"secret"}}' }
                'list-meeting-requests' { '{"ok":true,"result":{"items":[]}}' }
                'list-calendar' { '{"ok":true,"result":{"items":[]}}' }
                default { throw "Unexpected client command: $commandName" }
            }
        }

        {
            & $testScriptPath -ClientPath 'Invoke-LeakyClient' -TaskName 'OpenClaw MailBridge' -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'
        } | Should -Throw 'Safe mode leaked body_preview.'

        @($global:ClientRequests) | Should -Contain 'list-messages'
    }

    It 'fails when readiness does not arrive before the deadline' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        function global:Invoke-NotReadyClient {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $commandName = [string]$Arguments[0]
            $global:ClientRequests.Add($commandName)

            if ($commandName -eq 'status') {
                return '{"ok":true,"result":{"state":"waiting_for_outlook","mode":"safe"}}'
            }

            throw "Unexpected client command: $commandName"
        }

        {
            & $testScriptPath -ClientPath 'Invoke-NotReadyClient' -TaskName 'OpenClaw MailBridge' -ReadyTimeoutSeconds 0 -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'
        } | Should -Throw 'Bridge readiness deadline expired before status.result.state reached ready.'

        @($global:ScheduledTaskCalls) | Should -Contain '/run /tn OpenClaw MailBridge'
    }

    It 'emits suite E operator evidence keys' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        function global:Invoke-FakeClient {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $commandName = [string]$Arguments[0]
            $global:ClientRequests.Add(($Arguments -join ' '))

            switch ($commandName) {
                'status' { '{"ok":true,"result":{"state":"ready","mode":"safe"}}' }
                'list-messages' { '{"ok":true,"result":{"items":[{"bridgeId":"msg-1"}]}}' }
                'get-message' { '{"ok":true,"result":{"bridgeId":"msg-1"}}' }
                'list-meeting-requests' { '{"ok":true,"result":{"items":[]}}' }
                'list-calendar' { '{"ok":true,"result":{"items":[{"bridgeId":"evt-1"}]}}' }
                'get-event' { '{"ok":true,"result":{"bridgeId":"evt-1"}}' }
                default { throw "Unexpected client command: $commandName" }
            }
        }

        $output = & $testScriptPath `
            -ClientPath 'Invoke-FakeClient' `
            -TaskName 'OpenClaw MailBridge' `
            -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt' `
            -ExpectMessageData `
            -ExpectCalendarData `
            -OpenClawSvcPipeConnect $true `
            -NetworkDenyVerified $true

        @($output) | Should -Contain 'AutomatedSuitesPassed: A,B,C,D,F'
        $global:WrittenEvidencePath | Should -Be 'virtual:\operator-evidence.txt'
        @($global:WrittenEvidence) | Should -Contain 'PrimaryInteractiveSession: True'
        @($global:WrittenEvidence) | Should -Contain 'OpenClawSvcPipeConnect: True'
        @($global:WrittenEvidence) | Should -Contain 'NetworkDenyVerified: True'
        @($global:ClientRequests | Where-Object { $_ -like 'get-message*' }).Count | Should -Be 1
        @($global:ClientRequests | Where-Object { $_ -like 'get-event*' }).Count | Should -Be 1
    }
}
