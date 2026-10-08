using System;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkRunMetrics
{
    public TimeSpan TotalTime { get; set; }

    public TimeSpan InsertTime { get; set; }

    public double InsertSpeedRecordsPerSec { get; set; }

    public TimeSpan UpdateTime { get; set; }

    public double UpdateSpeedRecordsPerSec { get; set; }

    public TimeSpan DeleteTime { get; set; }

    public double DeleteSpeedRecordsPerSec { get; set; }
}
