Describe 'install-mailbridge.ps1' {
    BeforeEach {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\install-mailbridge.ps1'
        . $scriptPath -PrimaryUser 'TestUser' -WhatIf
    }

    AfterEach {
        foreach ($functionName in @(
                'Get-BridgeDefaultConfiguration',
                'Get-OutlookApplicationType',
                'Get-RuntimeFrameworkDescription',
                'New-OutlookApplication',
                'Remove-ComObjectReference',
                'Test-IsElevated',
                'Test-OutlookProfilePrerequisite',
                'Assert-DotNet10RuntimeConfig',
                'Wait-BridgeStatusPreflight')) {
            Remove-Item -Path ("Function:\{0}" -f $functionName) -ErrorAction SilentlyContinue
        }
    }

    It 'seeds safe mode defaults when the settings file is absent' {
        $config = Get-BridgeDefaultConfiguration

        $config.mode | Should -Be 'safe'
        $config.pipeName | Should -Be 'openclaw_mailbridge_v1'
        $config.autostartOutlook | Should -BeTrue
        $config.bodyPreviewMaxChars | Should -Be 500
    }

    It 'returns a boolean from Test-IsElevated' {
        $result = Test-IsElevated

        $result | Should -BeOfType [bool]
    }

    It 'fails preflight when Outlook COM is unavailable' {
        Mock Get-OutlookApplicationType { $null }

        { Test-OutlookProfilePrerequisite } | Should -Throw 'Outlook COM unavailable*'
    }

    It 'releases acquired COM objects after successful preflight' {
        $script:RequestedNamespace = $null
        $script:RequestedFolders = [System.Collections.Generic.List[int]]::new()
        $script:ReleasedObjects = [System.Collections.Generic.List[object]]::new()
        $script:FakeNamespace = [pscustomobject]@{}
        $null = $script:FakeNamespace | Add-Member -MemberType ScriptMethod -Name Logon -Value {
            param($Argument1, $Argument2, $Argument3, $Argument4)

            $null = @($Argument1, $Argument2, $Argument3, $Argument4)
        } -PassThru
        $null = $script:FakeNamespace | Add-Member -MemberType ScriptMethod -Name GetDefaultFolder -Value {
            param($FolderId)

            $script:RequestedFolders.Add([int]$FolderId)
            return [pscustomobject]@{ Id = $FolderId }
        } -PassThru

        $fakeOutlook = [pscustomobject]@{}
        $null = $fakeOutlook | Add-Member -MemberType ScriptMethod -Name GetNamespace -Value {
            param($NamespaceName)

            $script:RequestedNamespace = $NamespaceName
            return $script:FakeNamespace
        } -PassThru

        Mock Get-OutlookApplicationType { [pscustomobject]@{ Name = 'Outlook.Application' } }
        Mock New-OutlookApplication { $fakeOutlook }
        Mock Remove-ComObjectReference {
            param($ComObject)

            $script:ReleasedObjects.Add($ComObject)
        }

        { Test-OutlookProfilePrerequisite } | Should -Not -Throw

        $script:RequestedNamespace | Should -Be 'MAPI'
        @($script:RequestedFolders) | Should -Be @(6, 9)
        @($script:ReleasedObjects) | Should -Contain $script:FakeNamespace
        @($script:ReleasedObjects) | Should -Contain $fakeOutlook
    }

    It 'rejects a bridge host runtimeconfig that does not require .NET 10' {
        Mock Test-Path {
            param($Path)

            $Path -eq 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.runtimeconfig.json'
        }
        Mock Get-Content {
            '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}'
        }

        {
            Assert-DotNet10RuntimeConfig `
                -RuntimeConfigPath 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.runtimeconfig.json' `
                -ComponentName 'Bridge host'
        } | Should -Throw '*requires .NET 10*'
    }

    It 'rejects a client runtimeconfig that does not require .NET 10' {
        Mock Test-Path {
            param($Path)

            $Path -eq 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.runtimeconfig.json'
        }
        Mock Get-Content {
            '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}'
        }

        {
            Assert-DotNet10RuntimeConfig `
                -RuntimeConfigPath 'C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.runtimeconfig.json' `
                -ComponentName 'Client'
        } | Should -Throw '*requires .NET 10*'
    }

    It 'tolerates delayed bridge status readiness after registration' {
        $statusCalls = [System.Collections.Generic.List[string]]::new()

        Set-Item -Path Function:\Global:Invoke-DelayedStatusClient -Value ({
                [CmdletBinding()]
                [OutputType([string])]
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [object[]]$Arguments
                )

                $statusCalls.Add(($Arguments -join ' '))
                if ($statusCalls.Count -lt 3) {
                    return $null
                }

                return '{"ok":true,"result":{"state":"ready","mode":"safe"}}'
            }.GetNewClosure())
        Mock Start-Sleep {}

        $status = Wait-BridgeStatusPreflight -ClientPath 'Invoke-DelayedStatusClient' -ReadyTimeoutSeconds 5 -PollIntervalMilliseconds 1

        $status | Should -Be '{"ok":true,"result":{"state":"ready","mode":"safe"}}'
        $statusCalls.Count | Should -Be 3
    }
}

