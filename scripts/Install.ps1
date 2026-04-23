#Requires -Version 7.0
<#
.SYNOPSIS
    Installs an OpenClaw bundle produced by scripts/Publish.ps1 on a Windows
    host: verifies the bundle manifest, copies executables and docker
    artifacts, installs the MSIX via Add-AppxPackage, starts the docker
    compose stack, and writes a single-record install manifest at
    %LOCALAPPDATA%\OpenClaw\install-record.json.

    The script self-locates the bundle via $PSScriptRoot. The install scripts
    (Install.ps1, Uninstall.ps1, Install.Helpers.psm1) ship INSIDE every
    bundle produced by Publish.ps1, so operators `cd` into the bundle
    directory and run `.\Install.ps1` with no arguments.

.DESCRIPTION
    Planner decisions captured in this header:
      Q1  Wait-ComposeHealthy defaults to -TimeoutSeconds 90 and
          -PollIntervalSeconds 3 (30s safety margin above the longer
          docker-compose.yml start_period of 30s on openclaw-agent).
      Q2  -AllowUnsigned performs a proactive administrator precheck before
          any filesystem side effects. When the current PowerShell session is
          not elevated, the script aborts with a remediation message naming
          both the relaunch path and the signing-cert alternative.
      Q3  Runbook structure: docs/mailbridge-runbook.md includes a new
          "Install Path D: Scripted Bundle Install" section that documents
          this script.

    Delegates the helpers to scripts/Install.Helpers.psm1. The orchestrator
    itself stays thin and under 500 lines.

.PARAMETER SourcePath
    Absolute or relative path to the bundle root. Default is $PSScriptRoot
    (the directory the script lives in). Production installs rely on the
    default; -SourcePath is a dev/test override.

.PARAMETER AllowUnsigned
    Passes -AllowUnsigned to Add-AppxPackage. Required for bundles produced
    with Publish.ps1 -SkipSign. Triggers the Q2 administrator precheck.

.PARAMETER SkipDocker
    Skips the Docker readiness check, compose up, and health polling. The
    install record captures skipDocker = true so Uninstall.ps1 mirrors the
    skip.

.PARAMETER DockerEnvFilePath
    Optional operator-managed Docker .env file to copy into the installed
    docker directory after bundle manifest validation. Do not place this file
    inside artifacts/publish/<version>; that directory is manifest-controlled.

.PARAMETER AnthropicEnvFilePath
    Optional operator-managed Anthropic env file to copy to
    docker/secrets/.env.anthropic in the installed docker directory after
    bundle manifest validation. Do not place this file inside
    artifacts/publish/<version>; that directory is manifest-controlled.

.PARAMETER Force
    Performs a full uninstall-then-install against any prior install of the
    same version (compose down, remove MSIX, remove destination folder,
    delete install record).

.EXAMPLE
    cd artifacts/publish/1.2.3.0
    .\Install.ps1
    Installs the bundle whose directory the script lives in (signed MSIX,
    docker stage included).

.EXAMPLE
    .\Install.ps1 -SkipDocker
    Installs only the MSIX; records skipDocker = true.

.EXAMPLE
    .\Install.ps1 -DockerEnvFilePath "$env:LOCALAPPDATA\OpenClaw\operator-config\.env" -AnthropicEnvFilePath "$env:LOCALAPPDATA\OpenClaw\operator-config\secrets\.env.anthropic"
    Installs the bundle and stages operator-managed Docker env files from a
    version-neutral local configuration directory.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourcePath = $PSScriptRoot,

    [switch]$AllowUnsigned,

    [switch]$SkipDocker,

    [string]$DockerEnvFilePath,

    [string]$AnthropicEnvFilePath,

    [switch]$Force
)

Import-Module (Join-Path $PSScriptRoot 'Install.Helpers.psm1') -Force
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Admin probe wrapper. Defined at script scope so tests can override it with a
# global function; the production path returns the real elevation result.
if (-not (Get-Command -Name 'Test-IsElevatedAdmin' -ErrorAction SilentlyContinue)) {
    function Test-IsElevatedAdmin {
        [CmdletBinding()]
        [OutputType([bool])]
        param()
        $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
}

function Assert-OptionalInputFile {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ParameterName
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "The file supplied by -$ParameterName was not found at '$Path'. Keep operator env files outside artifacts/publish/<version> and pass their local paths to Install.ps1."
    }
}

