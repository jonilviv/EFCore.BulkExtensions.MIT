using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace EFCore.BulkOperations;

/// <summary> Provides config for batch update/create </summary>
public sealed class BatchUpdateCreateBodyData
{
    private readonly BulkConfig __tableInfoBulkConfig;
    private readonly Dictionary<Type, TableInfo> __tableInfoLookup;

    /// <summary> Creates an instance of BatchUpdateCreateBodyData used to provide a config for batch updates and creations </summary>
    public BatchUpdateCreateBodyData(
        string baseSql,
        DbContext dbContext,
        IEnumerable<object> innerParameters,
        IQueryable query,
        Type rootType,
        string tableAlias,
        LambdaExpression updateExpression)
    {
        BaseSql = baseSql;
        DbContext = dbContext;
        Query = query;
        RootInstanceParameterName = updateExpression.Parameters?.FirstOrDefault()?.Name;
        RootType = rootType;
        TableAlias = tableAlias;
        TableAliasesInUse = new List<string>();
        UpdateColumnsSql = new StringBuilder();
        UpdateExpression = updateExpression;

        __tableInfoBulkConfig = new BulkConfig();
        __tableInfoLookup = new Dictionary<Type, TableInfo>();

        TableInfo tableInfo = TableInfo.CreateInstance(dbContext, rootType, Array.Empty<object>(), OperationType.Read, __tableInfoBulkConfig);
        __tableInfoLookup.Add(rootType, tableInfo);

        SqlParameters = new List<object>(innerParameters);

        foreach (Match match in BatchUtil.TableAliasPattern.Matches(baseSql))
        {
            TableAliasesInUse.Add(match.Groups[2].Value);
        }
    }

    public string BaseSql { get; }
    public DbContext DbContext { get; }
    public IQueryable Query { get; }
    public string? RootInstanceParameterName { get; }
    public Type RootType { get; }
    public List<object> SqlParameters { get; }
    public string TableAlias { get; }
    public List<string> TableAliasesInUse { get; }
    public StringBuilder UpdateColumnsSql { get; }
    public LambdaExpression UpdateExpression { get; }

    public TableInfo? GetTableInfoForType(Type typeToLookup)
    {
        if (__tableInfoLookup.TryGetValue(typeToLookup, out TableInfo? tableInfo))
        {
            return tableInfo;
        }

        tableInfo = TableInfo.CreateInstance(DbContext, typeToLookup, Array.Empty<object>(), OperationType.Read, __tableInfoBulkConfig);

        if (tableInfo != null)
        {
            __tableInfoLookup.Add(typeToLookup, tableInfo);
        }

        return tableInfo;
    }
}