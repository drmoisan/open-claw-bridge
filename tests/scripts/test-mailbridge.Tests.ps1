Describe 'test-mailbridge.ps1' {
    BeforeEach {
        $script:ScheduledTaskCalls = [System.Collections.Generic.List[string]]::new()
        $script:ClientRequests = [System.Collections.Generic.List[string]]::new()
        $script:WrittenEvidence = [System.Collections.Generic.List[string]]::new()
        $script:WrittenEvidencePath = $null
        $script:WrittenEvidenceState = [pscustomobject]@{ Path = $null }
        $script:BridgeRuntimeConfigPath = 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.runtimeconfig.json'
        $script:ClientRuntimeConfigPath = 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.runtimeconfig.json'
        $scheduledTaskCalls = $script:ScheduledTaskCalls
        $writtenEvidence = $script:WrittenEvidence
        $writtenEvidenceState = $script:WrittenEvidenceState
        $bridgeRuntimeConfigPath = $script:BridgeRuntimeConfigPath
        $clientRuntimeConfigPath = $script:ClientRuntimeConfigPath

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

        Set-Item -Path Function:\Global:query -Value ({
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $null = $Arguments.Count
                "{0} console" -f $env:USERNAME
            }.GetNewClosure())

        Mock New-Item { [pscustomobject]@{ FullName = 'virtual:\operator-evidence.txt' } }
        Mock Test-Path {
            param(
                [string]$Path
            )

            $Path -in @($bridgeRuntimeConfigPath, $clientRuntimeConfigPath)
        }
        Mock Get-Content {
            param(
                [string]$Path,
                [string]$Raw
            )

            $null = $Raw
            if ($Path -eq $bridgeRuntimeConfigPath) {
                return '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}'
            }

            if ($Path -eq $clientRuntimeConfigPath) {
                return '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}'
            }

            throw "Unexpected Get-Content path: $Path"
        }
        Mock Set-Content {
            param(
                [string]$Path,
                [object[]]$Value
            )

            $writtenEvidenceState.Path = $Path
            foreach ($line in @($Value)) {
                $writtenEvidence.Add([string]$line)
            }
        }
    }

    AfterEach {
        Remove-Item Function:\Global:schtasks -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:query -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-FakeClient -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-LeakyClient -ErrorAction SilentlyContinue
        Remove-Item Function:\Global:Invoke-NotReadyClient -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ScheduledTaskCalls' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ClientRequests' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'WrittenEvidence' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'WrittenEvidencePath' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'WrittenEvidenceState' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'BridgeRuntimeConfigPath' -Scope Script -ErrorAction SilentlyContinue
        Remove-Variable -Name 'ClientRuntimeConfigPath' -Scope Script -ErrorAction SilentlyContinue
    }

    It 'fails when safe mode leaks protected message fields' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'
        $clientRequests = $script:ClientRequests

        Set-Item -Path Function:\Global:Invoke-LeakyClient -Value ({
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $commandName = [string]$Arguments[0]
                $clientRequests.Add($commandName)

                switch ($commandName) {
                    'status' { '{"ok":true,"result":{"state":"ready","mode":"safe"}}' }
                    'list-messages' { '{"ok":true,"result":{"items":[{"bridgeId":"msg-1","body_preview":"secret"}]}}' }
                    'get-message' { '{"ok":true,"result":{"bridgeId":"msg-1","body_preview":"secret"}}' }
                    'list-meeting-requests' { '{"ok":true,"result":{"items":[]}}' }
                    'list-calendar' { '{"ok":true,"result":{"items":[]}}' }
                    default { throw "Unexpected client command: $commandName" }
                }
            }.GetNewClosure())

        {
            & $testScriptPath -ClientPath 'Invoke-LeakyClient' -TaskName 'OpenClaw MailBridge' -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'
        } | Should -Throw 'Safe mode leaked body_preview.'

        @($script:ClientRequests) | Should -Contain 'list-messages'
    }

    It 'fails when readiness does not arrive before the deadline' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'
        $clientRequests = $script:ClientRequests

        Set-Item -Path Function:\Global:Invoke-NotReadyClient -Value ({
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $commandName = [string]$Arguments[0]
                $clientRequests.Add($commandName)

                if ($commandName -eq 'status') {
                    return '{"ok":true,"result":{"state":"waiting_for_outlook","mode":"safe"}}'
                }

                throw "Unexpected client command: $commandName"
            }.GetNewClosure())

        {
            & $testScriptPath -ClientPath 'Invoke-NotReadyClient' -TaskName 'OpenClaw MailBridge' -ReadyTimeoutSeconds 0 -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'
        } | Should -Throw 'Bridge readiness deadline expired before status.result.state reached ready.'

        @($script:ScheduledTaskCalls) | Should -Contain '/run /tn OpenClaw MailBridge'
    }

    It 'emits suite E operator evidence keys' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'
        $clientRequests = $script:ClientRequests

        Set-Item -Path Function:\Global:Invoke-FakeClient -Value ({
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $commandName = [string]$Arguments[0]
                $clientRequests.Add(($Arguments -join ' '))

                switch ($commandName) {
                    'status' { '{"ok":true,"result":{"state":"ready","mode":"safe"}}' }
                    'list-messages' { '{"ok":true,"result":{"items":[{"bridgeId":"msg-1"}]}}' }
                    'get-message' { '{"ok":true,"result":{"bridgeId":"msg-1"}}' }
                    'list-meeting-requests' { '{"ok":true,"result":{"items":[]}}' }
                    'list-calendar' { '{"ok":true,"result":{"items":[{"bridgeId":"evt-1"}]}}' }
                    'get-event' { '{"ok":true,"result":{"bridgeId":"evt-1"}}' }
                    default { throw "Unexpected client command: $commandName" }
                }
            }.GetNewClosure())

        $output = & $testScriptPath `
            -ClientPath 'Invoke-FakeClient' `
            -TaskName 'OpenClaw MailBridge' `
            -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt' `
            -ExpectMessageData `
            -ExpectCalendarData `
            -OpenClawSvcPipeConnect $true `
            -NetworkDenyVerified $true

        @($output) | Should -Contain 'AutomatedSuitesPassed: A,B,C,D,F'
        $script:WrittenEvidenceState.Path | Should -Be 'virtual:\operator-evidence.txt'
        @($script:WrittenEvidence) | Should -Contain 'PrimaryInteractiveSession: True'
        @($script:WrittenEvidence) | Should -Contain 'OpenClawSvcPipeConnect: True'
        @($script:WrittenEvidence) | Should -Contain 'NetworkDenyVerified: True'
        @($script:ClientRequests | Where-Object { $_ -like 'get-message*' }).Count | Should -Be 1
        @($script:ClientRequests | Where-Object { $_ -like 'get-event*' }).Count | Should -Be 1
    }

    It 'emits publish and runtime evidence keys for the installed host and client' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\test-mailbridge.ps1'

        Set-Item -Path Function:\Global:Invoke-FakeClient -Value ({
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $commandName = [string]$Arguments[0]

                switch ($commandName) {
                    'status' { '{"ok":true,"result":{"state":"ready","mode":"safe"}}' }
                    'list-messages' { '{"ok":true,"result":{"items":[]}}' }
                    'list-meeting-requests' { '{"ok":true,"result":{"items":[]}}' }
                    'list-calendar' { '{"ok":true,"result":{"items":[]}}' }
                    default { throw "Unexpected client command: $commandName" }
                }
            }.GetNewClosure())

        $output = & $testScriptPath `
            -ClientPath 'Invoke-FakeClient' `
            -InstallRoot 'C:\Program Files\OpenClaw\MailBridge' `
            -TaskName 'OpenClaw MailBridge' `
            -OperatorEvidenceOutputPath 'virtual:\operator-evidence.txt'

        @($output) | Should -Contain 'PublishedBridgeTargetFramework: net10.0-windows'
        @($output) | Should -Contain 'PublishedClientTargetFramework: net10.0-windows'
        @($output) | Should -Contain 'BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0'
        @($output) | Should -Contain 'ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0'
    }
}


