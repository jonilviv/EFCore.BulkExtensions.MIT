@echo off
setlocal
cd /d "%~dp0"

set VERSION=12.0.0
set NUGET_API_KEY=<your-api-key-here>
set NUGET_SOURCE=https://api.nuget.org/v3/index.json

dotnet clean

dotnet pack -c Release .\src\EFCore.BulkOperations\EFCore.BulkOperations.csproj
if errorlevel 1 exit /b 1
dotnet pack -c Release .\src\Providers\EFCore.BulkOperations.SqlServer\EFCore.BulkOperations.SqlServer.csproj
if errorlevel 1 exit /b 1
dotnet pack -c Release .\src\Providers\EFCore.BulkOperations.SQLite\EFCore.BulkOperations.SQLite.csproj
if errorlevel 1 exit /b 1
dotnet pack -c Release .\src\Providers\EFCore.BulkOperations.PostgreSql\EFCore.BulkOperations.PostgreSql.csproj
if errorlevel 1 exit /b 1
dotnet pack -c Release .\src\Providers\EFCore.BulkOperations.MySql\EFCore.BulkOperations.MySql.csproj
if errorlevel 1 exit /b 1



dotnet nuget push dist\EFCore.BulkOperations.%VERSION%.nupkg           -api-key %NUGET_API_KEY% --source %NUGET_SOURCE%
dotnet nuget push dist\EFCore.BulkOperations.MySql.%VERSION%.nupkg     -api-key %NUGET_API_KEY% --source %NUGET_SOURCE%
dotnet nuget push dist\EFCore.BulkOperations.PostgreSql.%VERSION%.nupkg -api-key %NUGET_API_KEY% --source %NUGET_SOURCE%
dotnet nuget push dist\EFCore.BulkOperations.SQLite.%VERSION%.nupkg    -api-key %NUGET_API_KEY% --source %NUGET_SOURCE%
dotnet nuget push dist\EFCore.BulkOperations.SqlServer.%VERSION%.nupkg -api-key %NUGET_API_KEY% --source %NUGET_SOURCE%