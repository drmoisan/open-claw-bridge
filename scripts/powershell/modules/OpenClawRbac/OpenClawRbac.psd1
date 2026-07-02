@{
    RootModule        = 'OpenClawRbac.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'b7c31a9e-52d4-4f0a-9c6d-8e2a41d7f0b3'
    Author            = 'OpenClaw MailBridge'
    CompanyName       = 'OpenClaw'
    Copyright         = '(c) OpenClaw. All rights reserved.'
    Description       = 'Exchange Online Application RBAC setup functions for the OpenClaw assistant (issue #111). Exchange cmdlets are resolved at runtime; ExchangeOnlineManagement is not a parse-time dependency.'
    PowerShellVersion = '7.0'
    FunctionsToExport = @(
        'Register-OpenClawServicePrincipal',
        'New-OpenClawMailboxScope',
        'Grant-OpenClawRbacRoles',
        'Set-OpenClawSendOnBehalf',
        'Test-OpenClawScopeBoundary',
        'Invoke-OpenClawNewServicePrincipal',
        'Invoke-OpenClawGetServicePrincipal',
        'Invoke-OpenClawNewManagementScope',
        'Invoke-OpenClawGetManagementScope',
        'Invoke-OpenClawNewManagementRoleAssignment',
        'Invoke-OpenClawGetManagementRoleAssignment',
        'Invoke-OpenClawAddMailboxPermission',
        'Invoke-OpenClawSetMailbox',
        'Invoke-OpenClawTestServicePrincipalAuthorization'
    )
    CmdletsToExport   = @()
    AliasesToExport   = @()
    VariablesToExport = @()
}
