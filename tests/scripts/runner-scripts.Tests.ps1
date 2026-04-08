Describe 'root PowerShell runner scripts' {
    BeforeEach {
        $global:DotnetCalls = [System.Collections.Generic.List[string]]::new()

        function global:dotnet {
            [CmdletBinding()]
            [OutputType([void])]
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [object[]]$Arguments
            )

            $null = $Arguments.Count
            $global:DotnetCalls.Add(($Arguments -join ' '))
        }
    }

    AfterEach {
        Remove-Item Function:\Global:dotnet -ErrorAction SilentlyContinue
        Remove-Variable -Name 'DotnetCalls' -Scope Global -ErrorAction SilentlyContinue
    }

    It 'builds the solution with the requested configuration' {
        $buildScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\Build.ps1'
        & $buildScriptPath -Configuration Release

        @($global:DotnetCalls).Count | Should -Be 1
        $global:DotnetCalls[0] | Should -Match '^build '
        $global:DotnetCalls[0] | Should -Match '-c Release'
        $global:DotnetCalls[0] | Should -Match ([regex]::Escape('OpenClaw.MailBridge.sln'))
    }

    It 'tests the solution with the requested configuration' {
        $testScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\Test.ps1'
        & $testScriptPath -Configuration Release

        @($global:DotnetCalls).Count | Should -Be 1
        $global:DotnetCalls[0] | Should -Match '^test '
        $global:DotnetCalls[0] | Should -Match '-c Release'
        $global:DotnetCalls[0] | Should -Match ([regex]::Escape('OpenClaw.MailBridge.sln'))
    }

    It 'runs the bridge project in Development mode' {
        $runBridgeScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\Run-Bridge.ps1'
        $env:ASPNETCORE_ENVIRONMENT = $null
        $env:DOTNET_ENVIRONMENT = $null

        & $runBridgeScriptPath -Configuration Release

        @($global:DotnetCalls).Count | Should -Be 1
        $global:DotnetCalls[0] | Should -Match '^run '
        $global:DotnetCalls[0] | Should -Match '--configuration Release'
        $global:DotnetCalls[0] | Should -Match ([regex]::Escape('OpenClaw.MailBridge\OpenClaw.MailBridge.csproj'))
        $env:ASPNETCORE_ENVIRONMENT | Should -Be 'Development'
        $env:DOTNET_ENVIRONMENT | Should -Be 'Development'
    }

    It 'runs the client project with the requested pipe name and message' {
        $runClientScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'scripts\Run-Client.ps1'
        & $runClientScriptPath -Configuration Release -PipeName 'custom-pipe' -Message 'hello'

        @($global:DotnetCalls).Count | Should -Be 1
        $global:DotnetCalls[0] | Should -Match '^run '
        $global:DotnetCalls[0] | Should -Match '--configuration Release'
        $global:DotnetCalls[0] | Should -Match '--pipe-name custom-pipe --message hello'
        $global:DotnetCalls[0] | Should -Match ([regex]::Escape('OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj'))
    }
}