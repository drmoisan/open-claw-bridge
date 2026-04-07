Describe 'install-mailbridge.ps1' {
    BeforeEach {
        $scriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\install-mailbridge.ps1'
        . $scriptPath -PrimaryUser 'TestUser' -WhatIf
    }

    AfterEach {
        foreach ($functionName in @(
                'Get-BridgeDefaultConfiguration',
                'Get-OutlookApplicationType',
                'New-OutlookApplication',
                'Remove-ComObjectReference',
                'Test-OutlookProfilePrerequisite')) {
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
}
