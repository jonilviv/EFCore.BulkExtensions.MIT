using EFCore.BulkOperations.Tests.Benchmark;
using System.Threading.Tasks;
using Xunit;

namespace EFCore.BulkOperations.SQLite.IntegrationTests;

public sealed class SqliteBenchmarkTests
{
    [Fact]
    public void Sqlite_Benchmark_Sync()
    {
        var lifecycle = new SqliteDatabaseLifecycle();
        string dbName = "SQLite";
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
    public async Task Sqlite_Benchmark_Async()
    {
        var lifecycle = new SqliteDatabaseLifecycle();
        string dbName = "SQLite";
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
