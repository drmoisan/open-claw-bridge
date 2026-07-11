BeforeDiscovery {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\..\scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

Describe 'OpenClawContainerValidation.psm1 (default EnvFilePath resolution)' {
    BeforeAll {
        $modulePath = Join-Path $PSScriptRoot '..\..\..\..\..\scripts\powershell\modules\OpenClawContainerValidation\OpenClawContainerValidation.psd1'
        Import-Module -Name $modulePath -Force -ErrorAction Stop
    }

    It 'Get-OpenClawOperatorEnvFilePath composes the version-neutral operator env path' {
        # Arrange / Act
        $result = Get-OpenClawOperatorEnvFilePath -LocalAppDataPath 'C:\Users\Test\AppData\Local'

        # Assert (path-separator-normalized comparison)
        ($result -replace '/', '\') | Should -Be 'C:\Users\Test\AppData\Local\OpenClaw\operator-config\.env'
    }

    It 'Get-OpenClawOperatorEnvFilePath returns $null for empty LocalAppData' {
        Get-OpenClawOperatorEnvFilePath -LocalAppDataPath '' | Should -BeNullOrEmpty
    }

    It 'Resolve-OpenClawDefaultEnvFilePath returns the operator path when it exists' {
        # Arrange
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true } -ParameterFilter { $LiteralPath -eq 'C:\op\.env' }

        # Act
        $result = Resolve-OpenClawDefaultEnvFilePath -OperatorEnvFilePath 'C:\op\.env' -FallbackEnvFilePath './.env'

        # Assert
        $result | Should -Be 'C:\op\.env'
    }

    It 'Resolve-OpenClawDefaultEnvFilePath falls back when the operator path does not exist' {
        # Arrange
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $false } -ParameterFilter { $LiteralPath -eq 'C:\op\.env' }

        # Act
        $result = Resolve-OpenClawDefaultEnvFilePath -OperatorEnvFilePath 'C:\op\.env' -FallbackEnvFilePath './.env'

        # Assert
        $result | Should -Be './.env'
    }

    It 'Resolve-OpenClawDefaultEnvFilePath returns the fallback without probing when the operator path is null' {
        # Arrange
        Mock -ModuleName OpenClawContainerValidation Test-Path { return $true }

        # Act
        $result = Resolve-OpenClawDefaultEnvFilePath -OperatorEnvFilePath $null -FallbackEnvFilePath './.env'

        # Assert
        $result | Should -Be './.env'
        Should -Invoke -ModuleName OpenClawContainerValidation Test-Path -Times 0
    }
}
