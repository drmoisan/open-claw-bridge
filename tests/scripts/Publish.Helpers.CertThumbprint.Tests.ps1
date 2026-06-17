#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for Resolve-CertThumbprint in scripts/Publish.Helpers.psm1.

.DESCRIPTION
    Extracted from tests/scripts/Publish.Helpers.Tests.ps1 (which exceeded the
    500-line cap) so the cert-thumbprint precedence cases can be extended without
    growing that file past the cap. All cases mock the Invoke-DotnetExe WRAPPER
    (never the dotnet executable directly), with a mock signature matching
    param([string[]]$DotnetArgs). No real dotnet is invoked and no temp files are
    created.
#>

Describe 'Publish.Helpers.psm1 Resolve-CertThumbprint' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Publish.Helpers.psm1'
        Import-Module $script:ModulePath -Force
    }

    Context 'Resolve-CertThumbprint base precedence (explicit / user secret / process env)' {
        It 'explicit thumbprint wins over user secret and env' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = SECRETVALUE'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint 'EXPLICITVALUE' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'EXPLICITVALUE'
            Assert-MockCalled -ModuleName Publish.Helpers Invoke-DotnetExe -Times 0 -Scope It
        }
        It 'returns the user-secret value when explicit is empty' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                @('Some preamble line', 'Signing:CertThumbprint = ABC123DEF456', 'Trailing line')
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'ABC123DEF456'
        }
        It 'returns the injected env value when explicit and user secret are absent' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'No secrets configured for this application.'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'ENVVALUE'
        }
        It 'returns empty string when all sources are absent' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'No secrets configured for this application.'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint ''
            $r | Should -Be ''
        }
        It 'whitespace-only explicit value does not win; falls through to user secret' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = FALLTHROUGH'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '   ' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint ''
            $r | Should -Be 'FALLTHROUGH'
        }
        It 'skips the user-secret lookup when ProjectPath is empty' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = SHOULDNOTBEUSED'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -ProjectPath '' -EnvThumbprint 'ENVONLY'
            $r | Should -Be 'ENVONLY'
            Assert-MockCalled -ModuleName Publish.Helpers Invoke-DotnetExe -Times 0 -Scope It
        }
    }

    Context 'Resolve-CertThumbprint .env precedence (D7, AC-4)' {
        It 'explicit -CertThumbprint wins over the .env value' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = SECRETVALUE'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint 'EXPLICIT' -DotEnvThumbprint 'DOTENVVALUE' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'EXPLICIT'
            Assert-MockCalled -ModuleName Publish.Helpers Invoke-DotnetExe -Times 0 -Scope It
        }
        It '.env OPENCLAW_CERT_THUMBPRINT beats the dotnet user secret' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = SECRETVALUE'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -DotEnvThumbprint 'DOTENVVALUE' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'DOTENVVALUE'
            Assert-MockCalled -ModuleName Publish.Helpers Invoke-DotnetExe -Times 0 -Scope It
        }
        It '.env OPENCLAW_CERT_THUMBPRINT beats the process-env -EnvThumbprint' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'No secrets configured for this application.'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -DotEnvThumbprint 'DOTENVVALUE' -ProjectPath '' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'DOTENVVALUE'
        }
        It 'falls through to the user secret when the .env value is empty' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = SECRETVALUE'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -DotEnvThumbprint '' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint 'ENVVALUE'
            $r | Should -Be 'SECRETVALUE'
        }
        It 'whitespace-only .env value does not win; falls through to user secret' {
            Mock -ModuleName Publish.Helpers Invoke-DotnetExe {
                param([string[]]$DotnetArgs)
                $null = $DotnetArgs
                'Signing:CertThumbprint = SECRETVALUE'
            }
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -DotEnvThumbprint '   ' -ProjectPath 'C:\fake\proj.csproj' -EnvThumbprint ''
            $r | Should -Be 'SECRETVALUE'
        }
        It 'trims surrounding whitespace from the .env value' {
            $r = Resolve-CertThumbprint -ExplicitThumbprint '' -DotEnvThumbprint '  DEAD00BEEF  ' -ProjectPath '' -EnvThumbprint ''
            $r | Should -Be 'DEAD00BEEF'
        }
    }
}
