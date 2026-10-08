using EFCore.BulkOperations.SqlAdapters.SQLite;
using EFCore.BulkOperations.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EFCore.BulkOperations.SQLite.IntegrationTests;

public sealed class SqlQueryBuilderSqliteTests
{
    [Fact]
    public void MergeTableInsertOrUpdateWithoutOnConflictWithIdentityUpdateWhereSqlTest()
    {
        BulkCopyOptions options = BulkCopyOptions.KeepIdentity;
        TableInfo tableInfo = GetTestTableInfo(null, options);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        string actual = SqlQueryBuilderSqlite.InsertIntoTable(tableInfo, operation);

        string expected = @"INSERT INTO [Item] ([ItemId], [Name]) " +
                          @"VALUES (@ItemId, @Name) " +
                          @"ON CONFLICT([ItemId]) DO UPDATE SET [ItemId] = @ItemId, [Name] = @Name " +
                          @"WHERE [ItemId] = @ItemId;";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithoutOnConflictWithoutIdentityUpdateWhereSqlTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        string actual = SqlQueryBuilderSqlite.InsertIntoTable(tableInfo, operation);

        string expected = @"INSERT INTO [Item] ([Name]) " +
                          @"VALUES (@Name) " +
                          @"ON CONFLICT([ItemId]) DO UPDATE SET [Name] = @Name " +
                          @"WHERE [ItemId] = @ItemId;";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithOnConflictUpdateWhereSqlTest()
    {
        Func<string, string, string> whereSql = (existing, inserted) => $"{inserted}.ItemTimestamp > {existing}.ItemTimestamp";
        BulkCopyOptions options = BulkCopyOptions.KeepIdentity;
        TableInfo tableInfo = GetTestTableInfo(whereSql, options);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        string actual = SqlQueryBuilderSqlite.InsertIntoTable(tableInfo, operation);

        string expected = @"INSERT INTO [Item] ([ItemId], [Name]) " +
                          @"VALUES (@ItemId, @Name) " +
                          @"ON CONFLICT([ItemId]) DO UPDATE SET [ItemId] = @ItemId, [Name] = @Name " +
                          @"WHERE [ItemId] = @ItemId AND excluded.ItemTimestamp > [Item].ItemTimestamp;";

        Assert.Equal(expected, actual);
    }

    private static TableInfo GetTestTableInfo(
        Func<string, string, string>? onConflictUpdateWhereSql = null,
        BulkCopyOptions? bulkCopyOptions = null)
    {
        BulkCopyOptions resolvedOptions = bulkCopyOptions ?? BulkCopyOptions.Default;
        var config = new BulkConfig
        {
            OnConflictUpdateWhereSql = onConflictUpdateWhereSql,
            BulkCopyOptions = resolvedOptions
        };
        string pkName = nameof(Item.ItemId);
        var pkDict = new Dictionary<string, string> { { pkName, pkName } };
        string tableName = nameof(Item);
        string tempSuffix = "Temp1234";
        string tempTable = tableName + tempSuffix;

        var tableInfo = new TableInfo
        {
            EscL = "[",
            EscR = "]",
            Schema = "dbo",
            TempSchema = "dbo",
            TableName = tableName,
            TempTableName = tempTable,
            TempTableSufix = tempSuffix,
            PrimaryKeysPropertyColumnNameDict = pkDict,
            BulkConfig = config
        };
        string nameText = nameof(Item.Name);

        string pkKey = tableInfo.PrimaryKeysPropertyColumnNameDict.Keys.First();
        string pkVal = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.First();
        tableInfo.PropertyColumnNamesDict.Add(pkKey, pkVal);
        tableInfo.PropertyColumnNamesDict.Add(nameText, nameText);
        tableInfo.PropertyColumnNamesCompareDict = tableInfo.PropertyColumnNamesDict;
        tableInfo.PropertyColumnNamesUpdateDict = tableInfo.PropertyColumnNamesDict;

        return tableInfo;
    }
}
