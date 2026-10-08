using EFCore.BulkOperations.SqlAdapters.PostgreSql;
using EFCore.BulkOperations.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EFCore.BulkOperations.PostgreSql.IntegrationTests;

public sealed class SqlQueryBuilderPostgreSqlTests
{
    [Fact]
    public void MergeTableInsertOrUpdateWithoutOnConflictUpdateWhereSqlTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        string actual = SqlQueryBuilderPostgreSql.MergeTable<Item>(tableInfo, operation);

        string expected = @"INSERT INTO ""dbo"".""Item"" (""ItemId"", ""Name"") " +
                          @"(SELECT ""ItemId"", ""Name"" FROM ""dbo"".""ItemTemp1234"") " +
                          @"ON CONFLICT (""ItemId"") DO UPDATE SET ""Name"" = EXCLUDED.""Name"";";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithOnConflictUpdateWhereSqlTest()
    {
        Func<string, string, string> whereSql = (existing, inserted) => $"{inserted}.ItemTimestamp > {existing}.ItemTimestamp";
        TableInfo tableInfo = GetTestTableInfo(whereSql);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        string actual = SqlQueryBuilderPostgreSql.MergeTable<Item>(tableInfo, operation);

        string expected = @"INSERT INTO ""dbo"".""Item"" (""ItemId"", ""Name"") " +
                          @"(SELECT ""ItemId"", ""Name"" FROM ""dbo"".""ItemTemp1234"") " +
                          @"ON CONFLICT (""ItemId"") DO UPDATE SET ""Name"" = EXCLUDED.""Name"" " +
                          @"WHERE EXCLUDED.ItemTimestamp > ""dbo"".""Item"".ItemTimestamp;";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithInsertOnlyTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        tableInfo.PropertyColumnNamesUpdateDict = new();
        OperationType operation = OperationType.InsertOrUpdate;
        string actual = SqlQueryBuilderPostgreSql.MergeTable<Item>(tableInfo, operation);

        string expected = @"INSERT INTO ""dbo"".""Item"" (""ItemId"", ""Name"") " +
                          @"(SELECT ""ItemId"", ""Name"" FROM ""dbo"".""ItemTemp1234"") " +
                          @"ON CONFLICT (""ItemId"") DO NOTHING;";

        Assert.Equal(expected, actual);
    }

    private static TableInfo GetTestTableInfo(Func<string, string, string>? onConflictUpdateWhereSql = null)
    {
        var config = new BulkConfig
        {
            OnConflictUpdateWhereSql = onConflictUpdateWhereSql
        };
        string pkName = nameof(Item.ItemId);
        var pkDict = new Dictionary<string, string> { { pkName, pkName } };
        string tableName = nameof(Item);
        string tempSuffix = "Temp1234";
        string tempTable = tableName + tempSuffix;

        var tableInfo = new TableInfo
        {
            EscL = "\"",
            EscR = "\"",
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
