#Requires -Version 7
<#
.SYNOPSIS
Pester v5 tests for the OpenClawRbac module manifest and export surface.

.DESCRIPTION
Asserts that the manifest imports without ExchangeOnlineManagement being
imported, that the exported function set exactly matches the manifest's
FunctionsToExport, and that no production file in the module directory
declares a parse-time Exchange dependency.
#>

BeforeAll {
    $script:moduleDirectory = Resolve-Path -Path (
        Join-Path -Path $PSScriptRoot -ChildPath '..\..\..\..\..\scripts\powershell\modules\OpenClawRbac'
    )
    $script:manifestPath = Join-Path -Path $script:moduleDirectory -ChildPath 'OpenClawRbac.psd1'
    Import-Module $script:manifestPath -Force
}

AfterAll {
    Remove-Module -Name OpenClawRbac -Force -ErrorAction SilentlyContinue
}

Describe 'OpenClawRbac module manifest' {
    It 'imports without error and without importing ExchangeOnlineManagement' {
        # Arrange / Act: import happened in BeforeAll; gather resulting state.
        $module = Get-Module -Name OpenClawRbac
        $exchangeModule = Get-Module -Name ExchangeOnlineManagement

        # Assert: module loaded; the Exchange module was not pulled in at import time.
        $module | Should -Not -BeNullOrEmpty
        $exchangeModule | Should -BeNullOrEmpty
    }

    It 'exports exactly the function set declared in FunctionsToExport' {
        # Arrange: read the declared export list straight from the manifest data.
        $manifestData = Import-PowerShellDataFile -Path $script:manifestPath
        $declaredExports = @($manifestData.FunctionsToExport) | Sort-Object

        # Act: capture the actually exported functions.
        $module = Get-Module -Name OpenClawRbac
        $actualExports = @($module.ExportedFunctions.Keys) | Sort-Object

        # Assert: exact match, both directions.
        $actualExports | Should -Be $declaredExports
    }
}

Describe 'OpenClawRbac parse-time dependency rules' {
    It 'contains no parse-time Import-Module of ExchangeOnlineManagement and no #Requires -Modules directive' {
        # Arrange: enumerate every production file in the module directory.
        $moduleFiles = Get-ChildItem -Path $script:moduleDirectory -File -Recurse

        # Act: scan each file for the forbidden parse-time dependency markers.
        $importPattern = 'Import-Module\s+ExchangeOnlineManagement'
        $requiresPattern = '#Requires\s+-Modules'
        $offendingFiles = @(
            foreach ($moduleFile in $moduleFiles) {
                $content = Get-Content -Path $moduleFile.FullName -Raw
                if ($content -match $importPattern -or $content -match $requiresPattern) {
                    $moduleFile.FullName
                }
            }
        )

        # Assert: no file declares a parse-time Exchange dependency.
        $offendingFiles | Should -BeNullOrEmpty
    }
}
