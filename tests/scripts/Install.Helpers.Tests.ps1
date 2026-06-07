#Requires -Version 7.0
<#
.SYNOPSIS
    Pester v5 tests for scripts/Install.Helpers.psm1.

.DESCRIPTION
    Tests every exported helper using mocks and function shims so no real
    filesystem, SDK, MSIX, or docker interaction is required. Inter-test state
    for the docker shim is held in $global: variables because function shims
    defined with `function global:docker { ... }` execute in global scope and
    cannot reach script-scoped variables. No temporary files are created.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidGlobalVars', '',
    Justification = 'Global docker shim functions run in global scope and must share state with the test via $global: variables.'
)]
param()

Describe 'Install.Helpers.psm1' {

    BeforeAll {
        $script:ModulePath = Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1'
        Import-Module $script:ModulePath -Force
    }

    AfterAll {
        Remove-Module Install.Helpers -ErrorAction SilentlyContinue
    }

    Context 'scripts/Install.Helpers.psm1 - export surface' {
        It 'exports exactly the helpers currently implemented (progressive batches)' {
            $exported = Get-Command -Module Install.Helpers | Select-Object -ExpandProperty Name | Sort-Object
            $expected = @(
                'Get-ManifestVersion',
                'Test-ManifestIntegrity',
                'Copy-BundleContents',
                'Initialize-DotEnv',
                'Invoke-MsixInstall',
                'Invoke-MsixCapture',
                'Invoke-MsixRemove',
                'Invoke-MsixAppActivate',
                'Test-DockerAvailable',
                'Invoke-ComposeUp',
                'Wait-ComposeHealthy',
                'Invoke-ComposeDown',
                'Write-InstallRecord',
                'Read-InstallRecord',
                'Get-ListeningProcessId',
                'Get-ProcessMainModulePath',
                'Invoke-HostAdapterStatusRequest'
            ) | Sort-Object
            $exported | Should -Be $expected
        }
    }

    Context 'Get-ManifestVersion' {
        BeforeEach {
            $script:Bundle = 'C:\bundle'
            $script:ManifestPath = Join-Path $script:Bundle 'manifest.json'
            Mock -ModuleName Install.Helpers Test-Path { $true }
        }
        It 'returns the top-level version string when manifest.json has a valid 4-part version' {
            $m = [pscustomobject]@{ version = '1.2.3.0'; files = @() }
            Mock -ModuleName Install.Helpers Get-Content { $m | ConvertTo-Json -Depth 5 }
            $v = Get-ManifestVersion -BundleRoot $script:Bundle
            $v | Should -Be '1.2.3.0'
        }
        It 'throws with the bundle root in the message when manifest.json is absent' {
            Mock -ModuleName Install.Helpers Test-Path { $false }
            { Get-ManifestVersion -BundleRoot 'C:\missing' } |
                Should -Throw -ExpectedMessage "*'C:\missing\manifest.json'*"
        }
        It 'throws a schema-violation message when manifest.json lacks the version field' {
            $m = [pscustomobject]@{ files = @() }
            Mock -ModuleName Install.Helpers Get-Content { $m | ConvertTo-Json -Depth 5 }
            { Get-ManifestVersion -BundleRoot $script:Bundle } |
                Should -Throw -ExpectedMessage "*missing the top-level 'version' field*"
        }
        It 'throws a parse-failure message when the version is unparseable' {
            $m = [pscustomobject]@{ version = 'not-a-version'; files = @() }
            Mock -ModuleName Install.Helpers Get-Content { $m | ConvertTo-Json -Depth 5 }
            { Get-ManifestVersion -BundleRoot $script:Bundle } |
                Should -Throw -ExpectedMessage "*unparseable 'version' value*"
        }
    }

    Context 'Test-ManifestIntegrity' {
        BeforeEach {
            $script:Bundle = 'C:\bundle'
            $script:ManifestPath = Join-Path $script:Bundle 'manifest.json'
            $script:ManifestObj = [pscustomobject]@{
                version = '1.2.3.0'
                files   = @(
                    [pscustomobject]@{ path = 'executables/foo.exe'; size = 100; sha256 = 'a' * 64 },
                    [pscustomobject]@{ path = 'docker/docker-compose.yml'; size = 200; sha256 = 'b' * 64 }
                )
            }
            Mock -ModuleName Install.Helpers Test-Path { $true }
            Mock -ModuleName Install.Helpers Get-Content { $script:ManifestObj | ConvertTo-Json -Depth 5 }
            Mock -ModuleName Install.Helpers Get-Item {
                param($LiteralPath)
                [pscustomobject]@{ Length = if ($LiteralPath -like '*foo.exe') { 100 } else { 200 } }
            }
            Mock -ModuleName Install.Helpers Get-FileHash {
                param($LiteralPath)
                $hash = if ($LiteralPath -like '*foo.exe') { 'a' * 64 } else { 'b' * 64 }
                [pscustomobject]@{ Hash = $hash.ToUpperInvariant() }
            }
            Mock -ModuleName Install.Helpers Get-ChildItem {
                @(
                    [pscustomobject]@{ FullName = (Join-Path $script:Bundle 'executables\foo.exe') },
                    [pscustomobject]@{ FullName = (Join-Path $script:Bundle 'docker\docker-compose.yml') },
                    [pscustomobject]@{ FullName = $script:ManifestPath }
                )
            }
        }

        It 'passes silently when every manifest entry matches disk' {
            { Test-ManifestIntegrity -BundleRoot $script:Bundle } | Should -Not -Throw
        }

        It 'throws a single terminating error listing every hash mismatch' {
            Mock -ModuleName Install.Helpers Get-FileHash { [pscustomobject]@{ Hash = ('c' * 64).ToUpperInvariant() } }
            $thrown = { Test-ManifestIntegrity -BundleRoot $script:Bundle } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'SHA-256 mismatch.*foo.exe'
            $thrown.Exception.Message | Should -Match 'SHA-256 mismatch.*docker-compose.yml'
        }

        It 'throws when an on-disk file under the bundle root is absent from the manifest' {
            Mock -ModuleName Install.Helpers Get-ChildItem {
                @(
                    [pscustomobject]@{ FullName = (Join-Path $script:Bundle 'executables\foo.exe') },
                    [pscustomobject]@{ FullName = (Join-Path $script:Bundle 'docker\docker-compose.yml') },
                    [pscustomobject]@{ FullName = (Join-Path $script:Bundle 'extras\orphan.txt') },
                    [pscustomobject]@{ FullName = $script:ManifestPath }
                )
            }
            $thrown = { Test-ManifestIntegrity -BundleRoot $script:Bundle } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'Unlisted file on disk.*orphan.txt'
        }

        It 'throws when a manifest entry points to a missing file' {
            Mock -ModuleName Install.Helpers Test-Path {
                param($LiteralPath)
                $LiteralPath -eq $script:ManifestPath
            }
            Mock -ModuleName Install.Helpers Get-ChildItem {
                @([pscustomobject]@{ FullName = $script:ManifestPath })
            }
            $thrown = { Test-ManifestIntegrity -BundleRoot $script:Bundle } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'Missing file.*foo.exe'
        }

        It 'throws when manifest lacks the top-level version field' {
            $bad = [pscustomobject]@{ files = @() }
            Mock -ModuleName Install.Helpers Get-Content { $bad | ConvertTo-Json -Depth 5 }
            { Test-ManifestIntegrity -BundleRoot $script:Bundle } |
                Should -Throw -ExpectedMessage '*does not conform to the expected { version, files } schema*'
        }
    }

    Context 'Copy-BundleContents' {
        BeforeEach {
            Mock -ModuleName Install.Helpers New-Item { }
            Mock -ModuleName Install.Helpers Copy-Item { }
        }

        It 'creates the destination executables/ and docker/ subdirectories via New-Item' {
            Copy-BundleContents -SourceBundleRoot 'C:\bundle' -DestinationRoot 'C:\dest'
            Should -Invoke -ModuleName Install.Helpers -CommandName New-Item -Times 2 -Exactly
        }

        It 'invokes Copy-Item with -Recurse for each subtree' {
            Copy-BundleContents -SourceBundleRoot 'C:\bundle' -DestinationRoot 'C:\dest'
            Should -Invoke -ModuleName Install.Helpers -CommandName Copy-Item -Times 2 -Exactly -ParameterFilter { $Recurse -eq $true }
        }

        It '-WhatIf produces no New-Item or Copy-Item calls' {
            Copy-BundleContents -SourceBundleRoot 'C:\bundle' -DestinationRoot 'C:\dest' -WhatIf
            Should -Invoke -ModuleName Install.Helpers -CommandName New-Item -Times 0 -Exactly
            Should -Invoke -ModuleName Install.Helpers -CommandName Copy-Item -Times 0 -Exactly
        }
    }

    Context 'Initialize-DotEnv' {
        BeforeEach { Mock -ModuleName Install.Helpers Copy-Item { } }

        It 'copies .env.example to .env when .env is absent' {
            Mock -ModuleName Install.Helpers Test-Path { $false }
            Initialize-DotEnv -DestDockerDir 'C:\dest\docker'
            Should -Invoke -ModuleName Install.Helpers -CommandName Copy-Item -Times 1 -Exactly
        }

        It 'does not invoke Copy-Item when .env already exists' {
            Mock -ModuleName Install.Helpers Test-Path { $true }
            Initialize-DotEnv -DestDockerDir 'C:\dest\docker'
            Should -Invoke -ModuleName Install.Helpers -CommandName Copy-Item -Times 0 -Exactly
        }

        It '-WhatIf produces no Copy-Item call' {
            Mock -ModuleName Install.Helpers Test-Path { $false }
            Initialize-DotEnv -DestDockerDir 'C:\dest\docker' -WhatIf
            Should -Invoke -ModuleName Install.Helpers -CommandName Copy-Item -Times 0 -Exactly
        }
    }

    Context 'Invoke-MsixInstall' {
        It 'calls Add-AppxPackage -Path <MsixPath> with no additional flags by default' {
            Mock -ModuleName Install.Helpers Add-AppxPackage { }
            Invoke-MsixInstall -MsixPath 'C:\pkg.msix'
            Should -Invoke -ModuleName Install.Helpers -CommandName Add-AppxPackage -Times 1 -Exactly -ParameterFilter { $Path -eq 'C:\pkg.msix' -and -not $AllowUnsigned }
        }

        It 'calls Add-AppxPackage -Path <MsixPath> -AllowUnsigned when switch is supplied' {
            Mock -ModuleName Install.Helpers Add-AppxPackage { }
            Invoke-MsixInstall -MsixPath 'C:\pkg.msix' -AllowUnsigned
            Should -Invoke -ModuleName Install.Helpers -CommandName Add-AppxPackage -Times 1 -Exactly -ParameterFilter { $Path -eq 'C:\pkg.msix' -and $AllowUnsigned -eq $true }
        }

        It 're-throws with a message referencing MSIX path when Add-AppxPackage fails' {
            Mock -ModuleName Install.Helpers Add-AppxPackage { throw 'signature trust validation failure' }
            $thrown = { Invoke-MsixInstall -MsixPath 'C:\pkg.msix' } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'C:\\pkg\.msix'
            $thrown.Exception.Message | Should -Match 'AllowUnsigned=False'
        }
    }

    Context 'Invoke-MsixCapture' {
        It 'returns the PackageFullName of the package returned by Get-AppxPackage' {
            Mock -ModuleName Install.Helpers Get-AppxPackage {
                [pscustomobject]@{ PackageFullName = 'OpenClaw.MailBridge_1.2.3.0_x64__abc' }
            }
            Invoke-MsixCapture | Should -Be 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
        }

        It 'throws with a descriptive message when Get-AppxPackage returns null' {
            Mock -ModuleName Install.Helpers Get-AppxPackage { $null }
            $thrown = { Invoke-MsixCapture } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'OpenClaw\.MailBridge'
        }
    }

    Context 'Invoke-MsixRemove' {
        BeforeEach { Mock -ModuleName Install.Helpers Remove-AppxPackage { } }

        It 'calls Remove-AppxPackage -Package <name> when the package is installed' {
            Mock -ModuleName Install.Helpers Get-AppxPackage { [pscustomobject]@{ PackageFullName = 'OpenClaw.MailBridge_1.2.3.0_x64__abc' } }
            Invoke-MsixRemove -PackageFullName 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
            Should -Invoke -ModuleName Install.Helpers -CommandName Remove-AppxPackage -Times 1 -Exactly -ParameterFilter { $Package -eq 'OpenClaw.MailBridge_1.2.3.0_x64__abc' }
        }

        It 'returns silently when Get-AppxPackage returns null' {
            Mock -ModuleName Install.Helpers Get-AppxPackage { $null }
            Invoke-MsixRemove -PackageFullName 'anything'
            Should -Invoke -ModuleName Install.Helpers -CommandName Remove-AppxPackage -Times 0 -Exactly
        }

        It 'uses the installed package full name when the supplied package full name is empty' {
            Mock -ModuleName Install.Helpers Get-AppxPackage { [pscustomobject]@{ PackageFullName = 'OpenClaw.MailBridge_1.2.3.0_x64__abc' } }
            Invoke-MsixRemove -PackageFullName ''
            Should -Invoke -ModuleName Install.Helpers -CommandName Remove-AppxPackage -Times 1 -Exactly -ParameterFilter { $Package -eq 'OpenClaw.MailBridge_1.2.3.0_x64__abc' }
        }

        It '-WhatIf produces no Remove-AppxPackage call' {
            Mock -ModuleName Install.Helpers Get-AppxPackage { [pscustomobject]@{ PackageFullName = 'x' } }
            Invoke-MsixRemove -PackageFullName 'x' -WhatIf
            Should -Invoke -ModuleName Install.Helpers -CommandName Remove-AppxPackage -Times 0 -Exactly
        }
    }

    Context 'Invoke-MsixAppActivate' {
        BeforeEach { Mock -ModuleName Install.Helpers Start-Process { } }

        It 'invokes Start-Process with the supplied ActivationUri under ShouldProcess' {
            Invoke-MsixAppActivate -ActivationUri 'openclaw-mailbridge:firstrun'
            Should -Invoke -ModuleName Install.Helpers -CommandName Start-Process -Times 1 -Exactly -ParameterFilter {
                $FilePath -eq 'openclaw-mailbridge:firstrun'
            }
        }

        It 'is a no-op when ShouldProcess returns false (e.g., -WhatIf)' {
            Invoke-MsixAppActivate -ActivationUri 'openclaw-mailbridge:firstrun' -WhatIf
            Should -Invoke -ModuleName Install.Helpers -CommandName Start-Process -Times 0 -Exactly
        }
    }

    Context 'Write-InstallRecord' {
        It 'calls Set-Content with the serialized JSON at the supplied path' {
            $record = [pscustomobject]@{ version = '1.2.3.0'; destinationPath = 'C:\x' }
            Mock -ModuleName Install.Helpers Test-Path { $true }
            Mock -ModuleName Install.Helpers New-Item { }
            Mock -ModuleName Install.Helpers Set-Content { }

            Write-InstallRecord -Record $record -RecordPath 'C:\AppData\OpenClaw\install-record.json'

            Should -Invoke -ModuleName Install.Helpers -CommandName Set-Content -Times 1 -Exactly -ParameterFilter {
                $LiteralPath -eq 'C:\AppData\OpenClaw\install-record.json' -and $Value -match '"version"' -and $Value -match '"1.2.3.0"'
            }
        }

        It 'ensures the parent directory is created when absent' {
            Mock -ModuleName Install.Helpers Test-Path { $false }
            Mock -ModuleName Install.Helpers New-Item { }
            Mock -ModuleName Install.Helpers Set-Content { }

            Write-InstallRecord -Record ([pscustomobject]@{}) -RecordPath 'C:\AppData\OpenClaw\install-record.json'

            Should -Invoke -ModuleName Install.Helpers -CommandName New-Item -Times 1 -Exactly -ParameterFilter {
                $Path -eq 'C:\AppData\OpenClaw'
            }
        }

        It '-WhatIf produces no Set-Content call' {
            Mock -ModuleName Install.Helpers Test-Path { $true }
            Mock -ModuleName Install.Helpers Set-Content { }

            Write-InstallRecord -Record ([pscustomobject]@{}) -RecordPath 'C:\x\install-record.json' -WhatIf

            Should -Invoke -ModuleName Install.Helpers -CommandName Set-Content -Times 0 -Exactly
        }
    }

    Context 'Read-InstallRecord' {
        It 'returns a parsed pscustomobject when the file exists' {
            Mock -ModuleName Install.Helpers Test-Path { $true }
            Mock -ModuleName Install.Helpers Get-Content {
                '{ "version": "1.2.3.0", "destinationPath": "C:\\x" }'
            }

            $r = Read-InstallRecord -RecordPath 'C:\x\install-record.json'
            $r.version | Should -Be '1.2.3.0'
            $r.destinationPath | Should -Be 'C:\x'
        }

        It 'throws a specific "no prior install recorded" message when the file is absent' {
            Mock -ModuleName Install.Helpers Test-Path { $false }

            $thrown = { Read-InstallRecord -RecordPath 'C:\x\install-record.json' } |
                Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'No prior install recorded'
        }

        It 'returned object exposes the documented schema fields' {
            Mock -ModuleName Install.Helpers Test-Path { $true }
            Mock -ModuleName Install.Helpers Get-Content {
                @{
                    version            = '1.2.3.0'
                    destinationPath    = 'C:\x'
                    packageFullName    = 'OpenClaw.MailBridge_1.2.3.0_x64__abc'
                    composeProjectName = 'openclaw'
                    composeFilePath    = 'C:\x\docker\docker-compose.yml'
                    skipDocker         = $false
                    allowUnsigned      = $false
                } | ConvertTo-Json
            }

            $r = Read-InstallRecord -RecordPath 'C:\x\install-record.json'
            $r.PSObject.Properties.Name | Should -Contain 'version'
            $r.PSObject.Properties.Name | Should -Contain 'destinationPath'
            $r.PSObject.Properties.Name | Should -Contain 'packageFullName'
            $r.PSObject.Properties.Name | Should -Contain 'composeProjectName'
            $r.PSObject.Properties.Name | Should -Contain 'composeFilePath'
            $r.PSObject.Properties.Name | Should -Contain 'skipDocker'
            $r.PSObject.Properties.Name | Should -Contain 'allowUnsigned'
        }
    }
}

