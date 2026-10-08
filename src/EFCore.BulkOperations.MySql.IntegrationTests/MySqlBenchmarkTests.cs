using EFCore.BulkOperations.Tests.Benchmark;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EFCore.BulkOperations.MySql.IntegrationTests;

public sealed class MySqlBenchmarkTests
{
    [Fact]
    public void MySql_Benchmark_Sync()
    {
        var lifecycle = new MySqlDatabaseLifecycle();
        string dbName = "MySQL";
        int count = BenchmarkConfiguration.GetRecordCount();

        try
        {
            BenchmarkResult result = BenchmarkExecutionEngine.ExecuteSyncBenchmark(lifecycle, dbName, count);
            Assert.NotNull(result);
            Assert.True(result.TotalSpeedupFactor > 0);
        }
        finally
        {
            lifecycle.Dispose();
        }
    }

    [Fact]
    public async Task MySql_Benchmark_Async()
    {
        var lifecycle = new MySqlDatabaseLifecycle();
        string dbName = "MySQL";
        int count = BenchmarkConfiguration.GetRecordCount();

        try
        {
            BenchmarkResult result = await BenchmarkExecutionEngine.ExecuteAsyncBenchmark(lifecycle, dbName, count);
            Assert.NotNull(result);
            Assert.True(result.TotalSpeedupFactor > 0);
        }
        finally
        {
            await lifecycle.DisposeAsync();
        }
    }
}
