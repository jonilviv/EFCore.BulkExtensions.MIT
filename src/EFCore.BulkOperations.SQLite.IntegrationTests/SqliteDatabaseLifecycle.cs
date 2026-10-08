using EFCore.BulkOperations.Tests.Benchmark;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.SQLite.IntegrationTests;

public sealed class SqliteDatabaseLifecycle : IBenchmarkDatabaseLifecycle
{
    private string? _connectionString;
    private string? _filePath;

    public void Initialize()
    {
        if (_connectionString is not null)
        {
            return;
        }

        SQLitePCL.Batteries.Init();
        string tempDir = Path.GetTempPath();
        string fileName = "efcore_bulk_benchmark_" + Guid.NewGuid().ToString("N") + ".db";
        string path = Path.Combine(tempDir, fileName);
        _filePath = path;
        string connection = "Data Source=" + path;
        _connectionString = connection;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Initialize();
        var completed = Task.CompletedTask;

        return completed;
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
        optionsBuilder.UseSqlite(connection);
        var options = optionsBuilder.Options;
        var context = new BenchmarkDbContext(options);

        return context;
    }

    public void Dispose()
    {
        if (_filePath is not null)
        {
            bool exists = File.Exists(_filePath);

            if (exists)
            {
                try
                {
                    File.Delete(_filePath);
                }
                catch (Exception)
                {
                }
            }

            _filePath = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        var valueTask = ValueTask.CompletedTask;

        return valueTask;
    }
}
