#Requires -Version 5.1
<#
.SYNOPSIS
    Stops and removes test database containers.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$composeFile = Join-Path $repoRoot 'docker-compose.test.yml'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is not installed or not on PATH.'
}

Write-Host "Stopping test databases ..." -ForegroundColor Cyan
& docker compose -f $composeFile down
if ($LASTEXITCODE -ne 0) {
    throw "docker compose down failed with exit code $LASTEXITCODE"
}

Write-Host 'Test databases stopped.' -ForegroundColor Green
