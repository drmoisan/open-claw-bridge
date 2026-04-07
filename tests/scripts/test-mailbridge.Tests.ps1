Describe 'test-mailbridge.ps1' {
    BeforeEach {
        $script:ScheduledTaskCalls = [System.Collections.Generic.List[string]]::new()
        $script:ClientRequests = [System.Collections.Generic.List[string]]::new()
        $script:WrittenEvidence = [System.Collections.Generic.List[string]]::new()
        $script:WrittenEvidencePath = $null

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

        function query {
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
                [Parameter(Mandatory = $true)]
                [string]$Path,
                [string]$Encoding,
                [Parameter(ValueFromPipeline = $true)]
                [object]$InputObject
            )

            begin {
                $script:WrittenEvidencePath = $Path
                $null = $Encoding
            }

            process {
                $script:WrittenEvidence.Add([string]$InputObject)
            }
        }
    }

    AfterEach {
        Remove-Item Function:\schtasks -ErrorAction SilentlyContinue
        Remove-Item Function:\query -ErrorAction SilentlyContinue
        Remove-Item Function:\Invoke-FakeClient -ErrorAction SilentlyContinue
        Remove-Item Function:\Invoke-LeakyClient -ErrorAction SilentlyContinue
        Remove-Item Function:\Invoke-NotReadyClient -ErrorAction SilentlyContinue
    }

    It 'fails when safe mode leaks protected message fields' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        function Invoke-LeakyClient {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $commandName = [string]$Arguments[0]
            $script:ClientRequests.Add($commandName)

            switch ($commandName) {
                'status' { '{"ok":true,"result":{"state":"ready","mode":"safe"}}' }
                'list-messages' { '{"ok":true,"result":{"items":[{"bridgeId":"msg-1","body_preview":"secret"}]}}' }
                default { throw "Unexpected client command: $commandName" }
            }
        }

        {
            & $testScriptPath -ClientPath 'Invoke-LeakyClient' -TaskName 'OpenClaw MailBridge' -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'
        } | Should -Throw 'Safe mode leaked body_preview.'

        @($script:ClientRequests) | Should -Contain 'list-messages'
    }

    It 'fails when readiness does not arrive before the deadline' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        function Invoke-NotReadyClient {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $commandName = [string]$Arguments[0]
            $script:ClientRequests.Add($commandName)

            if ($commandName -eq 'status') {
                return '{"ok":true,"result":{"state":"waiting_for_outlook","mode":"safe"}}'
            }

            throw "Unexpected client command: $commandName"
        }

        {
            & $testScriptPath -ClientPath 'Invoke-NotReadyClient' -TaskName 'OpenClaw MailBridge' -ReadyTimeoutSeconds 0 -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'
        } | Should -Throw 'Bridge readiness deadline expired before status.result.state reached ready.'

        @($script:ScheduledTaskCalls) | Should -Contain '/run /tn OpenClaw MailBridge'
    }

    It 'emits suite E operator evidence keys' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        function Invoke-FakeClient {
            [CmdletBinding()]
            [OutputType([string])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $commandName = [string]$Arguments[0]
            $script:ClientRequests.Add(($Arguments -join ' '))

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
        $script:WrittenEvidencePath | Should -Be 'virtual:\operator-evidence.txt'
        @($script:WrittenEvidence) | Should -Contain 'PrimaryInteractiveSession: True'
        @($script:WrittenEvidence) | Should -Contain 'OpenClawSvcPipeConnect: True'
        @($script:WrittenEvidence) | Should -Contain 'NetworkDenyVerified: True'
        @($script:ClientRequests | Where-Object { $_ -like 'get-message*' }).Count | Should -Be 1
        @($script:ClientRequests | Where-Object { $_ -like 'get-event*' }).Count | Should -Be 1
    }
}
