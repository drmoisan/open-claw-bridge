#Requires -Version 5.1
<#
.SYNOPSIS
    Creates a self-signed code-signing certificate for MSIX development and sideloading.

.DESCRIPTION
    Generates a new self-signed certificate suitable for signing MSIX packages during development
    and CI. Exports the certificate as PFX and CER to the specified output directory and installs
    the CER into the LocalMachine\Root trusted-root store to enable sideloading.

.PARAMETER Subject
    The certificate subject distinguished name. Must match the Publisher attribute in Package.appxmanifest.
    Defaults to 'CN=OpenClaw, O=OpenClaw, C=US'.

.PARAMETER OutputDir
    Directory path where the exported PFX and CER files are written.
    Defaults to 'artifacts'.

.PARAMETER PfxPassword
    Mandatory SecureString used to protect the exported PFX file.

.EXAMPLE
    $pwd = ConvertTo-SecureString 'dev' -AsPlainText -Force
    .\New-MsixDevCert.ps1 -PfxPassword $pwd

.EXAMPLE
    $pwd = ConvertTo-SecureString 'mypass' -AsPlainText -Force
    .\New-MsixDevCert.ps1 -Subject 'CN=OpenClaw, O=OpenClaw, C=US' -OutputDir 'artifacts' -PfxPassword $pwd
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Subject = 'CN=OpenClaw, O=OpenClaw, C=US',

    [string]$OutputDir = 'artifacts',

    [Parameter(Mandatory = $false)]
    [System.Security.SecureString]$PfxPassword = $null
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CertificateExportPaths {
    <#
    .SYNOPSIS Returns the PFX and CER export paths for the MSIX development certificate.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        'PSUseSingularNouns',
        '',
        Justification = 'Task [P3-T9] requires the helper name Get-CertificateExportPaths.'
    )]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDir
    )

    return [pscustomobject]@{
        PfxPath = Join-Path $OutputDir 'OpenClaw.MailBridge.pfx'
        CerPath = Join-Path $OutputDir 'OpenClaw.MailBridge.cer'
    }
}

function New-SigningCertificate {
    <#
    .SYNOPSIS Creates a self-signed code-signing certificate in the CurrentUser store.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Subject,

        [string]$CertStoreLocation = 'Cert:\CurrentUser\My'
    )
    # Create a self-signed code-signing certificate valid for 5 years
    if ($PSCmdlet.ShouldProcess($Subject, 'New-SelfSignedCertificate')) {
        $cert = New-SelfSignedCertificate `
            -Subject $Subject `
            -CertStoreLocation $CertStoreLocation `
            -Type CodeSigningCert `
            -KeyUsage DigitalSignature `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(5)
        return $cert
    }
    return $null
}

function Export-SigningCertificate {
    <#
    .SYNOPSIS Exports a certificate as PFX and CER files to the specified directory.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Cert,

        [Parameter(Mandatory = $true)]
        [string]$OutputDir,

        [Parameter(Mandatory = $true)]
        [System.Security.SecureString]$PfxPassword
    )
    $exportPaths = Get-CertificateExportPaths -OutputDir $OutputDir

    # Export the certificate with its private key as a PFX file
    Export-PfxCertificate -Cert $Cert -FilePath $exportPaths.PfxPath -Password $PfxPassword | Out-Null
    Write-Verbose "Exported PFX to $($exportPaths.PfxPath)"

    # Export the public certificate as CER for trusted-root installation
    Export-Certificate -Cert $Cert -FilePath $exportPaths.CerPath -Type CERT | Out-Null
    Write-Verbose "Exported CER to $($exportPaths.CerPath)"

    return $exportPaths
}

function Install-TrustedRootCertificate {
    <#
    .SYNOPSIS Installs a CER file into the LocalMachine\Root trusted-root store.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CerPath
    )
    # Install the public cert into LocalMachine\Root to allow MSIX sideloading
    $rootStore = 'Cert:\LocalMachine\Root'
    Import-Certificate -FilePath $CerPath -CertStoreLocation $rootStore | Out-Null
    Write-Verbose "Installed certificate into $rootStore"
}

# --- Main (only runs when executed directly, not when dot-sourced for testing) ---
if ($MyInvocation.InvocationName -ne '.') {
    Write-Information "Creating MSIX dev signing certificate: $Subject" -InformationAction Continue

    # Verify the session is running as Administrator (required for LocalMachine\Root import)
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Error 'New-MsixDevCert.ps1 must be run in an elevated (Run as Administrator) session because it imports into Cert:\LocalMachine\Root.'
        return
    }

    if (-not (Test-Path $OutputDir)) {
        $null = New-Item -ItemType Directory -Force -Path $OutputDir
    }

    if ($PSCmdlet.ShouldProcess($Subject, 'Create self-signed code-signing certificate')) {
        # Validate the PFX password at runtime (cannot be mandatory due to dot-source testability)
        if ($null -eq $PfxPassword) {
            Write-Error '-PfxPassword is required when running New-MsixDevCert.ps1 directly.'
            return
        }

        $cert = New-SigningCertificate -Subject $Subject

        $exported = Export-SigningCertificate -Cert $cert -OutputDir $OutputDir -PfxPassword $PfxPassword

        Install-TrustedRootCertificate -CerPath $exported.CerPath

        Write-Information "Certificate thumbprint: $($cert.Thumbprint)" -InformationAction Continue
        Write-Information "PFX: $($exported.PfxPath)" -InformationAction Continue
        Write-Information "CER: $($exported.CerPath)" -InformationAction Continue

        # Return thumbprint for use in build pipeline
        return $cert.Thumbprint
    }
}
