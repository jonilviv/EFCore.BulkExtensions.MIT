#Requires -Version 5.1
<#
.SYNOPSIS
    Runs EFCore.BulkExtensions.Tests against locally started database containers.

.DESCRIPTION
    Sets EFCORE_BULK_EXTENSIONS_USE_LOCAL_DB=1 and loads testsettings.docker.json.
    Start databases first: .\scripts\start-test-databases.ps1

    Without Docker (native SQL Server / PostgreSQL / MySQL on localhost):
      Copy testsettings.local.json.example to EFCore.BulkExtensions.Tests\testsettings.local.json,
      adjust connection strings, set $UseDocker = $false, and ensure servers are running.

.PARAMETER Framework
    Target framework (net8.0, net10.0). Default: net10.0

.PARAMETER UseDocker
    When true (default), uses docker connection strings. When false, uses testsettings.json + testsettings.local.json only.

.EXAMPLE
    .\scripts\run-tests-local.ps1

.EXAMPLE
    .\scripts\run-tests-local.ps1 -Framework net8.0 -Filter "FullyQualifiedName~BulkInsert"
#>
[CmdletBinding()]
param(
    [string]$Framework = 'net10.0',
    [string]$Configuration = 'Debug',
    [string]$Filter = '',
    [bool]$UseDocker = $true
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$testProject = Join-Path $repoRoot 'src\EFCore.BulkOperations.Tests\EFCore.BulkOperations.Tests.csproj'

$env:EFCORE_BULK_OPERATIONS_USE_LOCAL_DB = '1'
$env:EFCORE_BULK_EXTENSIONS_USE_LOCAL_DB = '1'
if ($UseDocker) {
    $env:EFCORE_BULK_OPERATIONS_TEST_SETTINGS = 'docker'
    $env:EFCORE_BULK_EXTENSIONS_TEST_SETTINGS = 'docker'
}
else {
    Remove-Item Env:EFCORE_BULK_OPERATIONS_TEST_SETTINGS -ErrorAction SilentlyContinue
    Remove-Item Env:EFCORE_BULK_EXTENSIONS_TEST_SETTINGS -ErrorAction SilentlyContinue
}

$testArgs = @(
    'test',
    $testProject,
    '--framework', $Framework,
    '--configuration', $Configuration,
    '--no-restore'
)

if ($Filter) {
    $testArgs += '--filter', $Filter
}

Write-Host "Running tests ($Framework, $Configuration) with local databases ..." -ForegroundColor Cyan
if ($UseDocker) {
    Write-Host 'Connection profile: testsettings.docker.json' -ForegroundColor DarkGray
}

Push-Location $repoRoot
try {
    & dotnet @testArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
