using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

namespace EFCore.BulkOperations.SqlAdapters;

public abstract class SqlDefaultDialect : IQueryBuilderSpecialization
{
    private static readonly int __selectStatementLength = "SELECT".Length;

    public abstract char EscL { get; }

    public abstract char EscR { get; }

    public virtual string? DefaultSchema => null;

    public virtual bool MatchEntitiesByPosition => false;

    public virtual bool SupportsGraphOperations => true;

    public virtual bool UseValueGenerationStrategyForIdentity => true;

    public virtual bool DetectIdentityByIntegerPrimaryKey => false;

    protected virtual string BatchSqlTableAliasSplitEscapeStart => "[";

    protected virtual string BatchSqlTableAliasSplitEscapeEnd => "]";

    public virtual List<object> ReloadSqlParameters(DbContext context, List<object> sqlParameters)
    {
        var sqlParametersReloaded = new List<object>();

        foreach (object parameter in sqlParameters)
        {
            var sqlParameter = (IDbDataParameter)parameter;

            try
            {
                if (sqlParameter.DbType == DbType.DateTime)
                {
                    sqlParameter.DbType = DbType.DateTime2; // sets most specific parameter DbType possible for so that precision is not lost
                }
            }
            catch (Exception ex)
            {
                string noMappingText = "No mapping exists from object type "; // Fixes for Batch ops on PostgreSQL with:

                if (!ex.Message.StartsWith(noMappingText + "System.Collections.Generic.List") &&             // - Contains
                    !ex.Message.StartsWith(noMappingText + "System.Int32[]") &&                              // - Contains
                    !ex.Message.StartsWith(noMappingText + "System.Int64[]") &&                              // - Contains
                    !ex.Message.StartsWith(noMappingText + typeof(System.Text.Json.JsonElement).FullName) && // - JsonElement param
                    !ex.Message.StartsWith(noMappingText + typeof(System.Text.Json.JsonDocument).FullName))  // - JsonElement param
                {
                    throw;
                }
            }
            sqlParametersReloaded.Add(sqlParameter);
        }

        return sqlParametersReloaded;
    }


    public virtual string GetBinaryExpressionAddOperation(BinaryExpression binaryExpression)
    {
        return "+";
    }

    public virtual BatchTableAliasTopStatement GetBatchSqlReformatTableAliasAndTopStatement(string sqlQuery)
    {
        string escapeSymbolEnd = BatchSqlTableAliasSplitEscapeEnd;
        string escapeSymbolStart = BatchSqlTableAliasSplitEscapeStart;
        string tableAliasEnd = sqlQuery[__selectStatementLength..sqlQuery.IndexOf(escapeSymbolEnd, StringComparison.Ordinal)]; // " TOP(10) [table_alias" / " [table_alias" : " table_alias"
        int tableAliasStartIndex = tableAliasEnd.IndexOf(escapeSymbolStart, StringComparison.Ordinal);
        string tableAlias = tableAliasEnd[(tableAliasStartIndex + escapeSymbolStart.Length)..]; // "table_alias"
        string topStatement = tableAliasEnd[..tableAliasStartIndex].TrimStart(); // "TOP(10) " / if TOP not present in query this will be a Substring(0,0) == ""
        var result = new BatchTableAliasTopStatement(tableAlias, topStatement);

        return result;
    }

    public virtual ExtractedTableAlias GetBatchSqlExtractTableAliasFromQuery(string fullQuery, string tableAlias, string tableAliasSuffixAs)
    {
        var result = new ExtractedTableAlias
        {
            TableAlias = tableAlias,
            TableAliasSuffixAs = tableAliasSuffixAs,
            Sql = fullQuery
        };

        return result;
    }
}