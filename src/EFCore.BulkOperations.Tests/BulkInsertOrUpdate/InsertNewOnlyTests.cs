using EFCore.BulkOperations.SqlAdapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EFCore.BulkOperations.Tests.BulkInsertOrUpdate;

public class InsertNewOnlyTests : IClassFixture<InsertNewOnlyTests.DatabaseFixture>
{
    private readonly DatabaseFixture __dbFixture;

    public InsertNewOnlyTests(DatabaseFixture dbFixture)
    {
        __dbFixture = dbFixture;
    }

    /// <summary> Covers issue: https://github.com/borisdj/EFCore.BulkOperations/issues/321 </summary>
    [Theory]
    [InlineData(DbServerType.SqlServer)]
    [InlineData(DbServerType.PostgreSql)]
    [InlineData(DbServerType.MySql)]
    public void BulkInsertOrUpdate_InsertNewOnly(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();

        var initialItem = new SimpleItem()
        {
            Name = "initial1",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = __dbFixture.GetDb(dbType))
        {
            db.SimpleItems.Add(initialItem);
            db.SaveChanges();
        }

        var initialItemId = initialItem.Id;

        // Should be inserted
        var newItem = new SimpleItem()
        {
            Id = initialItemId + 1,
            StringProperty = "insertedByBulk",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        // Should be also inserted
        var newItem2 = new SimpleItem()
        {
            Id = initialItemId + 2,
            StringProperty = "insertedByBulk2",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        // Should not be updated (since we use insert only new scenario):
        var updatedItem = new SimpleItem()
        {
            Id = initialItemId,
            BulkIdentifier = bulkId,
            Name = "updated by Bulks",
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = __dbFixture.GetDb(dbType))
        {
            var ensureList = new[] { newItem, updatedItem, newItem2 };

            db.BulkInsertOrUpdate(ensureList, c => { c.PropertiesToIncludeOnUpdate = new() { "" }; });
        }

        var allItems = __dbFixture.GetDb(dbType).GetItemsOfBulk(bulkId);

        Assert.Equal(3, allItems.Count);

        var insertedItem = allItems.SingleOrDefault(x => x.GuidProperty == newItem.GuidProperty);
        var insertedItem2 = allItems.SingleOrDefault(x => x.GuidProperty == newItem2.GuidProperty);
        Assert.NotNull(insertedItem);
        Assert.NotNull(insertedItem2);

        // initial item was not updated:
        var itemWhichWasNotUpdated = allItems.Single(x => x.GuidProperty == initialItem.GuidProperty);
        Assert.Equal(initialItem.Id, itemWhichWasNotUpdated.Id);
        Assert.Equal(initialItem.Name, itemWhichWasNotUpdated.Name);
    }

    [Theory]
    [InlineData(DbServerType.SqlServer)]
    // [InlineData(DbServerType.PostgreSQL)] 
    // PostgreSQL is ignored due to unfixed bug. See: https://github.com/videokojot/EFCore.BulkOperations.MIT/issues/110
    [InlineData(DbServerType.MySql)]
    public void BulkInsertOrUpdate_InsertNewOnly_ByColumns(DbServerType dbType)
    {
        var bulkId = Guid.NewGuid();

        var initialItem = new SimpleItem()
        {
            Name = "1",
            StringProperty = "1",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = __dbFixture.GetDb(dbType))
        {
            db.SimpleItems.Add(initialItem);
            db.SaveChanges();
        }

        var initialItemId = (initialItem.BulkIdentifier, initialItem.StringProperty);

        // Should be inserted
        var newItem = new SimpleItem()
        {
            StringProperty = "insertedByBulk",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        // Should be also inserted
        var newItem2 = new SimpleItem()
        {
            StringProperty = "insertedByBulk2",
            BulkIdentifier = bulkId,
            GuidProperty = Guid.NewGuid(),
        };

        // Should not be updated (since we use insert only new scenario):
        var updatedItem = new SimpleItem()
        {
            StringProperty = initialItemId.StringProperty,
            BulkIdentifier = bulkId,
            Name = "updated by Bulks",
            GuidProperty = Guid.NewGuid(),
        };

        using (var db = __dbFixture.GetDb(dbType))
        {
            var ensureList = new[] { newItem, updatedItem, newItem2 };

            db.BulkInsertOrUpdate(ensureList, c =>
            {
                c.PropertiesToIncludeOnUpdate = new() { "" };
                c.UpdateByProperties = new List<string> { nameof(SimpleItem.StringProperty), nameof(SimpleItem.BulkIdentifier) };
            });
        }

        var allItems = __dbFixture.GetDb(dbType).GetItemsOfBulk(bulkId);

        Assert.Equal(3, allItems.Count);

        var insertedItem = allItems.SingleOrDefault(x => x.GuidProperty == newItem.GuidProperty);
        var insertedItem2 = allItems.SingleOrDefault(x => x.GuidProperty == newItem2.GuidProperty);
        Assert.NotNull(insertedItem);
        Assert.NotNull(insertedItem2);

        // initial item was not updated:
        var itemWhichWasNotUpdated = allItems.Single(x => x.GuidProperty == initialItem.GuidProperty);
        Assert.Equal(initialItem.Id, itemWhichWasNotUpdated.Id);
        Assert.Equal(initialItem.Name, itemWhichWasNotUpdated.Name);
    }


    public class DatabaseFixture : BulkDbTestsFixture<SimpleBulkTestsContext>
    {
        protected override string DbName => nameof(InsertNewOnlyTests);
    }
}