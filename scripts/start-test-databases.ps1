#Requires -Version 5.1
<#
.SYNOPSIS
    Starts SQL Server, PostgreSQL, and MySQL containers for local test runs.

.DESCRIPTION
    Uses docker-compose.test.yml in the repository root.
    Containers expose ports 1433, 5432, and 3306 (see testsettings.docker.json).

.EXAMPLE
    .\scripts\start-test-databases.ps1
#>
[CmdletBinding()]
param(
    [switch]$Detach = $true
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$composeFile = Join-Path $repoRoot 'docker-compose.test.yml'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is not installed or not on PATH. Install Docker Desktop and ensure it is running.'
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker daemon is not running. Start Docker Desktop and retry.'
}

Write-Host "Starting test databases from $composeFile ..." -ForegroundColor Cyan

$composeArgs = @('compose', '-f', $composeFile, 'up')
if ($Detach) {
    $composeArgs += '-d', '--wait'
}
else {
    $composeArgs += '--wait'
}

& docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "docker compose failed with exit code $LASTEXITCODE"
}

Write-Host ''
Write-Host 'Test databases are ready.' -ForegroundColor Green
Write-Host 'Run tests:  .\scripts\run-tests-local.ps1'
Write-Host 'Stop:       .\scripts\stop-test-databases.ps1'
