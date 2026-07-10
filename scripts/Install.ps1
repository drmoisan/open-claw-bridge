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

$helperModulePath = Join-Path $PSScriptRoot 'Install.Helpers.psm1'
$preflightModulePath = Join-Path $PSScriptRoot 'Install.Preflight.psm1'
$dockerModulePath = Join-Path $PSScriptRoot 'Install.Docker.psm1'

try {
    Import-Module $helperModulePath -Force -Global -ErrorAction Stop
}
catch {
    throw "Failed to load bundled install helper module '$helperModulePath'. Ensure Install.ps1 is being run from a bundle produced by Publish.ps1 and that the bundle contents are intact. Original error: $($_.Exception.Message)"
}

try {
    Import-Module $preflightModulePath -Force -Global -ErrorAction Stop
}
catch {
    throw "Failed to load bundled install preflight module '$preflightModulePath'. Ensure Install.ps1 is being run from a bundle produced by Publish.ps1 and that the bundle contents are intact. Original error: $($_.Exception.Message)"
}

try {
    Import-Module $dockerModulePath -Force -Global -ErrorAction Stop
}
catch {
    throw "Failed to load bundled install docker module '$dockerModulePath'. Ensure Install.ps1 is being run from a bundle produced by Publish.ps1 and that the bundle contents are intact. Original error: $($_.Exception.Message)"
}

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

