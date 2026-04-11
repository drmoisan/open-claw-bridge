[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$actionlint = Get-Command actionlint -ErrorAction SilentlyContinue
if ($null -eq $actionlint) {
    throw 'actionlint was not found on PATH.'
}

$workflowRoot = Join-Path $PSScriptRoot '..\..\.github\workflows'
if (-not (Test-Path -LiteralPath $workflowRoot)) {
    Write-Output "No workflow files found under $workflowRoot"
    exit 0
}

& $actionlint.Source -color
