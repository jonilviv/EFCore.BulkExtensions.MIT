using System;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkResult
{
    public string DatabaseProvider { get; set; } = string.Empty;

    public bool IsAsync { get; set; }

    public int TotalRecords { get; set; }

    public BenchmarkRunMetrics ClassicMetrics { get; set; } = new();

    public BenchmarkRunMetrics BulkMetrics { get; set; } = new();

    public double InsertSpeedupFactor { get; set; }

    public double UpdateSpeedupFactor { get; set; }

    public double DeleteSpeedupFactor { get; set; }

    public double TotalSpeedupFactor { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
