using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkExecutionEngine
{
    public static BenchmarkResult ExecuteSyncBenchmark(IBenchmarkDatabaseLifecycle lifecycle, string databaseName, int count)
    {
        lifecycle.Initialize();
        int batchSize = BenchmarkConfiguration.GetBatchSize();

        lifecycle.RecreateDatabase();
        var classicItems = BenchmarkDataGenerator.GenerateItems(count);

        var classicTotalWatch = Stopwatch.StartNew();
        var classicInsertWatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i += batchSize)
        {
            int remaining = count - i;
            int currentBatchSize = Math.Min(batchSize, remaining);
            var batch = classicItems.GetRange(i, currentBatchSize);
            using var context = lifecycle.CreateDbContext();
            context.BenchmarkItems.AddRange(batch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }

        classicInsertWatch.Stop();

        BenchmarkDataGenerator.MutateItemsForUpdate(classicItems);
        var classicUpdateWatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i += batchSize)
        {
            int remaining = count - i;
            int currentBatchSize = Math.Min(batchSize, remaining);
            var batch = classicItems.GetRange(i, currentBatchSize);
            using var context = lifecycle.CreateDbContext();
            context.BenchmarkItems.UpdateRange(batch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }

        classicUpdateWatch.Stop();

        var classicDeleteWatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i += batchSize)
        {
            int remaining = count - i;
            int currentBatchSize = Math.Min(batchSize, remaining);
            var batch = classicItems.GetRange(i, currentBatchSize);
            using var context = lifecycle.CreateDbContext();
            context.BenchmarkItems.RemoveRange(batch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }

        classicDeleteWatch.Stop();
        classicTotalWatch.Stop();

        lifecycle.RecreateDatabase();
        var bulkItems = BenchmarkDataGenerator.GenerateItems(count);

        var bulkTotalWatch = Stopwatch.StartNew();
        var bulkInsertWatch = Stopwatch.StartNew();

        using (var bulkContext = lifecycle.CreateDbContext())
        {
            bulkContext.BulkInsert(bulkItems);
        }

        bulkInsertWatch.Stop();

        BenchmarkDataGenerator.MutateItemsForUpdate(bulkItems);
        var bulkUpdateWatch = Stopwatch.StartNew();

        using (var bulkUpdateContext = lifecycle.CreateDbContext())
        {
            bulkUpdateContext.BulkUpdate(bulkItems);
        }

        bulkUpdateWatch.Stop();

        var bulkDeleteWatch = Stopwatch.StartNew();

        using (var bulkDeleteContext = lifecycle.CreateDbContext())
        {
            bulkDeleteContext.BulkDelete(bulkItems);
        }

        bulkDeleteWatch.Stop();
        bulkTotalWatch.Stop();

        TimeSpan classicInsertTime = classicInsertWatch.Elapsed;
        TimeSpan classicUpdateTime = classicUpdateWatch.Elapsed;
        TimeSpan classicDeleteTime = classicDeleteWatch.Elapsed;
        TimeSpan classicTotalTime = classicTotalWatch.Elapsed;

        TimeSpan bulkInsertTime = bulkInsertWatch.Elapsed;
        TimeSpan bulkUpdateTime = bulkUpdateWatch.Elapsed;
        TimeSpan bulkDeleteTime = bulkDeleteWatch.Elapsed;
        TimeSpan bulkTotalTime = bulkTotalWatch.Elapsed;

        double minSec = 0.0001;
        double minMs = 1.0;

        double classicInsertTotalSec = classicInsertTime.TotalSeconds;
        double classicInsertSec = Math.Max(classicInsertTotalSec, minSec);

        double classicUpdateTotalSec = classicUpdateTime.TotalSeconds;
        double classicUpdateSec = Math.Max(classicUpdateTotalSec, minSec);

        double classicDeleteTotalSec = classicDeleteTime.TotalSeconds;
        double classicDeleteSec = Math.Max(classicDeleteTotalSec, minSec);

        double bulkInsertTotalSec = bulkInsertTime.TotalSeconds;
        double bulkInsertSec = Math.Max(bulkInsertTotalSec, minSec);

        double bulkUpdateTotalSec = bulkUpdateTime.TotalSeconds;
        double bulkUpdateSec = Math.Max(bulkUpdateTotalSec, minSec);

        double bulkDeleteTotalSec = bulkDeleteTime.TotalSeconds;
        double bulkDeleteSec = Math.Max(bulkDeleteTotalSec, minSec);

        double classicInsertSpeed = count / classicInsertSec;
        double classicUpdateSpeed = count / classicUpdateSec;
        double classicDeleteSpeed = count / classicDeleteSec;

        double bulkInsertSpeed = count / bulkInsertSec;
        double bulkUpdateSpeed = count / bulkUpdateSec;
        double bulkDeleteSpeed = count / bulkDeleteSec;

        double bulkInsertTotalMs = bulkInsertTime.TotalMilliseconds;
        double bulkInsertMs = Math.Max(bulkInsertTotalMs, minMs);

        double bulkUpdateTotalMs = bulkUpdateTime.TotalMilliseconds;
        double bulkUpdateMs = Math.Max(bulkUpdateTotalMs, minMs);

        double bulkDeleteTotalMs = bulkDeleteTime.TotalMilliseconds;
        double bulkDeleteMs = Math.Max(bulkDeleteTotalMs, minMs);

        double bulkTotalOverallMs = bulkTotalTime.TotalMilliseconds;
        double bulkTotalMs = Math.Max(bulkTotalOverallMs, minMs);

        double classicInsertMs = classicInsertTime.TotalMilliseconds;
        double classicUpdateMs = classicUpdateTime.TotalMilliseconds;
        double classicDeleteMs = classicDeleteTime.TotalMilliseconds;
        double classicTotalMs = classicTotalTime.TotalMilliseconds;

        double insertSpeedup = classicInsertMs / bulkInsertMs;
        double updateSpeedup = classicUpdateMs / bulkUpdateMs;
        double deleteSpeedup = classicDeleteMs / bulkDeleteMs;
        double totalSpeedup = classicTotalMs / bulkTotalMs;

        var result = new BenchmarkResult
        {
            DatabaseProvider = databaseName,
            IsAsync = false,
            TotalRecords = count,
            ClassicMetrics = new BenchmarkRunMetrics
            {
                TotalTime = classicTotalTime,
                InsertTime = classicInsertTime,
                InsertSpeedRecordsPerSec = classicInsertSpeed,
                UpdateTime = classicUpdateTime,
                UpdateSpeedRecordsPerSec = classicUpdateSpeed,
                DeleteTime = classicDeleteTime,
                DeleteSpeedRecordsPerSec = classicDeleteSpeed
            },
            BulkMetrics = new BenchmarkRunMetrics
            {
                TotalTime = bulkTotalTime,
                InsertTime = bulkInsertTime,
                InsertSpeedRecordsPerSec = bulkInsertSpeed,
                UpdateTime = bulkUpdateTime,
                UpdateSpeedRecordsPerSec = bulkUpdateSpeed,
                DeleteTime = bulkDeleteTime,
                DeleteSpeedRecordsPerSec = bulkDeleteSpeed
            },
            InsertSpeedupFactor = insertSpeedup,
            UpdateSpeedupFactor = updateSpeedup,
            DeleteSpeedupFactor = deleteSpeedup,
            TotalSpeedupFactor = totalSpeedup,
            ExecutedAt = DateTime.UtcNow
        };

        BenchmarkReporter.RecordResult(result);

        return result;
    }

    public static async Task<BenchmarkResult> ExecuteAsyncBenchmark(
        IBenchmarkDatabaseLifecycle lifecycle,
        string databaseName,
        int count,
        CancellationToken cancellationToken = default)
    {
        await lifecycle.InitializeAsync(cancellationToken);
        int batchSize = BenchmarkConfiguration.GetBatchSize();

        await lifecycle.RecreateDatabaseAsync(cancellationToken);
        var classicItems = BenchmarkDataGenerator.GenerateItems(count);

        var classicTotalWatch = Stopwatch.StartNew();
        var classicInsertWatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i += batchSize)
        {
            int remaining = count - i;
            int currentBatchSize = Math.Min(batchSize, remaining);
            var batch = classicItems.GetRange(i, currentBatchSize);
            await using var context = lifecycle.CreateDbContext();
            await context.BenchmarkItems.AddRangeAsync(batch, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        classicInsertWatch.Stop();

        BenchmarkDataGenerator.MutateItemsForUpdate(classicItems);
        var classicUpdateWatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i += batchSize)
        {
            int remaining = count - i;
            int currentBatchSize = Math.Min(batchSize, remaining);
            var batch = classicItems.GetRange(i, currentBatchSize);
            await using var context = lifecycle.CreateDbContext();
            context.BenchmarkItems.UpdateRange(batch);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        classicUpdateWatch.Stop();

        var classicDeleteWatch = Stopwatch.StartNew();

        for (int i = 0; i < count; i += batchSize)
        {
            int remaining = count - i;
            int currentBatchSize = Math.Min(batchSize, remaining);
            var batch = classicItems.GetRange(i, currentBatchSize);
            await using var context = lifecycle.CreateDbContext();
            context.BenchmarkItems.RemoveRange(batch);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        classicDeleteWatch.Stop();
        classicTotalWatch.Stop();

        await lifecycle.RecreateDatabaseAsync(cancellationToken);
        var bulkItems = BenchmarkDataGenerator.GenerateItems(count);

        var bulkTotalWatch = Stopwatch.StartNew();
        var bulkInsertWatch = Stopwatch.StartNew();

        await using (var bulkContext = lifecycle.CreateDbContext())
        {
            await bulkContext.BulkInsertAsync(bulkItems, cancellationToken: cancellationToken);
        }

        bulkInsertWatch.Stop();

        BenchmarkDataGenerator.MutateItemsForUpdate(bulkItems);
        var bulkUpdateWatch = Stopwatch.StartNew();

        await using (var bulkUpdateContext = lifecycle.CreateDbContext())
        {
            await bulkUpdateContext.BulkUpdateAsync(bulkItems, cancellationToken: cancellationToken);
        }

        bulkUpdateWatch.Stop();

        var bulkDeleteWatch = Stopwatch.StartNew();

        await using (var bulkDeleteContext = lifecycle.CreateDbContext())
        {
            await bulkDeleteContext.BulkDeleteAsync(bulkItems, cancellationToken: cancellationToken);
        }

        bulkDeleteWatch.Stop();
        bulkTotalWatch.Stop();

        TimeSpan classicInsertTime = classicInsertWatch.Elapsed;
        TimeSpan classicUpdateTime = classicUpdateWatch.Elapsed;
        TimeSpan classicDeleteTime = classicDeleteWatch.Elapsed;
        TimeSpan classicTotalTime = classicTotalWatch.Elapsed;

        TimeSpan bulkInsertTime = bulkInsertWatch.Elapsed;
        TimeSpan bulkUpdateTime = bulkUpdateWatch.Elapsed;
        TimeSpan bulkDeleteTime = bulkDeleteWatch.Elapsed;
        TimeSpan bulkTotalTime = bulkTotalWatch.Elapsed;

        double minSec = 0.0001;
        double minMs = 1.0;

        double classicInsertTotalSec = classicInsertTime.TotalSeconds;
        double classicInsertSec = Math.Max(classicInsertTotalSec, minSec);

        double classicUpdateTotalSec = classicUpdateTime.TotalSeconds;
        double classicUpdateSec = Math.Max(classicUpdateTotalSec, minSec);

        double classicDeleteTotalSec = classicDeleteTime.TotalSeconds;
        double classicDeleteSec = Math.Max(classicDeleteTotalSec, minSec);

        double bulkInsertTotalSec = bulkInsertTime.TotalSeconds;
        double bulkInsertSec = Math.Max(bulkInsertTotalSec, minSec);

        double bulkUpdateTotalSec = bulkUpdateTime.TotalSeconds;
        double bulkUpdateSec = Math.Max(bulkUpdateTotalSec, minSec);

        double bulkDeleteTotalSec = bulkDeleteTime.TotalSeconds;
        double bulkDeleteSec = Math.Max(bulkDeleteTotalSec, minSec);

        double classicInsertSpeed = count / classicInsertSec;
        double classicUpdateSpeed = count / classicUpdateSec;
        double classicDeleteSpeed = count / classicDeleteSec;

        double bulkInsertSpeed = count / bulkInsertSec;
        double bulkUpdateSpeed = count / bulkUpdateSec;
        double bulkDeleteSpeed = count / bulkDeleteSec;

        double bulkInsertTotalMs = bulkInsertTime.TotalMilliseconds;
        double bulkInsertMs = Math.Max(bulkInsertTotalMs, minMs);

        double bulkUpdateTotalMs = bulkUpdateTime.TotalMilliseconds;
        double bulkUpdateMs = Math.Max(bulkUpdateTotalMs, minMs);

        double bulkDeleteTotalMs = bulkDeleteTime.TotalMilliseconds;
        double bulkDeleteMs = Math.Max(bulkDeleteTotalMs, minMs);

        double bulkTotalOverallMs = bulkTotalTime.TotalMilliseconds;
        double bulkTotalMs = Math.Max(bulkTotalOverallMs, minMs);

        double classicInsertMs = classicInsertTime.TotalMilliseconds;
        double classicUpdateMs = classicUpdateTime.TotalMilliseconds;
        double classicDeleteMs = classicDeleteTime.TotalMilliseconds;
        double classicTotalMs = classicTotalTime.TotalMilliseconds;

        double insertSpeedup = classicInsertMs / bulkInsertMs;
        double updateSpeedup = classicUpdateMs / bulkUpdateMs;
        double deleteSpeedup = classicDeleteMs / bulkDeleteMs;
        double totalSpeedup = classicTotalMs / bulkTotalMs;

        var result = new BenchmarkResult
        {
            DatabaseProvider = databaseName,
            IsAsync = true,
            TotalRecords = count,
            ClassicMetrics = new BenchmarkRunMetrics
            {
                TotalTime = classicTotalTime,
                InsertTime = classicInsertTime,
                InsertSpeedRecordsPerSec = classicInsertSpeed,
                UpdateTime = classicUpdateTime,
                UpdateSpeedRecordsPerSec = classicUpdateSpeed,
                DeleteTime = classicDeleteTime,
                DeleteSpeedRecordsPerSec = classicDeleteSpeed
            },
            BulkMetrics = new BenchmarkRunMetrics
            {
                TotalTime = bulkTotalTime,
                InsertTime = bulkInsertTime,
                InsertSpeedRecordsPerSec = bulkInsertSpeed,
                UpdateTime = bulkUpdateTime,
                UpdateSpeedRecordsPerSec = bulkUpdateSpeed,
                DeleteTime = bulkDeleteTime,
                DeleteSpeedRecordsPerSec = bulkDeleteSpeed
            },
            InsertSpeedupFactor = insertSpeedup,
            UpdateSpeedupFactor = updateSpeedup,
            DeleteSpeedupFactor = deleteSpeedup,
            TotalSpeedupFactor = totalSpeedup,
            ExecutedAt = DateTime.UtcNow
        };

        BenchmarkReporter.RecordResult(result);

        return result;
    }
}
