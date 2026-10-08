using DotNet.Testcontainers.Containers;
using EFCore.BulkOperations.Tests;
using EFCore.BulkOperations.Tests.Benchmark;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;

namespace EFCore.BulkOperations.PostgreSql.IntegrationTests;

public sealed class PostgreSqlDatabaseLifecycle : IBenchmarkDatabaseLifecycle
{
    private PostgreSqlContainer? _container;
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
            string provider = "PostgreSql";
            string dbName = "EfCoreBulkBenchmarkPostgreSql";
            string localConn = TestSettingsConfiguration.GetConnectionString(provider, dbName);
            string errorDetail = ";Include Error Detail=True";
            _connectionString = localConn + errorDetail;

            return;
        }

        string image = "postgres:16";
        var builder = new PostgreSqlBuilder(image);
        _container = builder.Build();
        await _container.StartAsync(cancellationToken);

        string containerConn = _container.GetConnectionString();
        var connBuilder = new NpgsqlConnectionStringBuilder(containerConn);
        string initialDb = "EfCoreBulkBenchmarkPostgreSql";
        connBuilder.Database = initialDb;
        string enhancedConn = connBuilder.ToString();
        string detailSuffix = ";Include Error Detail=True";
        _connectionString = enhancedConn + detailSuffix;
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
        optionsBuilder.UseNpgsql(connection);
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
