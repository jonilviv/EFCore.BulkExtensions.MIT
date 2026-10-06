using EFCore.BulkOperations.SqlAdapters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace EFCore.BulkOperations.Tests.BulkInsertOrUpdate;

public class IdentityDifferentFromPrimaryKeyTests : IClassFixture<IdentityDifferentFromPrimaryKeyTests.DatabaseFixture>
{
    public class DatabaseFixture : BulkDbTestsFixture<IdentityDifferentFromPkDbContext>
    {
        protected override string DbName => nameof(IdentityDifferentFromPrimaryKeyTests);
    }

    private readonly DatabaseFixture __dbFixture;

    public IdentityDifferentFromPrimaryKeyTests(DatabaseFixture dbFixture)
    {
        __dbFixture = dbFixture;
    }

    /// <summary> Covers: https://github.com/borisdj/EFCore.BulkOperations/issues/1263 </summary>
    [Theory]
    [InlineData(DbServerType.SqlServer)]
    // [InlineData(DbServerType.PostgreSQL)] // TODO Add this here!
    public void IUD_KeepIdentity_IdentityDifferentFromKey(DbServerType dbType)
    {
        var item = new EntityKeyDifferentFromIdentity()
        {
            ItemTestGid = Guid.NewGuid(),
            ItemTestIdent = 1234,
            Name = "1234",
        };

        var item2 = new EntityKeyDifferentFromIdentity()
        {
            ItemTestGid = Guid.NewGuid(),
            ItemTestIdent = 12345678,
            Name = "12345678",
        };

        using (var db = __dbFixture.GetDb(dbType))
        {
            var items = new[] { item, item2 };

            db.BulkInsertOrUpdateOrDelete(items, c => { c.BulkCopyOptions = BulkCopyOptions.Default | BulkCopyOptions.KeepIdentity; });
        }

        using (var db = __dbFixture.GetDb(dbType))
        {
            var insertedItem = db.EntityKeyDifferentFromIdentities.Single(x => x.ItemTestGid == item.ItemTestGid);
            Assert.Equal(1234, insertedItem.ItemTestIdent);
        }
    }


    public class IdentityDifferentFromPkDbContext : DbContext
    {
        public IdentityDifferentFromPkDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<EntityKeyDifferentFromIdentity> EntityKeyDifferentFromIdentities { get; set; } = null!;
    }

    public class EntityKeyDifferentFromIdentity
    {
        [Key] public Guid ItemTestGid { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemTestIdent { get; set; } // with fluent Api: modelBuilder.Entity<ItemTest>().Property(p => p.ItemTestIdent ).ValueGeneratedOnAdd();

        public string? Name { get; set; }
    }
}