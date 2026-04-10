[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PrimaryUser,
    [string]$InstallRoot = 'C:\Program Files\OpenClaw\MailBridge',
    [string]$TaskName = 'OpenClaw MailBridge'
)

$ErrorActionPreference = 'Stop'

function Get-RegisterMailBridgeTaskCommand {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath
    )

    return '"{0}" --config "{1}"' -f $ExecutablePath, $ConfigPath
}

function Invoke-SchtasksCommand {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$TaskArgs
    )

    $output = @(schtasks @TaskArgs 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $message = if ($output.Count -gt 0) {
            $output -join [Environment]::NewLine
        }
        else {
            'schtasks failed with no output.'
        }

        throw $message
    }

    return $output
}

function Get-PrimaryUserSessionCandidate {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$UserName
    )

    if ($UserName -match '\\') {
        return ($UserName -split '\\')[-1]
    }

    return $UserName
}

function Test-PrimaryUserLoggedOn {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PrimaryUser
    )

    $sessionCandidate = Get-PrimaryUserSessionCandidate -UserName $PrimaryUser
    $sessionOutput = @(query user 2>$null)

    return [bool]($sessionOutput | Select-String -SimpleMatch $sessionCandidate)
}

$configPath = Join-Path $env:LOCALAPPDATA 'OpenClaw\MailBridge\bridge.settings.json'
$exe = Join-Path $InstallRoot 'OpenClaw.MailBridge.exe'

if (-not (Test-Path $exe)) {
    throw "Bridge executable not found at '$exe'."
}

if (-not (Test-Path $configPath)) {
    throw "Bridge settings file not found at '$configPath'. Run install-mailbridge.ps1 first."
}

$taskCommand = Get-RegisterMailBridgeTaskCommand -ExecutablePath $exe -ConfigPath $configPath

if ($PSCmdlet.ShouldProcess($TaskName, 'Register interactive on-logon bridge task')) {
    $createArgs = @('/create', '/tn', $TaskName, '/tr', $taskCommand, '/sc', 'onlogon', '/ru', $PrimaryUser, '/it', '/f')
    $null = Invoke-SchtasksCommand -TaskArgs $createArgs

    if (Test-PrimaryUserLoggedOn -PrimaryUser $PrimaryUser) {
        $runArgs = @('/run', '/tn', $TaskName)
        $null = Invoke-SchtasksCommand -TaskArgs $runArgs
    }
}

