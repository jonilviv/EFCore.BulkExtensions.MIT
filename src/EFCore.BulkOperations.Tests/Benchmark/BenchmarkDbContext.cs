using Microsoft.EntityFrameworkCore;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkDbContext : DbContext
{
    public DbSet<BenchmarkItem> BenchmarkItems { get; set; } = null!;

    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BenchmarkItem>();
        string tableName = "BenchmarkItems";
        entity.ToTable(tableName);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        int nameMaxLength = 100;
        entity.Property(x => x.Name).HasMaxLength(nameMaxLength).IsRequired();
        int descriptionMaxLength = 500;
        entity.Property(x => x.Description).HasMaxLength(descriptionMaxLength).IsRequired();
    }
}
