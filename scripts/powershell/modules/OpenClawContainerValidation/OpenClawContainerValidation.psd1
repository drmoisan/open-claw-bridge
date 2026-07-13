@{
    RootModule        = 'OpenClawContainerValidation.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'f4a9c0d1-7a2c-4c9b-8ed6-6a4d2f0b91a2'
    Author            = 'OpenClaw MailBridge'
    CompanyName       = 'OpenClaw'
    Copyright         = '(c) OpenClaw. All rights reserved.'
    Description       = 'Shared helpers for scripts/Invoke-OpenClawContainerPathValidation.ps1 (issue #38).'
    PowerShellVersion = '7.0'
    FunctionsToExport = @(
        'Get-OpenClawEndpointUri',
        'Get-OpenClawPropertyValue',
        'Get-OpenClawContentPreview',
        'ConvertFrom-OpenClawJsonContent',
        'Invoke-OpenClawEndpointRequest',
        'Get-OpenClawValidationResult',
        'Invoke-OpenClawDockerCommand',
        'Get-OpenClawEnvFileMap',
        'Get-OpenClawOperatorEnvFilePath',
        'Resolve-OpenClawDefaultEnvFilePath',
        'Invoke-OpenClawReadyzProbe',
        'Invoke-OpenClawHostAdapterInContainerProbe',
        'Test-OpenClawGatewayTokenPresence',
        'Test-OpenClawGatewayTokenInContainer',
        'ConvertFrom-OpenClawImageReference',
        'Test-OpenClawImageVersionAligned'
    )
    CmdletsToExport   = @()
    AliasesToExport   = @()
    VariablesToExport = @()
}
