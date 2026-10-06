using EFCore.BulkOperations.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations;

internal static class SqlBulkOperation
{
    public static void Insert<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress)
    {
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        adapter.Insert(context, type, entities, tableInfo, progress);
    }

    public static async Task InsertAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress, CancellationToken cancellationToken)
    {
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        await adapter.InsertAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);
    }

    public static void Merge<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, OperationType operationType, Action<decimal>? progress) where T : class
    {
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        adapter.Merge(context, type, entities, tableInfo, operationType, progress);
    }

    public static async Task MergeAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, OperationType operationType, Action<decimal>? progress, CancellationToken cancellationToken = default) where T : class
    {
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        await adapter.MergeAsync(context, type, entities, tableInfo, operationType, progress, cancellationToken).ConfigureAwait(false);
    }

    public static void Read<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress) where T : class
    {
        if (tableInfo.BulkConfig.UseTempDb) // dropTempTableIfExists
        {
            string fullTempTableName = tableInfo.FullTempTableName;
            bool useTempDb = tableInfo.BulkConfig.UseTempDb;
            string dropTableSql = SqlQueryBuilder.DropTable(fullTempTableName, useTempDb);
            context.Database.ExecuteSqlRaw(dropTableSql);
        }
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        adapter.Read(context, type, entities, tableInfo, progress);
    }

    public static async Task ReadAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress, CancellationToken cancellationToken) where T : class
    {
        if (tableInfo.BulkConfig.UseTempDb) // dropTempTableIfExists
        {
            string fullTempTableName = tableInfo.FullTempTableName;
            bool useTempDb = tableInfo.BulkConfig.UseTempDb;
            string dropTableSql = SqlQueryBuilder.DropTable(fullTempTableName, useTempDb);
            await context.Database.ExecuteSqlRawAsync(dropTableSql, cancellationToken).ConfigureAwait(false);
        }
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        await adapter.ReadAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);
    }

    public static void Truncate(DbContext context, TableInfo tableInfo)
    {
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        adapter.Truncate(context, tableInfo);
    }

    public static async Task TruncateAsync(DbContext context, TableInfo tableInfo, CancellationToken cancellationToken)
    {
        ISqlOperationsAdapter adapter = SqlAdaptersMapping.CreateBulkOperationsAdapter(context);
        await adapter.TruncateAsync(context, tableInfo, cancellationToken).ConfigureAwait(false);
    }
}