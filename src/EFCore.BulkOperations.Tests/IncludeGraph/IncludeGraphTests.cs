using EFCore.BulkOperations.SqlAdapters;
using EFCore.BulkOperations.Tests.IncludeGraph.Model;
using EFCore.BulkOperations.Tests.ShadowProperties;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.Tests.IncludeGraph;

public class IncludeGraphTests : IDisposable
{
    private readonly static WorkOrder __workOrder1 = new ()
    {
        Description = "Fix belt",
        Asset = new Asset
        {
            Description = "MANU-1",
            Location = "WAREHOUSE-1"
        },
        WorkOrderSpares =
            {
                new WorkOrderSpare
                {
                    Description = "Bolt 5mm x5",
                    Quantity = 5,
                    Spare = new Spare
                    {
                        PartNumber = "MZD 5mm",
                        Barcode = "12345"
                    }
                },
                new WorkOrderSpare
                {
                    Description = "Bolt 10mm x5",
                    Quantity = 5,
                    Spare = new Spare
                    {
                        PartNumber = "MZD 10mm",
                        Barcode = "222655"
                    }
                }
            }
    };

    private static readonly WorkOrder __workOrder2 = new ()
    {
        Description = "Fix toilets",
        Asset = new Asset
        {
            Description = "FLUSHMASTER-1",
            Location = "GYM-BLOCK-3"
        },
        WorkOrderSpares =
        {
            new WorkOrderSpare
            {
                Description = "Plunger",
                Quantity = 2,
                Spare = new Spare
                {
                    PartNumber = "Poo'o'magic 531",
                    Barcode = "544532bbc"
                }
            },
            new WorkOrderSpare
            {
                Description = "Crepepele",
                Quantity = 1,
                Spare = new Spare
                {
                    PartNumber = "MZD f",
                    Barcode = "222655"
                }
            }
        }
    };

    [Theory]
    [InlineData(DbServerType.SqlServer)]
    //[InlineData(DbServer.Sqlite)]
    public async Task BulkInsertOrUpdate_EntityWithNestedObjectGraph_SavesGraphToDatabase(DbServerType dbServer)
    {
        ContextUtil.DbServer = dbServer;

        using var db = new GraphDbContext(ContextUtil.GetOptions<GraphDbContext>(databaseName: $"{nameof(EfCoreBulkTest)}_Graph"));
        await db.Database.EnsureCreatedAsync();

        // To ensure there are no stack overflows with circular reference trees, we must test for that.
        // Set all navigation properties so the base navigation and its inverse both have values
        foreach (var wos in __workOrder1.WorkOrderSpares)
        {
            wos.WorkOrder = __workOrder1;
        }

        foreach (var wos in __workOrder2.WorkOrderSpares)
        {
            wos.WorkOrder = __workOrder2;
        }

        __workOrder1.Asset.WorkOrders.Add(__workOrder1);
        __workOrder2.Asset.WorkOrders.Add(__workOrder2);

        __workOrder1.Asset.ParentAsset = __workOrder2.Asset;
        __workOrder2.Asset.ChildAssets.Add(__workOrder1.Asset);

        var testData = GetTestData().ToList();
        await db.BulkInsertOrUpdateAsync(testData, new BulkConfig
        {
            IncludeGraph = true
        });

        var workOrderQuery = db.WorkOrderSpares
            .Include(y => y.WorkOrder)
            .Include(y => y.WorkOrder.Asset)
            .Include(y => y.Spare);

        foreach (var wos in workOrderQuery)
        {
            Assert.NotNull(wos.WorkOrder);
            Assert.NotNull(wos.WorkOrder.Asset);
            Assert.NotNull(wos.Spare);
        }
    }

    private static IEnumerable<WorkOrder> GetTestData()
    {
        yield return __workOrder1;
        yield return __workOrder2;

    }

    public void Dispose()
    {
        using var db = new GraphDbContext(ContextUtil.GetOptions<GraphDbContext>(databaseName: $"{nameof(EfCoreBulkTest)}_Graph"));
        db.Database.EnsureDeleted();
    }
}