Describe 'Get-ProcessMainModulePath defensive branch' {
    BeforeAll { Import-Module (Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1') -Force }
    It 'returns $null and does not throw when Get-Process throws Win32Exception (access denied)' {
        Mock Get-Process -ModuleName Install.Helpers -MockWith { throw [System.ComponentModel.Win32Exception]::new(5, 'Access is denied') } -ParameterFilter { $Id -eq 99001 -and $ErrorAction -eq 'Stop' }
        Get-ProcessMainModulePath -ProcessId 99001 | Should -BeNullOrEmpty
    }
    It 'returns $null when Get-Process returns a process whose MainModule is null' {
        Mock Get-Process -ModuleName Install.Helpers -MockWith { [pscustomobject]@{ Id = 99002; MainModule = $null } } -ParameterFilter { $Id -eq 99002 }
        Get-ProcessMainModulePath -ProcessId 99002 | Should -BeNullOrEmpty
    }
}

Describe 'Get-ListeningProcessId no-listener path' {
    BeforeAll { Import-Module (Join-Path $PSScriptRoot '..\..\scripts\Install.Helpers.psm1') -Force }
    It 'returns $null when Get-NetTCPConnection yields an empty pipeline' {
        Mock Get-NetTCPConnection -ModuleName Install.Helpers -MockWith { } -ParameterFilter { $LocalPort -eq 14319 -and $State -eq 'Listen' }
        Get-ListeningProcessId -Port 14319 | Should -BeNullOrEmpty
    }
    It 'returns the OwningProcess as [int] when a single listener is present' {
        Mock Get-NetTCPConnection -ModuleName Install.Helpers -MockWith { [pscustomobject]@{ OwningProcess = 4321 } } -ParameterFilter { $LocalPort -eq 14320 -and $State -eq 'Listen' }
        Get-ListeningProcessId -Port 14320 | Should -Be 4321
    }
}
