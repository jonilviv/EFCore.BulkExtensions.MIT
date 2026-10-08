using EFCore.BulkOperations.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EFCore.BulkOperations.SqlServer.IntegrationTests;

public sealed class SqlQueryBuilderSqlServerTests
{
    [Fact]
    public void MergeTableInsertTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.Insert;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string result = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name]) VALUES (S.[Name]);";

        Assert.Equal(result, expected);
    }

    [Fact]
    public void MergeTableInsertOrUpdateTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string result = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name]) VALUES (S.[Name]) " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name] " +
                          "EXCEPT SELECT T.[Name]) " +
                          "THEN UPDATE SET T.[Name] = S.[Name];";

        Assert.Equal(result, expected);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithOnConflictUpdateWhereSqlTest()
    {
        Func<string, string, string> whereSql = (existing, inserted) => $"{inserted}.ItemTimestamp > {existing}.ItemTimestamp";
        TableInfo tableInfo = GetTestTableInfo(whereSql);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string actual = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name]) VALUES (S.[Name]) " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name] " +
                          "EXCEPT SELECT T.[Name]) " +
                          $"AND S.ItemTimestamp > T.ItemTimestamp " +
                          "THEN UPDATE SET T.[Name] = S.[Name];";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithCompareTest()
    {
        TableInfo tableInfo = GetTestTableWithCompareInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string result = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name], [TimeUpdated]) VALUES (S.[Name], S.[TimeUpdated]) " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name] " +
                          "EXCEPT SELECT T.[Name]) " +
                          "THEN UPDATE SET T.[Name] = S.[Name], T.[TimeUpdated] = S.[TimeUpdated];";

        Assert.Equal(result, expected);
    }

    [Fact]
    public void MergeTableInsertOrUpdateWithCompareAndOnConflictUpdateWhereSqlTest()
    {
        Func<string, string, string> whereSql = (existing, inserted) => $"{inserted}.ItemTimestamp > {existing}.ItemTimestamp";
        TableInfo tableInfo = GetTestTableWithCompareInfo(whereSql);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string actual = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name], [TimeUpdated]) VALUES (S.[Name], S.[TimeUpdated]) " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name] " +
                          "EXCEPT SELECT T.[Name]) " +
                          "AND S.ItemTimestamp > T.ItemTimestamp " +
                          "THEN UPDATE SET T.[Name] = S.[Name], T.[TimeUpdated] = S.[TimeUpdated];";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableInsertOrUpdateNoUpdateTest()
    {
        TableInfo tableInfo = GetTestTableWithNoUpdateInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string result = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name], [TimeUpdated]) VALUES (S.[Name], S.[TimeUpdated]) " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name], S.[TimeUpdated] " +
                          "EXCEPT SELECT T.[Name], T.[TimeUpdated]) " +
                          "THEN UPDATE SET T.[Name] = S.[Name];";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MergeTableInsertOrUpdateNoUpdateWithOnConflictUpdateWhereSqlTest()
    {
        Func<string, string, string> whereSql = (existing, inserted) => $"{inserted}.ItemTimestamp > {existing}.ItemTimestamp";
        TableInfo tableInfo = GetTestTableWithNoUpdateInfo(whereSql);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.InsertOrUpdate;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string actual = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN NOT MATCHED BY TARGET THEN INSERT ([Name], [TimeUpdated]) VALUES (S.[Name], S.[TimeUpdated]) " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name], S.[TimeUpdated] " +
                          "EXCEPT SELECT T.[Name], T.[TimeUpdated]) " +
                          "AND S.ItemTimestamp > T.ItemTimestamp " +
                          "THEN UPDATE SET T.[Name] = S.[Name];";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MergeTableUpdateTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.Update;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string result = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name] " +
                          "EXCEPT SELECT T.[Name]) " +
                          "THEN UPDATE SET T.[Name] = S.[Name];";

        Assert.Equal(result, expected);
    }

    [Fact]
    public void MergeTableUpdateWithOnConflictUpdateWhereSqlTest()
    {
        Func<string, string, string> whereSql = (existing, inserted) => $"{inserted}.ItemTimestamp > {existing}.ItemTimestamp";
        TableInfo tableInfo = GetTestTableInfo(whereSql);
        string identityCol = "ItemId";
        tableInfo.IdentityColumnName = identityCol;
        OperationType operation = OperationType.Update;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string actual = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN MATCHED AND EXISTS (SELECT S.[Name] " +
                          "EXCEPT SELECT T.[Name]) " +
                          "AND S.ItemTimestamp > T.ItemTimestamp " +
                          "THEN UPDATE SET T.[Name] = S.[Name];";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectJoinTableReadTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        string nameCol = nameof(Item.Name);
        var updateBy = new List<string> { nameCol };
        tableInfo.BulkConfig.UpdateByProperties = updateBy;
        string result = SqlQueryBuilder.SelectJoinTable(tableInfo);

        string expected = "SELECT S.[ItemId], S.[Name] FROM [dbo].[Item] AS S " +
                          "JOIN [dbo].[ItemTemp1234] AS J " +
                          "ON S.[ItemId] = J.[ItemId]";

        Assert.Equal(result, expected);
    }

    [Fact]
    public void MergeTableDeleteDeleteTest()
    {
        TableInfo tableInfo = GetTestTableInfo();
        OperationType operation = OperationType.Delete;
        MergeTableResult mergeResult = SqlQueryBuilder.MergeTable<Item>(null, tableInfo, operation);
        string result = mergeResult.Sql;

        string expected = "MERGE [dbo].[Item] WITH (HOLDLOCK) AS T USING (SELECT TOP 0 * FROM [dbo].[ItemTemp1234] ORDER BY [ItemId]) AS S " +
                          "ON T.[ItemId] = S.[ItemId] " +
                          "WHEN MATCHED THEN DELETE;";

        Assert.Equal(result, expected);
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

    private static TableInfo GetTestTableWithCompareInfo(Func<string, string, string>? onConflictUpdateWhereSql = null)
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
        string timeUpdatedText = nameof(Item.TimeUpdated);

        string pkKey = tableInfo.PrimaryKeysPropertyColumnNameDict.Keys.First();
        string pkVal = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.First();
        tableInfo.PropertyColumnNamesDict.Add(pkKey, pkVal);
        tableInfo.PropertyColumnNamesDict.Add(nameText, nameText);
        tableInfo.PropertyColumnNamesDict.Add(timeUpdatedText, timeUpdatedText);

        tableInfo.PropertyColumnNamesCompareDict =
            tableInfo.PropertyColumnNamesDict.Where(p => p.Key != timeUpdatedText).ToDictionary(p => p.Key, p => p.Value);

        tableInfo.PropertyColumnNamesUpdateDict = tableInfo.PropertyColumnNamesDict;

        return tableInfo;
    }

    private static TableInfo GetTestTableWithNoUpdateInfo(Func<string, string, string>? onConflictUpdateWhereSql = null)
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
        string timeUpdatedText = nameof(Item.TimeUpdated);

        string pkKey = tableInfo.PrimaryKeysPropertyColumnNameDict.Keys.First();
        string pkVal = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.First();
        tableInfo.PropertyColumnNamesDict.Add(pkKey, pkVal);
        tableInfo.PropertyColumnNamesDict.Add(nameText, nameText);
        tableInfo.PropertyColumnNamesDict.Add(timeUpdatedText, timeUpdatedText);

        tableInfo.PropertyColumnNamesCompareDict = tableInfo.PropertyColumnNamesDict;

        tableInfo.PropertyColumnNamesUpdateDict =
            tableInfo.PropertyColumnNamesDict.Where(p => p.Key != timeUpdatedText).ToDictionary(p => p.Key, p => p.Value);

        return tableInfo;
    }
}
