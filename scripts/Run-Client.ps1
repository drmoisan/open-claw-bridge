param(
    [string]$PipeName = 'openclaw-mail-bridge',
    [string]$Message = 'ping',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$project = Join-Path $PSScriptRoot '..\src\OpenClaw.MailBridge.Client\OpenClaw.MailBridge.Client.csproj'
dotnet run --project $project --configuration $Configuration -- --pipe-name $PipeName --message $Message
