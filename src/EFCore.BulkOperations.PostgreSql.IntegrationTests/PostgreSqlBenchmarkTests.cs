using EFCore.BulkOperations.Tests.Benchmark;
using System.Threading.Tasks;
using Xunit;

namespace EFCore.BulkOperations.PostgreSql.IntegrationTests;

public sealed class PostgreSqlBenchmarkTests
{
    [Fact]
    public void PostgreSql_Benchmark_Sync()
    {
        var lifecycle = new PostgreSqlDatabaseLifecycle();
        string dbName = "PostgreSQL";
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
    public async Task PostgreSql_Benchmark_Async()
    {
        var lifecycle = new PostgreSqlDatabaseLifecycle();
        string dbName = "PostgreSQL";
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
