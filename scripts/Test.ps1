param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$solution = Join-Path $PSScriptRoot '..\OpenClaw.MailBridge.sln'
dotnet test $solution -c $Configuration