function Copy-OperatorDockerConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestDockerDir,

        [AllowEmptyString()]
        [string]$DockerEnvFilePath,

        [AllowEmptyString()]
        [string]$AnthropicEnvFilePath
    )

    if (-not [string]::IsNullOrWhiteSpace($DockerEnvFilePath)) {
        $destEnv = Join-Path $DestDockerDir '.env'
        Write-Information "[install:env] Copying operator Docker env file to $destEnv" -InformationAction Continue
        Copy-Item -LiteralPath $DockerEnvFilePath -Destination $destEnv -Force
    }

    if (-not [string]::IsNullOrWhiteSpace($AnthropicEnvFilePath)) {
        $destSecretsDir = Join-Path $DestDockerDir 'secrets'
        $destAnthropicEnv = Join-Path $destSecretsDir '.env.anthropic'
        Write-Information "[install:env] Copying operator Anthropic env file to $destAnthropicEnv" -InformationAction Continue
        $null = New-Item -ItemType Directory -Path $destSecretsDir -Force
        Copy-Item -LiteralPath $AnthropicEnvFilePath -Destination $destAnthropicEnv -Force
    }
}

function Assert-DockerRuntimeInput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestDockerDir
    )

    $anthropicEnvPath = Join-Path $DestDockerDir 'secrets/.env.anthropic'
    if (-not (Test-Path -LiteralPath $anthropicEnvPath)) {
        throw "Required Docker secret file not found at '$anthropicEnvPath'. Keep the file outside artifacts/publish/<version> and pass -AnthropicEnvFilePath to Install.ps1, or pass -SkipDocker and stage Docker configuration before starting compose manually."
    }
}

function Assert-StagedGatewayTokenPresent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestDockerDir
    )

    $envFilePath = Join-Path $DestDockerDir '.env'
    if (-not (Test-Path -LiteralPath $envFilePath)) {
        throw "Staged Docker .env was not found at '$envFilePath'. Expected Initialize-DotEnv or -DockerEnvFilePath to place it there. Investigate the staging step before retrying."
    }

    $envMap = Get-InstallEnvFileMap -EnvFilePath $envFilePath
    $hasKey = $envMap.ContainsKey('OPENCLAW_GATEWAY_TOKEN')
    $value = if ($hasKey) { [string]$envMap['OPENCLAW_GATEWAY_TOKEN'] } else { '' }
    if (-not $hasKey -or [string]::IsNullOrWhiteSpace($value)) {
        throw "Staged Docker .env at '$envFilePath' is missing a non-empty OPENCLAW_GATEWAY_TOKEN. The openclaw-agent container reads this value via SecretRef from the baked workspace config and will crash-loop at start if it is empty. Populate it by running scripts/Invoke-OpenClawAgentOnboarding.ps1 -EnvFilePath <operator-config>\.env before Install.ps1, or pass -SkipDocker to skip the container stage."
    }
}

function Get-InstallEnvFileMap {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$EnvFilePath
    )

    $map = @{}
    if (-not (Test-Path -LiteralPath $EnvFilePath)) {
        return $map
    }

    $lines = @(Get-Content -LiteralPath $EnvFilePath)
    foreach ($line in $lines) {
        if ($null -eq $line) {
            continue
        }

        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
            continue
        }

        $equalsIndex = $trimmed.IndexOf('=')
        if ($equalsIndex -lt 1) {
            continue
        }

        $key = $trimmed.Substring(0, $equalsIndex).Trim()
        $value = $trimmed.Substring($equalsIndex + 1).Trim().Trim([char[]]@('"', "'"))
        $map[$key] = $value
    }

    return $map
}

