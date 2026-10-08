using DotNet.Testcontainers.Containers;
using EFCore.BulkOperations.Tests;
using EFCore.BulkOperations.Tests.Benchmark;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.MySql;

namespace EFCore.BulkOperations.MySql.IntegrationTests;

public sealed class MySqlDatabaseLifecycle : IBenchmarkDatabaseLifecycle
{
    private MySqlContainer? _container;
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
            string provider = "MySql";
            string dbName = "EfCoreBulkBenchmarkMySql";
            string localConn = TestSettingsConfiguration.GetConnectionString(provider, dbName);
            _connectionString = localConn;

            return;
        }

        string image = "mysql:latest";
        string benchmarkDb = "EfCoreBulkBenchmarkMySql";
        var builder = new MySqlBuilder(image);
        builder = builder.WithDatabase(benchmarkDb);
        builder = builder.WithCommand("--local-infile=1");
        _container = builder.Build();
        await _container.StartAsync(cancellationToken);

        string containerConn = _container.GetConnectionString();
        var connBuilder = new MySqlConnectionStringBuilder(containerConn);
        connBuilder.AllowLoadLocalInfile = true;
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
        var version = new Version(8, 0, 36);
        var serverVersion = new MySqlServerVersion(version);
        optionsBuilder.UseMySql(connection, serverVersion);
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
