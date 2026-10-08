using DotNet.Testcontainers.Containers;
using EFCore.BulkOperations.Tests;
using EFCore.BulkOperations.Tests.Benchmark;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.MsSql;

namespace EFCore.BulkOperations.SqlServer.IntegrationTests;

public sealed class SqlServerDatabaseLifecycle : IBenchmarkDatabaseLifecycle
{
    private MsSqlContainer? _container;
    private string? _connectionString;

    public void Initialize()
    {
        var task = InitializeAsync();
        task.GetAwaiter().GetResult();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionString is not null)
        {
            return;
        }

        bool useLocal = TestSettingsConfiguration.UseLocalDatabases;

        if (useLocal)
        {
            string provider = "SqlServer";
            string dbName = "EfCoreBulkBenchmarkSqlServer";
            string localConn = TestSettingsConfiguration.GetConnectionString(provider, dbName);
            _connectionString = localConn;

            return;
        }

        string image = "mcr.microsoft.com/mssql/server:2022-latest";
        var builder = new MsSqlBuilder(image);
        _container = builder.Build();
        await _container.StartAsync(cancellationToken);

        string containerConn = _container.GetConnectionString();
        var connBuilder = new SqlConnectionStringBuilder(containerConn);
        string initialCatalog = "EfCoreBulkBenchmarkSqlServer";
        connBuilder.InitialCatalog = initialCatalog;
        connBuilder.MultipleActiveResultSets = true;
        connBuilder.TrustServerCertificate = true;
        string enhancedConn = connBuilder.ToString();
        _connectionString = enhancedConn;
    }

    public void RecreateDatabase()
    {
        using var context = CreateDbContext();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }

    public async Task RecreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public BenchmarkDbContext CreateDbContext()
    {
        if (_connectionString is null)
        {
            Initialize();
        }

        var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
        string connection = _connectionString!;
        optionsBuilder.UseSqlServer(connection);
        var options = optionsBuilder.Options;
        var context = new BenchmarkDbContext(options);

        return context;
    }

    public void Dispose()
    {
        var task = DisposeAsync();
        var asTask = task.AsTask();
        asTask.GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }
}
