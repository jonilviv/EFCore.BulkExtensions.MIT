# Local test database scripts

Tests in `EFCore.BulkExtensions.Tests` use **Testcontainers** by default (Docker required, containers start automatically).

For faster repeat runs or debugging, use **persistent local containers** and connection strings from `testsettings.docker.json`.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (8.x, 10.x)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows/macOS/Linux)

## Quick start (Docker databases)

```powershell
# From repository root
.\scripts\start-test-databases.ps1
.\scripts\run-tests-local.ps1
.\scripts\stop-test-databases.ps1
```

`start-test-databases.ps1` runs `docker compose -f docker-compose.test.yml up -d --wait`.

`run-tests-local.ps1` sets:

- `EFCORE_BULK_EXTENSIONS_USE_LOCAL_DB=1` — use `testsettings.json` instead of Testcontainers
- `EFCORE_BULK_EXTENSIONS_TEST_SETTINGS=docker` — merge `testsettings.docker.json`

## Native servers (no Docker for databases)

1. Install SQL Server / PostgreSQL / MySQL locally.
2. Copy `EFCore.BulkExtensions.Tests\testsettings.local.json.example` to `testsettings.local.json` and edit credentials.
3. Run:

```powershell
.\scripts\run-tests-local.ps1 -UseDocker:$false
```

SQLite tests need no server (file-based `.db` in the test output folder).

## Ports and passwords (Docker compose)

| Service    | Port | Credentials                          |
|-----------|------|--------------------------------------|
| SQL Server | 1433 | `SA` / `YourStrong!Passw0rd`         |
| PostgreSQL | 5432 | `postgres` / `Postgres22`            |
| MySQL      | 3306 | `root` / `MySQL22`                   |

## Examples

```powershell
# Single test class, .NET 8
.\scripts\run-tests-local.ps1 -Framework net8.0 -Filter "FullyQualifiedName~EFCoreBulkTest"

# Default Testcontainers mode (no scripts)
dotnet test EFCore.BulkExtensions.Tests\EFCore.BulkExtensions.Tests.csproj
```
