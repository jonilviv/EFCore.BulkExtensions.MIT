using EFCore.BulkOperations.Tests.Benchmark;
using System.Threading.Tasks;
using Xunit;

namespace EFCore.BulkOperations.SqlServer.IntegrationTests;

public sealed class SqlServerBenchmarkTests
{
    [Fact]
    public void SqlServer_Benchmark_Sync()
    {
        var lifecycle = new SqlServerDatabaseLifecycle();
        string dbName = "SQL Server";
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
    public async Task SqlServer_Benchmark_Async()
    {
        var lifecycle = new SqlServerDatabaseLifecycle();
        string dbName = "SQL Server";
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
