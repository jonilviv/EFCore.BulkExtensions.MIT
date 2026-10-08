using System;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.Tests.Benchmark;

public interface IBenchmarkDatabaseLifecycle : IAsyncDisposable, IDisposable
{
    void Initialize();

    Task InitializeAsync(CancellationToken cancellationToken = default);

    void RecreateDatabase();

    Task RecreateDatabaseAsync(CancellationToken cancellationToken = default);

    BenchmarkDbContext CreateDbContext();
}
