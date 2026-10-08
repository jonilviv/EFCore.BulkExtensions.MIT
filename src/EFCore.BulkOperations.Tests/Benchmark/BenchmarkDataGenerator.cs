using System;
using System.Collections.Generic;

namespace EFCore.BulkOperations.Tests.Benchmark;

public sealed class BenchmarkDataGenerator
{
    public static List<BenchmarkItem> GenerateItems(int count)
    {
        var items = new List<BenchmarkItem>(count);
        int year = 2026;
        int month = 1;
        int day = 1;
        int hour = 0;
        int minute = 0;
        int second = 0;
        var baseDate = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

        for (int i = 1; i <= count; i++)
        {
            var guid = Guid.NewGuid();
            string name = "Item_" + i;
            string description = "Synthetic payload data for benchmark item number " + i;
            int quantity = i % 100;
            decimal priceModulo = i % 50;
            decimal basePrice = 10.50m;
            decimal price = basePrice + priceModulo;
            double seconds = i;
            DateTime created = baseDate.AddSeconds(seconds);

            var item = new BenchmarkItem
            {
                Id = i,
                ExternalId = guid,
                Name = name,
                Description = description,
                Quantity = quantity,
                Price = price,
                CreatedAt = created
            };

            items.Add(item);
        }

        return items;
    }

    public static void MutateItemsForUpdate(List<BenchmarkItem> items)
    {
        DateTime now = DateTime.UtcNow;

        for (int i = 0; i < items.Count; i++)
        {
            BenchmarkItem item = items[i];
            long id = item.Id;
            item.Description = "Updated synthetic payload data for item number " + id;
            int addedQuantity = 10;
            item.Quantity = item.Quantity + addedQuantity;
            decimal addedPrice = 5.00m;
            item.Price = item.Price + addedPrice;
            item.UpdatedAt = now;
        }
    }
}
