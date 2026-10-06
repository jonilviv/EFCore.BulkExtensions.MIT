using EFCore.BulkOperations.SqlAdapters;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EFCore.BulkOperations.Tests.BulkInsertOrUpdate;

public class BulkInsertTests : IClassFixture<BulkInsertTests.DatabaseFixture>
{
    private readonly DatabaseFixture __dbFixture;

    public BulkInsertTests(DatabaseFixture dbFixture)
    {
        __dbFixture = dbFixture;
    }

    public class DatabaseFixture : BulkDbTestsFixture<SimpleBulkTestsContext>
    {
        protected override string DbName => nameof(BulkInsertTests);
    }


    [Theory]
    [InlineData(DbServerType.SqlServer)]
    public async Task BulkInsert_UnfilledIds(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();

        using (var db = __dbFixture.GetDb(dbType))
        {
            var items = new[]
            {
                new SimpleItem()
                {
                    Id = 0,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
                new SimpleItem()
                {
                    Id = 0,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
            };

            await db.BulkInsertAsync(items, new BulkConfig()
            {
                SetOutputIdentity = true
            });

            Assert.True(items.All(x => x.Id != 0));
            Assert.True(items.All(x => x.Id > 0));
            var itemsFromDb = db.SimpleItems.Where(x => x.BulkIdentifier == bulkId).ToList();
            Assert.True(itemsFromDb.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty).SequenceEqual(items.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty)));
        }
    }

    [Theory]
    [InlineData(DbServerType.SqlServer)]
    public async Task BulkInsert_NegativeIds(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();

        using (var db = __dbFixture.GetDb(dbType))
        {
            var items = new[]
            {
                new SimpleItem()
                {
                    Id = -2,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
                new SimpleItem()
                {
                    Id = -1,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
            };

            await db.BulkInsertAsync(items, new BulkConfig()
            {
                SetOutputIdentity = true
            });

            Assert.True(items.All(x => x.Id != 0));
            Assert.True(items.All(x => x.Id > 0));
            var itemsFromDb = db.SimpleItems.Where(x => x.BulkIdentifier == bulkId).ToList();
            Assert.True(itemsFromDb.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty).SequenceEqual(items.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty)));
        }
    }

    [Theory]
    [InlineData(DbServerType.SqlServer)]
    public async Task BulkInsert_NonZeroIds(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();

        using (var db = __dbFixture.GetDb(dbType))
        {
            var items = new[]
            {
                new SimpleItem()
                {
                    Id = 123,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
                new SimpleItem()
                {
                    Id = 12345,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
            };

            await db.BulkInsertAsync(items, new BulkConfig()
            {
                SetOutputIdentity = true
            });

            Assert.True(items.All(x => x.Id != 0));
            Assert.True(items.All(x => x.Id > 0));
            var itemsFromDb = db.SimpleItems.Where(x => x.BulkIdentifier == bulkId).ToList();
            Assert.True(itemsFromDb.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty).SequenceEqual(items.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty)));
        }
    }

    [Theory]
    [InlineData(DbServerType.SqlServer)]
    public async Task BulkInsert_AtypicalIds(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();

        using (var db = __dbFixture.GetDb(dbType))
        {
            var items = new[]
            {
                new SimpleItem()
                {
                    Id = 0,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
                new SimpleItem()
                {
                    Id = 123,
                    GuidProperty = Guid.NewGuid(),
                    BulkIdentifier = bulkId,
                },
            };

            await db.BulkInsertAsync(items, new BulkConfig()
            {
                SetOutputIdentity = true
            });

            Assert.True(items.All(x => x.Id != 0));
            Assert.True(items.All(x => x.Id > 0));
            var itemsFromDb = db.SimpleItems.Where(x => x.BulkIdentifier == bulkId).ToList();
            Assert.True(itemsFromDb.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty).SequenceEqual(items.OrderBy(x => x.GuidProperty).Select(x => x.GuidProperty)));
        }
    }

    [Theory]
    [InlineData(DbServerType.SqlServer)]
    [InlineData(DbServerType.SqLite)]
    public void BulkInsert_CustomColumnNames(DbServerType dbType)
    {
        var item = new EntityCustomColumnNames()
        {
            Id = 0,
            CustomColumn = "Value1",
            GuidProperty = Guid.NewGuid(),
        };

        var item2 = new EntityCustomColumnNames()
        {
            Id = 0,
            CustomColumn = "Value2",
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = __dbFixture.GetDb(dbType))
        {
            var items = new[] { item, item2 };
            db.BulkInsert(items, c =>
            {
                c.SetOutputIdentity = true;
            });
        }

        using (var db = __dbFixture.GetDb(dbType))
        {
            var insertedItem = db.EntityCustomColumnNames.Single(x => x.GuidProperty == item.GuidProperty);
            Assert.Equal("Value1", insertedItem.CustomColumn);
        }
    }

    [Theory]
    [InlineData(DbServerType.PostgreSql)]
    public async Task BulkInsert_CloseConnection(DbServerType dbType)
    {
        await using var db = __dbFixture.GetDb(dbType);
        var items = new[]
        {
            new SimpleItem
            {
                Id = 0,
                GuidProperty = Guid.NewGuid()
            },
            new SimpleItem
            {
                Id = 0,
                GuidProperty = Guid.NewGuid()
            },
        };

        await db.BulkInsertAsync(items, new BulkConfig
        {
            SetOutputIdentity = true
        });

        Assert.True(db.Database.GetDbConnection().State == ConnectionState.Closed);
    }
}