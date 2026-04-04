param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$project = Join-Path $PSScriptRoot '..\src\OpenClaw.MailBridge\OpenClaw.MailBridge.csproj'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project $project --configuration $Configuration
