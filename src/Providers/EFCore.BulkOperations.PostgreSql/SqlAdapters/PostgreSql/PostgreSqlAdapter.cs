using EFCore.BulkOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.SqlAdapters.PostgreSql;

/// <inheritdoc/>
public sealed class PostgreSqlAdapter : ISqlOperationsAdapter
{
    /// <inheritdoc/>
    #region Methods
    // Insert
    public void Insert<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress)
    {
        NpgsqlConnection? connection = (NpgsqlConnection?)SqlAdaptersMapping.DbServer(context).DbConnection;
        bool closeConnectionInternally = false;

        if (connection == null)
        {
            OpenedNpgsqlConnection openedConnection = OpenAndGetNpgsqlConnection(context);
            connection = openedConnection.Connection;
            closeConnectionInternally = openedConnection.CloseInternally;
        }

        try
        {
            OperationType operationType = tableInfo.InsertToTempTable ? OperationType.InsertOrUpdate : OperationType.Insert;
            string sqlCopy = SqlQueryBuilderPostgreSql.InsertIntoTable(tableInfo, operationType);
            using NpgsqlBinaryImporter writer = connection.BeginBinaryImport(sqlCopy);
            List<string> propertiesNames = GetInsertProperties(tableInfo);
            int entitiesCopiedCount = 0;

            foreach (T entity in entities)
            {
                writer.StartRow();

                foreach (string propertyName in propertiesNames)
                {
                    if (!TryGetConvertedPropertyValue(tableInfo, propertyName, entity, operationType, out object? propertyValue, out string columnType))
                    {
                        continue;
                    }

                    writer.Write(propertyValue, columnType);
                }

                entitiesCopiedCount++;

                if (progress != null && entitiesCopiedCount % tableInfo.BulkConfig.NotifyAfter == 0)
                {
                    progress?.Invoke(ProgressHelper.GetProgress(entities.Count, entitiesCopiedCount));
                }
            }

            writer.Complete();
        }
        finally
        {
            if (closeConnectionInternally)
            {
                connection.Close();
            }
        }
    }

    /// <inheritdoc/>
    public async Task InsertAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress, CancellationToken cancellationToken)
    {
        NpgsqlConnection? connection = (NpgsqlConnection?)SqlAdaptersMapping.DbServer(context).DbConnection;
        bool closeConnectionInternally = false;

        if (connection == null)
        {
            OpenedNpgsqlConnection openedConnection = await OpenAndGetNpgsqlConnectionAsync(context, cancellationToken).ConfigureAwait(false);
            connection = openedConnection.Connection;
            closeConnectionInternally = openedConnection.CloseInternally;
        }

        try
        {
            OperationType operationType = tableInfo.InsertToTempTable ? OperationType.InsertOrUpdate : OperationType.Insert;
            string sqlCopy = SqlQueryBuilderPostgreSql.InsertIntoTable(tableInfo, operationType);
            using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(sqlCopy, cancellationToken).ConfigureAwait(false);
            List<string> propertiesNames = GetInsertProperties(tableInfo);
            int entitiesCopiedCount = 0;

            foreach (T entity in entities)
            {
                await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);

                foreach (string propertyName in propertiesNames)
                {
                    if (!TryGetConvertedPropertyValue(tableInfo, propertyName, entity, operationType, out object? propertyValue, out string columnType))
                    {
                        continue;
                    }

                    await writer.WriteAsync(propertyValue, columnType, cancellationToken).ConfigureAwait(false);
                }

                entitiesCopiedCount++;

                if (progress != null && entitiesCopiedCount % tableInfo.BulkConfig.NotifyAfter == 0)
                {
                    progress?.Invoke(ProgressHelper.GetProgress(entities.Count, entitiesCopiedCount));
                }
            }

            await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (closeConnectionInternally)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static List<string> GetInsertProperties(TableInfo tableInfo)
    {
        string? uniqueColumnName = tableInfo.PrimaryKeysPropertyColumnNameDict.Values.ToList().FirstOrDefault();
        bool doKeepIdentity = tableInfo.BulkConfig.BulkCopyOptions.HasFlag(BulkCopyOptions.KeepIdentity);
        IEnumerable<KeyValuePair<string, string>> propertiesColumnDict = ((tableInfo.InsertToTempTable || doKeepIdentity) && tableInfo.IdentityColumnName == uniqueColumnName)
            ? tableInfo.PropertyColumnNamesDict
            : tableInfo.PropertyColumnNamesDict.Where(a => a.Value != tableInfo.IdentityColumnName);

        List<string> propertiesNames = propertiesColumnDict.Select(a => a.Key).ToList();

        return propertiesNames;
    }

    private static bool TryGetConvertedPropertyValue<T>(TableInfo tableInfo, string propertyName, T entity, OperationType operationType, out object? propertyValue, out string columnType)
    {
        if (operationType == OperationType.Insert
            && tableInfo.DefaultValueProperties.Contains(propertyName)
            && !tableInfo.PrimaryKeysPropertyColumnNameDict.ContainsKey(propertyName))
        {
            propertyValue = null;
            columnType = string.Empty;

            return false;
        }

        propertyValue = GetPropertyValue(tableInfo, propertyName, entity);
        string propertyColumnName = tableInfo.PropertyColumnNamesDict.ContainsKey(propertyName) ? tableInfo.PropertyColumnNamesDict[propertyName] : string.Empty;
        columnType = tableInfo.ColumnNamesTypesDict[propertyColumnName];

        if (columnType.StartsWith("character"))
        {
            columnType = "character";
        }
        else if (columnType.StartsWith("varchar"))
        {
            columnType = "varchar";
        }
        else if (columnType.StartsWith("numeric") && columnType != "numeric[]")
        {
            columnType = "numeric";
        }

        Dictionary<string, ValueConverter> convertibleDict = tableInfo.ConvertibleColumnConverterDict;

        if (convertibleDict.TryGetValue(propertyColumnName, out ValueConverter? converter))
        {
            if (propertyValue != null)
            {
                if (converter.ModelClrType.IsEnum)
                {
                    Type clrType = converter.ProviderClrType;

                    if (clrType == typeof(byte))
                    {
                        propertyValue = (byte)propertyValue;
                    }

                    if (clrType == typeof(short))
                    {
                        propertyValue = (short)propertyValue;
                    }

                    if (clrType == typeof(int))
                    {
                        propertyValue = (int)propertyValue;
                    }

                    if (clrType == typeof(long))
                    {
                        propertyValue = (long)propertyValue;
                    }

                    if (clrType == typeof(string))
                    {
                        propertyValue = propertyValue.ToString();
                    }
                }
                else
                {
                    propertyValue = converter.ConvertToProvider.Invoke(propertyValue);
                }
            }
        }

        return true;
    }

    static object? GetPropertyValue<T>(TableInfo tableInfo, string propertyName, T entity)
    {
        if (!tableInfo.FastPropertyDict.ContainsKey(propertyName.Replace('.', '_')) || entity is null)
        {
            return null;
        }

        object? propertyValue = entity;
        string fullPropertyName = string.Empty;

        foreach (SpanSplitExtensions.TokenSplitEntry<char> entry in propertyName.AsSpan().Split(".".AsSpan()))
        {
            if (propertyValue == null)
            {
                return null;
            }

            if (fullPropertyName.Length > 0)
            {
                fullPropertyName += $"_{entry.Token}";
            }
            else
            {
                fullPropertyName = new string(entry.Token);
            }

            propertyValue = tableInfo.FastPropertyDict[fullPropertyName].Get(propertyValue);
        }

        return propertyValue;
    }

    /// <inheritdoc/>
    public void Merge<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, OperationType operationType, Action<decimal>? progress) where T : class
    {
        if (tableInfo.BulkConfig.CustomSourceTableName == null)
        {
            tableInfo.InsertToTempTable = true;
            string sqlCreateTableCopy = SqlQueryBuilderPostgreSql.CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
            context.Database.ExecuteSqlRaw(sqlCreateTableCopy);
        }

        UniqueConstraintCheckResult uniqueCheck = CheckHasExplicitUniqueConstrain(context, tableInfo);
        bool hasUniqueConstrain = uniqueCheck.HasUniqueConstraint;
        bool connectionOpenedInternally = uniqueCheck.ConnectionOpenedInternally;

        if (!hasUniqueConstrain)
        {
            if (tableInfo.EntityPkPropertyColumnNameDict == tableInfo.PrimaryKeysPropertyColumnNameDict)
            {
                hasUniqueConstrain = true;
            }
        }

        bool doDropUniqueConstrain = false;

        try
        {
            if (tableInfo.BulkConfig.CustomSourceTableName == null)
            {
                Insert(context, type, entities, tableInfo, progress);
            }

            if (!hasUniqueConstrain)
            {
                string createUniqueIndex = SqlQueryBuilderPostgreSql.CreateUniqueIndex(tableInfo);
                string createUniqueConstrain = SqlQueryBuilderPostgreSql.CreateUniqueConstrain(tableInfo);

                context.Database.ExecuteSqlRaw(createUniqueIndex);
                context.Database.ExecuteSqlRaw(createUniqueConstrain);
                doDropUniqueConstrain = true;
            }

            string sqlMergeTable = SqlQueryBuilderPostgreSql.MergeTable<T>(tableInfo, operationType);

            if (operationType != OperationType.Read && (!tableInfo.BulkConfig.SetOutputIdentity || operationType == OperationType.Delete))
            {
                context.Database.ExecuteSqlRaw(sqlMergeTable);
            }
            else
            {
                ProcessMergeOutputEntities(context, type, entities, tableInfo, sqlMergeTable);
            }
        }
        finally
        {
            try
            {
                if (doDropUniqueConstrain)
                {
                    string dropUniqueConstrain = SqlQueryBuilderPostgreSql.DropUniqueConstrain(tableInfo);
                    context.Database.ExecuteSqlRaw(dropUniqueConstrain);
                }

                if (!tableInfo.BulkConfig.UseTempDb)
                {
                    if (tableInfo.BulkConfig.CustomSourceTableName == null)
                    {
                        string sqlDropTable = SqlQueryBuilderPostgreSql.DropTable(tableInfo.FullTempTableName);
                        context.Database.ExecuteSqlRaw(sqlDropTable);
                    }
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "25P02")
            {
            }

            if (connectionOpenedInternally)
            {
                NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
                connection.Close();
            }
        }
    }

    /// <inheritdoc/>
    public async Task MergeAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, OperationType operationType, Action<decimal>? progress, CancellationToken cancellationToken) where T : class
    {
        if (tableInfo.BulkConfig.CustomSourceTableName == null)
        {
            tableInfo.InsertToTempTable = true;
            string sqlCreateTableCopy = SqlQueryBuilderPostgreSql.CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
            await context.Database.ExecuteSqlRawAsync(sqlCreateTableCopy, cancellationToken).ConfigureAwait(false);
        }

        UniqueConstraintCheckResult uniqueCheck = await CheckHasExplicitUniqueConstrainAsync(context, tableInfo, cancellationToken).ConfigureAwait(false);
        bool hasUniqueConstrain = uniqueCheck.HasUniqueConstraint;
        bool connectionOpenedInternally = uniqueCheck.ConnectionOpenedInternally;

        if (!hasUniqueConstrain)
        {
            if (tableInfo.EntityPkPropertyColumnNameDict == tableInfo.PrimaryKeysPropertyColumnNameDict)
            {
                hasUniqueConstrain = true;
            }
        }

        bool doDropUniqueConstrain = false;

        try
        {
            if (tableInfo.BulkConfig.CustomSourceTableName == null)
            {
                await InsertAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);
            }

            if (!hasUniqueConstrain)
            {
                string createUniqueIndex = SqlQueryBuilderPostgreSql.CreateUniqueIndex(tableInfo);
                string createUniqueConstrain = SqlQueryBuilderPostgreSql.CreateUniqueConstrain(tableInfo);

                await context.Database.ExecuteSqlRawAsync(createUniqueIndex, cancellationToken).ConfigureAwait(false);
                await context.Database.ExecuteSqlRawAsync(createUniqueConstrain, cancellationToken).ConfigureAwait(false);
                doDropUniqueConstrain = true;
            }

            string sqlMergeTable = SqlQueryBuilderPostgreSql.MergeTable<T>(tableInfo, operationType);

            if (operationType != OperationType.Read && (!tableInfo.BulkConfig.SetOutputIdentity || operationType == OperationType.Delete))
            {
                await context.Database.ExecuteSqlRawAsync(sqlMergeTable, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                ProcessMergeOutputEntities(context, type, entities, tableInfo, sqlMergeTable);
            }
        }
        finally
        {
            try
            {
                if (doDropUniqueConstrain)
                {
                    string dropUniqueConstrain = SqlQueryBuilderPostgreSql.DropUniqueConstrain(tableInfo);
                    await context.Database.ExecuteSqlRawAsync(dropUniqueConstrain, cancellationToken).ConfigureAwait(false);
                }

                if (!tableInfo.BulkConfig.UseTempDb)
                {
                    if (tableInfo.BulkConfig.CustomSourceTableName == null)
                    {
                        string sqlDropTable = SqlQueryBuilderPostgreSql.DropTable(tableInfo.FullTempTableName);
                        await context.Database.ExecuteSqlRawAsync(sqlDropTable, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "25P02")
            {
            }

            if (connectionOpenedInternally)
            {
                NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static void ProcessMergeOutputEntities<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, string sqlMergeTable) where T : class
    {
        string sqlMergeTableOutput = sqlMergeTable.TrimEnd(';');
        List<T> outputEntities = tableInfo.LoadOutputEntities<T>(context, type, sqlMergeTableOutput);
        tableInfo.UpdateReadEntities(entities, outputEntities, context);
    }

    /// <inheritdoc/>
    public void Read<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress) where T : class
        => Merge(context, type, entities, tableInfo, OperationType.Read, progress);

    /// <inheritdoc/>
    public async Task ReadAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress, CancellationToken cancellationToken) where T : class
        => await MergeAsync(context, type, entities, tableInfo, OperationType.Read, progress, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public void Truncate(DbContext context, TableInfo tableInfo)
    {
        string sqlTruncateTable = SqlQueryBuilderPostgreSql.TruncateTable(tableInfo.FullTableName);
        context.Database.ExecuteSqlRaw(sqlTruncateTable);
    }

    /// <inheritdoc/>
    public async Task TruncateAsync(DbContext context, TableInfo tableInfo, CancellationToken cancellationToken)
    {
        string sqlTruncateTable = SqlQueryBuilderPostgreSql.TruncateTable(tableInfo.FullTableName);
        await context.Database.ExecuteSqlRawAsync(sqlTruncateTable, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region Connection
    internal static async Task<OpenedNpgsqlConnection> OpenAndGetNpgsqlConnectionAsync(DbContext context, CancellationToken cancellationToken)
    {
        bool closeConnectionInternally = false;
        NpgsqlConnection npgsqlConnection = (NpgsqlConnection)context.Database.GetDbConnection();

        if (npgsqlConnection.State != ConnectionState.Open)
        {
            await npgsqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            closeConnectionInternally = true;
        }

        return new OpenedNpgsqlConnection(npgsqlConnection, closeConnectionInternally);
    }

    internal static OpenedNpgsqlConnection OpenAndGetNpgsqlConnection(DbContext context)
    {
        bool closeConnectionInternally = false;
        NpgsqlConnection npgsqlConnection = (NpgsqlConnection)context.Database.GetDbConnection();

        if (npgsqlConnection.State != ConnectionState.Open)
        {
            npgsqlConnection.Open();
            closeConnectionInternally = true;
        }

        return new OpenedNpgsqlConnection(npgsqlConnection, closeConnectionInternally);
    }
    #endregion

    internal static UniqueConstraintCheckResult CheckHasExplicitUniqueConstrain(DbContext context, TableInfo tableInfo)
    {
        string countUniqueConstrain = SqlQueryBuilderPostgreSql.CountUniqueConstrain(tableInfo);
        OpenedNpgsqlConnection openedConnection = OpenAndGetNpgsqlConnection(context);
        DbConnection connection = openedConnection.Connection;
        bool connectionOpenedInternally = openedConnection.CloseInternally;
        bool hasUniqueConstrain = false;

        using (DbCommand command = connection.CreateCommand())
        {
            command.CommandText = countUniqueConstrain;
            using DbDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    hasUniqueConstrain = (long)reader[0] == 1;
                }
            }
        }

        var result = new UniqueConstraintCheckResult(hasUniqueConstrain, connectionOpenedInternally);

        return result;
    }

    internal static async Task<UniqueConstraintCheckResult> CheckHasExplicitUniqueConstrainAsync(DbContext context, TableInfo tableInfo, CancellationToken cancellationToken)
    {
        string countUniqueConstrain = SqlQueryBuilderPostgreSql.CountUniqueConstrain(tableInfo);
        OpenedNpgsqlConnection openedConnection = await OpenAndGetNpgsqlConnectionAsync(context, cancellationToken).ConfigureAwait(false);
        DbConnection connection = openedConnection.Connection;
        bool connectionOpenedInternally = openedConnection.CloseInternally;
        bool hasUniqueConstrain = false;

        using (DbCommand command = connection.CreateCommand())
        {
            command.CommandText = countUniqueConstrain;
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (reader.HasRows)
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    hasUniqueConstrain = (long)reader[0] == 1;
                }
            }
        }

        var result = new UniqueConstraintCheckResult(hasUniqueConstrain, connectionOpenedInternally);

        return result;
    }
}