function Get-InstallEndpointUri {
    [CmdletBinding()]
    [OutputType([uri])]
    param(
        [Parameter(Mandatory = $true)]
        [uri]$BaseUri,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $builder = [UriBuilder]::new($BaseUri)
    $basePath = $builder.Path.TrimEnd('/')
    $relativePath = $Path.TrimStart('/')
    $builder.Path = if ([string]::IsNullOrWhiteSpace($basePath)) {
        $relativePath
    }
    else {
        '{0}/{1}' -f $basePath, $relativePath
    }

    return $builder.Uri
}

function Get-HostAdapterPreflightUri {
    [CmdletBinding()]
    [OutputType([uri])]
    param(
        [hashtable]$EnvMap
    )

    $baseUrl = 'http://host.docker.internal:4319/v1'
    if ($EnvMap -and $EnvMap.ContainsKey('OpenClaw__HostAdapter__BaseUrl') -and -not [string]::IsNullOrWhiteSpace([string]$EnvMap['OpenClaw__HostAdapter__BaseUrl'])) {
        $baseUrl = [string]$EnvMap['OpenClaw__HostAdapter__BaseUrl']
    }

    try {
        $builder = [UriBuilder]::new([uri]$baseUrl)
    }
    catch {
        throw "OpenClaw__HostAdapter__BaseUrl in the installed docker .env is not a valid URI: '$baseUrl'."
    }

    if ($builder.Host -eq 'host.docker.internal') {
        $builder.Host = '127.0.0.1'
    }

    return Get-InstallEndpointUri -BaseUri $builder.Uri -Path '/status'
}

function Assert-HostAdapterRuntimePreflight {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestDockerDir
    )

    $envFilePath = Join-Path $DestDockerDir '.env'
    $envMap = Get-InstallEnvFileMap -EnvFilePath $envFilePath
    $tokenFilePath = 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token'
    if ($envMap.ContainsKey('HOSTADAPTER_TOKEN_FILE') -and -not [string]::IsNullOrWhiteSpace([string]$envMap['HOSTADAPTER_TOKEN_FILE'])) {
        $tokenFilePath = [Environment]::ExpandEnvironmentVariables([string]$envMap['HOSTADAPTER_TOKEN_FILE'])
    }

    if (-not (Test-Path -LiteralPath $tokenFilePath)) {
        throw "HostAdapter preflight failed before starting Docker. Token file not found at '$tokenFilePath'. Provision the HostAdapter token file, start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
    }

    $token = (Get-Content -LiteralPath $tokenFilePath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "HostAdapter preflight failed before starting Docker. Token file '$tokenFilePath' is empty. Provision a non-empty HostAdapter token, start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
    }

    $statusUri = Get-HostAdapterPreflightUri -EnvMap $envMap
    try {
        $response = Invoke-WebRequest `
            -Uri $statusUri `
            -Method Get `
            -Headers @{ Authorization = "Bearer $token" } `
            -UseBasicParsing `
            -SkipHttpErrorCheck `
            -TimeoutSec 10
    }
    catch {
        throw "HostAdapter preflight failed before starting Docker. GET $statusUri was unreachable: $($_.Exception.Message). Start OpenClaw.HostAdapter and OpenClaw.MailBridge, then retry; or pass -SkipDocker to skip the container stage."
    }

    if ([int]$response.StatusCode -ne 200) {
        throw "HostAdapter preflight failed before starting Docker. GET $statusUri returned HTTP $($response.StatusCode). Confirm OpenClaw.HostAdapter is running, the token is valid, and OpenClaw.MailBridge is running, then retry; or pass -SkipDocker to skip the container stage."
    }
}