if (-not (Get-Command -Name 'Test-TcpPortOpen' -ErrorAction SilentlyContinue)) {
    function Test-TcpPortOpen {
        [CmdletBinding()]
        [OutputType([bool])]
        param(
            [Parameter(Mandatory = $true)][string]$IpAddress,
            [Parameter(Mandatory = $true)][int]$Port
        )
        $client = [System.Net.Sockets.TcpClient]::new()
        try { return $client.ConnectAsync($IpAddress, $Port).Wait(500) }
        catch { return $false }
        finally { $client.Dispose() }
    }
}
if (-not (Get-Command -Name 'Invoke-HostAdapterProcess' -ErrorAction SilentlyContinue)) {
    function Invoke-HostAdapterProcess {
        [CmdletBinding(SupportsShouldProcess = $true)]
        param([Parameter(Mandatory = $true)][System.Diagnostics.ProcessStartInfo]$ProcessStartInfo)
        if ($PSCmdlet.ShouldProcess($ProcessStartInfo.FileName, 'Start-Process')) {
            [System.Diagnostics.Process]::Start($ProcessStartInfo) | Out-Null
        }
    }
}
if (-not (Get-Command -Name 'Invoke-HostAdapterStart' -ErrorAction SilentlyContinue)) {
    function Invoke-HostAdapterStart {
        [CmdletBinding(SupportsShouldProcess = $true)]
        param(
            [Parameter(Mandatory = $true)][string]$HostAdapterExePath,
            [Parameter(Mandatory = $true)][string]$AspNetCoreUrls,
            [switch]$Force
        )
        if (-not (Test-Path -LiteralPath $HostAdapterExePath)) {
            throw "HostAdapter executable not found at '$HostAdapterExePath'. The bundle may be incomplete or the destination copy did not complete."
        }
        $port = [UriBuilder]::new($AspNetCoreUrls).Port
        if (Test-TcpPortOpen -IpAddress '127.0.0.1' -Port $port) {
            $listenerPid = Get-ListeningProcessId -Port $port
            $staleAutoStopped = $false
            if ($null -ne $listenerPid) {
                $observedPath = Get-ProcessMainModulePath -ProcessId $listenerPid
                $pathsMatch = $false
                if (-not [string]::IsNullOrWhiteSpace($observedPath)) {
                    $pathsMatch = [string]::Equals($observedPath, $HostAdapterExePath, [System.StringComparison]::OrdinalIgnoreCase)
                }
                if (-not $pathsMatch) {
                    $observedDisplay = if ([string]::IsNullOrWhiteSpace($observedPath)) { '(unavailable)' } else { $observedPath }
                    if ($Force) {
                        # -Force auto-stop: terminate the stale process and fall through to launch the bundle's HostAdapter.
                        Write-Information "[install:hostadapter-start] Stale HostAdapter detected on port $port`: PID $listenerPid at '$observedDisplay'. -Force auto-stop applied; stopping stale process." -InformationAction Continue
                        Stop-Process -Id $listenerPid -Force
                        $staleAutoStopped = $true
                    }
                    else {
                        throw "Stale HostAdapter detected on port $port`: PID $listenerPid at '$observedDisplay' does not match the bundle's HostAdapter at '$HostAdapterExePath'. Stop the stale process (Stop-Process -Id $listenerPid) and rerun Install.ps1."
                    }
                }
            }
            if (-not $staleAutoStopped) {
                Write-Information "[install:hostadapter-start] HostAdapter already running on port $port; skipping start." -InformationAction Continue
                return
            }
        }
        $psi = [System.Diagnostics.ProcessStartInfo]::new()
        $psi.FileName = $HostAdapterExePath
        $psi.UseShellExecute = $false
        $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
        $psi.EnvironmentVariables['ASPNETCORE_URLS'] = $AspNetCoreUrls
        if ($PSCmdlet.ShouldProcess($HostAdapterExePath, 'Start-HostAdapter')) {
            Invoke-HostAdapterProcess -ProcessStartInfo $psi
        }
        Write-Information "[install:hostadapter-start] HostAdapter process launched from '$HostAdapterExePath'." -InformationAction Continue
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
        Write-Information '[install:force-uninstall] Retaining installed MSIX; Add-AppxPackage at Stage 8 will replace the same-name package.' -InformationAction Continue
        if (Test-Path -LiteralPath $InstallRecordPath) {
            $prior = Read-InstallRecord -RecordPath $InstallRecordPath
            try {
                if ($prior.PSObject.Properties.Name -contains 'skipDocker' -and (-not $prior.skipDocker)) {
                    Invoke-ComposeDown -ComposeFilePath $prior.composeFilePath -ProjectName $prior.composeProjectName
                }
            }
            catch { Write-Information "[install:force-uninstall] compose-down tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
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

    # Stage 7a: launch HostAdapter from bundle if not already running.
    if (-not $SkipDocker) {
        Write-Information '[install:hostadapter-start] Ensuring HostAdapter is running' -InformationAction Continue
        $HostAdapterExePath = Join-Path $DestinationPath 'executables\OpenClaw.HostAdapter\OpenClaw.HostAdapter.exe'
        $hostAdapterUri = Get-HostAdapterPreflightUri -EnvMap (Get-InstallEnvFileMap -EnvFilePath (Join-Path $DestDockerDir '.env'))
        Invoke-HostAdapterStart -HostAdapterExePath $HostAdapterExePath -AspNetCoreUrls "$($hostAdapterUri.Scheme)://$($hostAdapterUri.Host):$($hostAdapterUri.Port)" -Force:$Force
    }
    # Stage 7 preflight: HostAdapter readiness guard runs before any state-changing
    # operations so that a failed preflight leaves nothing installed. This follows
    # the same pattern as the Stage 4 Docker readiness guard and Stage 6 gateway
    # token guard.
    if (-not $SkipDocker) {
        Write-Information '[install:hostadapter-check] Verifying HostAdapter is responsive before MSIX install' -InformationAction Continue
        Assert-HostAdapterRespondingPreflight -DestDockerDir $DestDockerDir
    }

    # Stage 8: MSIX install + capture.
    $MsixPath = Join-Path $BundleRoot "msix/OpenClaw.MailBridge_${ResolvedVersion}_x64.msix"
    if (-not (Test-Path -LiteralPath $MsixPath)) {
        throw "MSIX not found at expected path '$MsixPath'. The bundle may be incomplete."
    }
    Write-Information "[install:msix] Installing MSIX $MsixPath" -InformationAction Continue
    Invoke-MsixInstall -MsixPath $MsixPath -AllowUnsigned:$AllowUnsigned
    $PackageFullName = Invoke-MsixCapture

    # Stage 8b: first-launch protocol activation (issue #62). windows.startupTask only
    # fires at logon, so the installer must explicitly activate MailBridge in the
    # operator session before the Stage 8.5 readiness gate polls it. Skipped under
    # -SkipDocker (mirrors Stage 7a/Stage 7/Stage 8.5). Activation failure triggers the
    # same MSIX rollback as Stage 8.5 to preserve the no-orphan invariant (issue #52).
    if (-not $SkipDocker) {
        Write-Information '[install:msix-activate] Activating MailBridge via protocol handler' -InformationAction Continue
        try {
            Invoke-MsixAppActivate -ActivationUri 'openclaw-mailbridge:firstrun'
        }
        catch {
            $activateError = $_.Exception.Message
            try { Invoke-MsixRemove -PackageFullName $PackageFullName }
            catch { Write-Information "[install:msix-activate] msix rollback tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
            throw $activateError
        }
    }

    # Stage 8.5: bridge-readiness preflight runs after MSIX install so MailBridge is
    # present, with rollback when bridge is not ready (preserves the no-orphan
    # invariant from issue #52).
    if (-not $SkipDocker) {
        Write-Information '[install:hostadapter-bridge-check] Verifying MailBridge readiness after MSIX install' -InformationAction Continue
        try {
            Assert-HostAdapterBridgeReadyPreflight -DestDockerDir $DestDockerDir
        }
        catch {
            $bridgeReadyError = $_.Exception.Message
            try { Invoke-MsixRemove -PackageFullName $PackageFullName }
            catch { Write-Information "[install:hostadapter-bridge-check] msix rollback tolerated failure: $($_.Exception.Message)" -InformationAction Continue }
            throw $bridgeReadyError
        }
    }

    Write-Information "[install:msix] PackageFullName $PackageFullName" -InformationAction Continue

    # Stage 9: compose up + health poll (skipped when -SkipDocker).
    $ComposeFilePath = Join-Path $DestDockerDir 'docker-compose.yml'
    if (-not $SkipDocker) {
        $ImageTarPath = Join-Path $DestDockerDir 'openclaw-images.tar'
        Write-Information "[install:docker] Loading bundled container images from $ImageTarPath" -InformationAction Continue
        Invoke-DockerImageLoad -ImageTarPath $ImageTarPath
        Write-Information '[install:docker] Starting compose stack' -InformationAction Continue
        Invoke-ComposeUp -DestDockerDir $DestDockerDir -ComposeFilePath $ComposeFilePath
        Wait-ComposeHealthy -ComposeFilePath $ComposeFilePath
    }

    # Stage 10: install record.
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
