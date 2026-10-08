using System;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkConfiguration
{
    public static int GetRecordCount()
    {
        string variableName = "BENCHMARK_RECORD_COUNT";
        string? envValue = Environment.GetEnvironmentVariable(variableName);

        if (!string.IsNullOrWhiteSpace(envValue))
        {
            bool parsed = int.TryParse(envValue, out int count);

            if (parsed)
            {
                if (count > 0)
                {
                    return count;
                }
            }
        }

        int defaultCount = 1_000_000;

        return defaultCount;
    }

    public static int GetBatchSize()
    {
        string variableName = "BENCHMARK_BATCH_SIZE";
        string? envValue = Environment.GetEnvironmentVariable(variableName);

        if (!string.IsNullOrWhiteSpace(envValue))
        {
            bool parsed = int.TryParse(envValue, out int size);

            if (parsed)
            {
                if (size > 0)
                {
                    return size;
                }
            }
        }

        int defaultBatchSize = 10_000;

        return defaultBatchSize;
    }
}
