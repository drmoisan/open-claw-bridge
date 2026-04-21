# PSScriptAnalyzer suppressions for this test-fixture module:
#  - PSUseShouldProcessForStateChangingFunctions: the helpers named with the
#    `New-` and `Install-`/`Reset-` verbs are deterministic test-scaffolding
#    functions that never write to the filesystem, network, or user state;
#    they only create in-memory objects or manipulate a global function slot
#    in the test process. ShouldProcess support is not meaningful for test
#    fixtures.
#  - PSUseOutputTypeCorrectly: generic-list return type annotations are
#    declared but PSScriptAnalyzer cannot always infer them through the
#    unary comma operator used to preserve enumeration; explicitly declared
#    [OutputType] is already present.
#  - PSProvideCommentHelp: the per-function block comments below document
#    intent; Information-severity help findings are suppressed at module
#    scope to keep the file compact.
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test-fixture helpers do not alter user state; they manipulate in-memory or test-local function slots only.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '', Justification = 'Unary comma operator returns the declared generic list type; the analyzer cannot always infer this.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSProvideCommentHelp', '', Justification = 'Per-function intent documented inline; this is internal test-fixture scaffolding.')]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Shared fixture module for Invoke-OpenClawContainerPathValidation.ps1 tests.
#
# Each split *.Tests.ps1 file imports this module in BeforeAll to load the
# OpenClawContainerValidation module, then calls the helpers below from
# BeforeEach/AfterEach. The split files use $script:RequestedUris,
# $script:DockerRequests, and $script:ScriptPath as a single source of truth
# for per-test state.

function Import-OpenClawContainerValidationModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$TestsRoot
    )

    $modulePath = Join-Path (Resolve-Path (Join-Path $TestsRoot '..\..')).Path 'scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

function Get-ContainerValidationScriptPath {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)][string]$TestsRoot
    )

    Join-Path (Resolve-Path (Join-Path $TestsRoot '..\..')).Path 'scripts\Invoke-OpenClawContainerPathValidation.ps1'
}

function New-RequestedUriList {
    [CmdletBinding()]
    [OutputType([System.Collections.Generic.List[string]])]
    param()

    , [System.Collections.Generic.List[string]]::new()
}

function New-DockerRequestList {
    [CmdletBinding()]
    [OutputType([System.Collections.Generic.List[string]])]
    param()

    , [System.Collections.Generic.List[string]]::new()
}

function Install-DefaultInvokeFakeDocker {
    [CmdletBinding()]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'DockerRequests', Justification = 'Captured by GetNewClosure into the fake-docker scriptblock; static analyzer cannot see closure capture.')]
    param(
        # ValidateNotNull is required (rather than Mandatory alone) because PowerShell's
        # default Mandatory binder rejects an empty generic list as an empty collection.
        # The list is intentionally empty at the time of the install call; tests fill it.
        [Parameter(Mandatory = $true)]
        [ValidateNotNull()]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$DockerRequests
    )

    # The scriptblock captures $DockerRequests via GetNewClosure() so each call
    # to the fake docker records into the caller-supplied list. Strict mode
    # inside the scriptblock requires $DockerRequests to be in scope before
    # .GetNewClosure() runs, which it is (this function's parameter).
    $fakeDockerBody = {
        [CmdletBinding()]
        param(
            [Parameter(ValueFromRemainingArguments = $true)]
            [object[]]$Arguments
        )

        $commandLine = $Arguments -join ' '
        $DockerRequests.Add($commandLine)
        $global:LASTEXITCODE = 0
        switch ($commandLine) {
            'version --format {{.Server.Version}}' { '25.0.0' }
            'container inspect openclaw-core' { '[{"Name":"/openclaw-core","Config":{"Image":"openclaw/core:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]' }
            'container inspect openclaw-agent' { '[{"Name":"/openclaw-agent","Config":{"Image":"openclaw/agent:pre-mvp"},"State":{"Status":"running","Running":true,"Health":{"Status":"healthy"}}}]' }
            default {
                $global:LASTEXITCODE = 1
                "Unexpected docker command: $commandLine"
            }
        }
    }.GetNewClosure()

    Set-Item -Path Function:\Global:Invoke-FakeDocker -Value $fakeDockerBody
}

function Reset-ContainerPathValidationTestState {
    [CmdletBinding()]
    param()

    Remove-Item Function:\Global:Invoke-FakeDocker -ErrorAction SilentlyContinue
}

Export-ModuleMember -Function @(
    'Import-OpenClawContainerValidationModule',
    'Get-ContainerValidationScriptPath',
    'New-RequestedUriList',
    'New-DockerRequestList',
    'Install-DefaultInvokeFakeDocker',
    'Reset-ContainerPathValidationTestState'
)
