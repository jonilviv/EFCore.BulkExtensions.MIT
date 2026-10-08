using System;

namespace EFCore.BulkOperations.Tests.Benchmark;

public class BenchmarkItem
{
    public long Id { get; set; }

    public Guid ExternalId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal? Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