# --- Main (only runs when executed directly, not when dot-sourced for tests) ---
if ($MyInvocation.InvocationName -ne '.') {

    # Stage 0: -AllowUnsigned administrator precheck (Planner Q2).
    if ($AllowUnsigned) {
        Write-Information '[install:precheck] Verifying administrator privileges for -AllowUnsigned' -InformationAction Continue
        if (-not (Test-IsElevatedAdmin)) {
            throw '-AllowUnsigned requires the current PowerShell session to run as administrator when the MSIX contains executable content. Relaunch PowerShell as administrator and retry, or install the signing certificate to Cert:\LocalMachine\TrustedPeople and omit -AllowUnsigned. See https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package for details.'
        }
    }

    Assert-OptionalInputFile -Path $DockerEnvFilePath -ParameterName 'DockerEnvFilePath'
    Assert-OptionalInputFile -Path $AnthropicEnvFilePath -ParameterName 'AnthropicEnvFilePath'

    # Stage 1: bundle selection. Bundle root = -SourcePath (defaults to
    # $PSScriptRoot, the directory this script lives in after it has been
    # staged into the bundle by Publish.ps1).
    $BundleRoot = $SourcePath
    $BundleManifestPath = Join-Path $BundleRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $BundleManifestPath)) {
        throw "manifest.json not found at '$BundleManifestPath'. Ensure Install.ps1 is executed from a bundle directory produced by Publish.ps1 (or pass -SourcePath to a valid bundle root)."
    }
    $ResolvedVersion = Get-ManifestVersion -BundleRoot $BundleRoot
    Write-Information "[install:select] Selected bundle root $BundleRoot (version $ResolvedVersion)" -InformationAction Continue

    # Stage 2: manifest integrity.
    Write-Information '[install:verify] Validating manifest integrity' -InformationAction Continue
    Test-ManifestIntegrity -BundleRoot $BundleRoot

    # Stage 3: prior-install detection and -Force handling.
    $InstallRecordPath = Join-Path $env:LOCALAPPDATA 'OpenClaw/install-record.json'
    $DestinationPath = Join-Path $env:LOCALAPPDATA "OpenClaw/$ResolvedVersion"
    $priorExists = (Test-Path -LiteralPath $InstallRecordPath) -or (Test-Path -LiteralPath $DestinationPath)
    if ($priorExists) {
        if (-not $Force) {
            throw "A prior install exists at '$DestinationPath' or '$InstallRecordPath'. Pass -Force or run .\scripts\Uninstall.ps1 first."
        }
        Write-Information '[install:force-uninstall] Running prior-install uninstall sequence' -InformationAction Continue
        if (Test-Path -LiteralPath $InstallRecordPath) {
            $prior = Read-InstallRecord -RecordPath $InstallRecordPath
            try {
                if ($prior.PSObject.Properties.Name -contains 'skipDocker' -and (-not $prior.skipDocker)) {
                    Invoke-ComposeDown -ComposeFilePath $prior.composeFilePath -ProjectName $prior.composeProjectName
                }
            }
            catch { Write-Information "[install:force-uninstall] compose-down tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
            try { Invoke-MsixRemove -PackageFullName $prior.packageFullName }
            catch { Write-Information "[install:force-uninstall] msix-remove tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
            try {
                if (Test-Path -LiteralPath $prior.destinationPath) {
                    Remove-Item -LiteralPath $prior.destinationPath -Recurse -Force
                }
            }
            catch { Write-Information "[install:force-uninstall] remove-destination tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
            try { Remove-Item -LiteralPath $InstallRecordPath -Force }
            catch { Write-Information "[install:force-uninstall] remove-record tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
        }
        elseif (Test-Path -LiteralPath $DestinationPath) {
            try { Invoke-MsixRemove -PackageFullName '' }
            catch { Write-Information "[install:force-uninstall] destination-only msix-remove tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
            try { Remove-Item -LiteralPath $DestinationPath -Recurse -Force }
            catch { Write-Information "[install:force-uninstall] remove-destination tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
        }
    }

    # Stage 4: Docker readiness (skipped when -SkipDocker).
    if (-not $SkipDocker) {
        Write-Information '[install:docker-check] Running docker info readiness probe' -InformationAction Continue
        $null = Test-DockerAvailable
    }

    # Stage 5: bundle copy.
    Write-Information "[install:copy] Creating destination $DestinationPath" -InformationAction Continue
    $null = New-Item -ItemType Directory -Path $DestinationPath -Force
    Copy-BundleContents -SourceBundleRoot $BundleRoot -DestinationRoot $DestinationPath

    # Stage 6: .env guard.
    $DestDockerDir = Join-Path $DestinationPath 'docker'
    Write-Information '[install:env] Provisioning .env when absent' -InformationAction Continue
    Initialize-DotEnv -DestDockerDir $DestDockerDir
    Copy-OperatorDockerConfiguration -DestDockerDir $DestDockerDir -DockerEnvFilePath $DockerEnvFilePath -AnthropicEnvFilePath $AnthropicEnvFilePath
    if (-not $SkipDocker) {
        Assert-DockerRuntimeInput -DestDockerDir $DestDockerDir
        Write-Information '[install:env-guard] Verifying OPENCLAW_GATEWAY_TOKEN is present in staged .env' -InformationAction Continue
        Assert-StagedGatewayTokenPresent -DestDockerDir $DestDockerDir
    }

    # Stage 7: MSIX install + capture.
    $MsixPath = Join-Path $BundleRoot "msix/OpenClaw.MailBridge_${ResolvedVersion}_x64.msix"
    if (-not (Test-Path -LiteralPath $MsixPath)) {
        throw "MSIX not found at expected path '$MsixPath'. The bundle may be incomplete."
    }
    Write-Information "[install:msix] Installing MSIX $MsixPath" -InformationAction Continue
    Invoke-MsixInstall -MsixPath $MsixPath -AllowUnsigned:$AllowUnsigned
    $PackageFullName = Invoke-MsixCapture
    Write-Information "[install:msix] PackageFullName $PackageFullName" -InformationAction Continue

    # Stage 8: compose up + health poll (skipped when -SkipDocker).
    $ComposeFilePath = Join-Path $DestDockerDir 'docker-compose.yml'
    if (-not $SkipDocker) {
        Write-Information '[install:hostadapter-check] Verifying HostAdapter and MailBridge readiness before compose up' -InformationAction Continue
        Assert-HostAdapterRuntimePreflight -DestDockerDir $DestDockerDir
        Write-Information '[install:docker] Starting compose stack' -InformationAction Continue
        Invoke-ComposeUp -DestDockerDir $DestDockerDir -ComposeFilePath $ComposeFilePath
        Wait-ComposeHealthy -ComposeFilePath $ComposeFilePath
    }

    # Stage 9: install record.
    Write-Information '[install:record] Writing install record' -InformationAction Continue
    $record = [pscustomobject]@{
        installedAt        = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        version            = $ResolvedVersion
        sourcePath         = $BundleRoot
        destinationPath    = $DestinationPath
        packageFullName    = $PackageFullName
        composeProjectName = 'openclaw'
        composeFilePath    = $ComposeFilePath
        skipDocker         = [bool]$SkipDocker
        allowUnsigned      = [bool]$AllowUnsigned
    }
    Write-InstallRecord -Record $record -RecordPath $InstallRecordPath
    Write-Information "[install] Installed version $ResolvedVersion to $DestinationPath" -InformationAction Continue
}
