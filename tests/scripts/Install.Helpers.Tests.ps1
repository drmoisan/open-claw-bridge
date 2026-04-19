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
                'Find-NewestPublishVersion',
                'Test-ManifestIntegrity',
                'Copy-BundleContents',
                'Initialize-DotEnv',
                'Invoke-MsixInstall',
                'Invoke-MsixCapture',
                'Invoke-MsixRemove',
                'Test-DockerAvailable',
                'Invoke-ComposeUp',
                'Wait-ComposeHealthy',
                'Invoke-ComposeDown',
                'Write-InstallRecord',
                'Read-InstallRecord'
            ) | Sort-Object
            $exported | Should -Be $expected
        }
    }

    Context 'Find-NewestPublishVersion' {
        It 'returns the highest-version directory when multiple parseable names exist' {
            Mock -ModuleName Install.Helpers Get-ChildItem {
                @(
                    [pscustomobject]@{ Name = '1.0.0.0'; FullName = 'C:\pub\1.0.0.0'; PSIsContainer = $true },
                    [pscustomobject]@{ Name = '1.2.3.0'; FullName = 'C:\pub\1.2.3.0'; PSIsContainer = $true },
                    [pscustomobject]@{ Name = '0.0.0.1'; FullName = 'C:\pub\0.0.0.1'; PSIsContainer = $true }
                )
            }
            $result = Find-NewestPublishVersion -PublishRoot 'C:\pub'
            $result.Version.ToString() | Should -Be '1.2.3.0'
            $result.Path | Should -Be 'C:\pub\1.2.3.0'
        }

        It 'filters out non-parseable directory names (bridge, client)' {
            Mock -ModuleName Install.Helpers Get-ChildItem {
                @(
                    [pscustomobject]@{ Name = 'bridge'; FullName = 'C:\pub\bridge'; PSIsContainer = $true },
                    [pscustomobject]@{ Name = 'client'; FullName = 'C:\pub\client'; PSIsContainer = $true },
                    [pscustomobject]@{ Name = '1.0.0.1'; FullName = 'C:\pub\1.0.0.1'; PSIsContainer = $true }
                )
            }
            $result = Find-NewestPublishVersion -PublishRoot 'C:\pub'
            $result.Version.ToString() | Should -Be '1.0.0.1'
        }

        It 'throws with a message containing the publish root when no parseable directory exists' {
            Mock -ModuleName Install.Helpers Get-ChildItem {
                @(
                    [pscustomobject]@{ Name = 'bridge'; FullName = 'C:\pub\bridge'; PSIsContainer = $true },
                    [pscustomobject]@{ Name = 'client'; FullName = 'C:\pub\client'; PSIsContainer = $true }
                )
            }
            { Find-NewestPublishVersion -PublishRoot 'C:\pub' } |
                Should -Throw -ExpectedMessage "*'C:\pub'*"
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

        It '-WhatIf produces no Remove-AppxPackage call' {
            Mock -ModuleName Install.Helpers Get-AppxPackage { [pscustomobject]@{ PackageFullName = 'x' } }
            Invoke-MsixRemove -PackageFullName 'x' -WhatIf
            Should -Invoke -ModuleName Install.Helpers -CommandName Remove-AppxPackage -Times 0 -Exactly
        }
    }

    Context 'Test-DockerAvailable' {
        BeforeEach {
            # Define a docker shim in the global scope; individual It blocks
            # override it to set the desired exit code.
            function global:docker { $global:LASTEXITCODE = 0 }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
        }

        It 'returns $true when the docker shim sets $LASTEXITCODE = 0' {
            function global:docker { $global:LASTEXITCODE = 0 }
            Test-DockerAvailable | Should -BeTrue
        }

        It 'throws with a remediation message containing -SkipDocker when the shim sets $LASTEXITCODE = 1' {
            function global:docker { $global:LASTEXITCODE = 1 }
            $thrown = { Test-DockerAvailable } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match '-SkipDocker'
        }
    }

    Context 'Invoke-ComposeUp' {
        BeforeEach {
            $global:lastDockerArgs = $null
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 0 }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
            Remove-Variable -Name lastDockerArgs -Scope Global -ErrorAction SilentlyContinue
        }

        It 'docker shim receives compose up -d with explicit flags verbatim' {
            Invoke-ComposeUp -DestDockerDir 'C:\dest\docker' -ComposeFilePath 'C:\dest\docker\docker-compose.yml'
            $expected = @('compose', '--project-name', 'openclaw', '--project-directory', 'C:\dest\docker', '-f', 'C:\dest\docker\docker-compose.yml', 'up', '-d', 'openclaw-core', 'openclaw-agent')
            ($global:lastDockerArgs -join '|') | Should -Be ($expected -join '|')
        }

        It 'throws on non-zero exit' {
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 1 }
            { Invoke-ComposeUp -DestDockerDir 'C:\dest\docker' -ComposeFilePath 'C:\dest\docker\dc.yml' } |
                Should -Throw -ExpectedMessage '*docker compose up failed*'
        }

        It '-WhatIf does not invoke the shim' {
            Invoke-ComposeUp -DestDockerDir 'C:\d' -ComposeFilePath 'C:\d\dc.yml' -WhatIf
            $global:lastDockerArgs | Should -BeNullOrEmpty
        }
    }

    Context 'Wait-ComposeHealthy' {
        BeforeEach {
            $global:dockerCallCount = 0
            $global:dockerResponses = @()
            function global:docker {
                $global:dockerCallCount++
                $global:LASTEXITCODE = 0
                $idx = [math]::Min($global:dockerCallCount - 1, $global:dockerResponses.Count - 1)
                $global:dockerResponses[$idx]
            }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
            Remove-Variable -Name dockerCallCount -Scope Global -ErrorAction SilentlyContinue
            Remove-Variable -Name dockerResponses -Scope Global -ErrorAction SilentlyContinue
        }

        It 'returns when both services report running + healthy on the first poll' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' },
                        [pscustomobject]@{ Service = 'openclaw-agent'; State = 'running'; Health = 'healthy' }
                    ) | ConvertTo-Json -Compress))
            { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 10 -PollIntervalSeconds 1 } | Should -Not -Throw
        }

        It 'throws with failing service name on timeout when a service never reports healthy' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' },
                        [pscustomobject]@{ Service = 'openclaw-agent'; State = 'starting'; Health = 'starting' }
                    ) | ConvertTo-Json -Compress))
            $thrown = { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 2 -PollIntervalSeconds 1 } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'openclaw-agent'
        }

        It 'accepts Health as null/empty when no healthcheck is defined' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = '' },
                        [pscustomobject]@{ Service = 'openclaw-agent'; State = 'running'; Health = $null }
                    ) | ConvertTo-Json -Compress))
            { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 10 -PollIntervalSeconds 1 } | Should -Not -Throw
        }

        It 'exposes default values -TimeoutSeconds 90 and -PollIntervalSeconds 3' {
            $ast = (Get-Command -Module Install.Helpers -Name Wait-ComposeHealthy).ScriptBlock.Ast
            $paramAst = $ast.Body.ParamBlock.Parameters
            ($paramAst | Where-Object { $_.Name.VariablePath.UserPath -eq 'TimeoutSeconds' }).DefaultValue.Extent.Text | Should -Be '90'
            ($paramAst | Where-Object { $_.Name.VariablePath.UserPath -eq 'PollIntervalSeconds' }).DefaultValue.Extent.Text | Should -Be '3'
        }

        It 'parses a stream of one JSON object per line' {
            $core = [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' } | ConvertTo-Json -Compress
            $agent = [pscustomobject]@{ Service = 'openclaw-agent'; State = 'running'; Health = 'healthy' } | ConvertTo-Json -Compress
            $global:dockerResponses = @($core + "`n" + $agent)
            { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 10 -PollIntervalSeconds 1 } | Should -Not -Throw
        }

        It 'treats malformed JSON as transient and retries until timeout' {
            $global:dockerResponses = @('NOT VALID JSON')
            $thrown = { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 2 -PollIntervalSeconds 1 } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'Timed out'
        }

        It 'reports absent-service when JSON omits a required service' {
            $global:dockerResponses = @((@(
                        [pscustomobject]@{ Service = 'openclaw-core'; State = 'running'; Health = 'healthy' }
                    ) | ConvertTo-Json -Compress -AsArray))
            $thrown = { Wait-ComposeHealthy -ComposeFilePath 'C:\x\dc.yml' -TimeoutSeconds 2 -PollIntervalSeconds 1 } | Should -Throw -PassThru
            $thrown.Exception.Message | Should -Match 'openclaw-agent'
        }
    }

    Context 'Invoke-ComposeDown' {
        BeforeEach {
            $global:lastDockerArgs = $null
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 0 }
        }
        AfterEach {
            Remove-Item -Path 'Function:\global:docker' -ErrorAction SilentlyContinue
            Remove-Variable -Name lastDockerArgs -Scope Global -ErrorAction SilentlyContinue
        }

        It 'docker shim receives compose --project-name <name> -f <file> down verbatim' {
            Invoke-ComposeDown -ComposeFilePath 'C:\x\dc.yml' -ProjectName 'openclaw'
            ($global:lastDockerArgs -join '|') | Should -Be (@('compose', '--project-name', 'openclaw', '-f', 'C:\x\dc.yml', 'down') -join '|')
        }

        It 'throws on non-zero exit' {
            function global:docker { $global:lastDockerArgs = $args; $global:LASTEXITCODE = 2 }
            { Invoke-ComposeDown -ComposeFilePath 'C:\x\dc.yml' } | Should -Throw -ExpectedMessage '*docker compose down failed*'
        }

        It '-WhatIf does not invoke the shim' {
            Invoke-ComposeDown -ComposeFilePath 'C:\x\dc.yml' -WhatIf
            $global:lastDockerArgs | Should -BeNullOrEmpty
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
