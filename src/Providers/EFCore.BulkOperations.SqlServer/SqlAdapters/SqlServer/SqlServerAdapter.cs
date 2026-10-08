using EFCore.BulkOperations.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.SqlAdapters.SqlServer;

/// <inheritdoc/>
public sealed class SqlOperationsServerAdapter : ISqlOperationsAdapter
{
    internal static string CreateTableCopy(string existingTableName, string newTableName, TableInfo tableInfo, bool isOutputTable = false)
    {
        // TODO: (optionaly) if CalculateStats = True but SetOutputIdentity = False then Columns could be ommited from Create and from MergeOutput
        List<string> columnsNames = (isOutputTable ? tableInfo.OutputPropertyColumnNamesDict : tableInfo.PropertyColumnNamesDict).Values.ToList();

        if (tableInfo.TimeStampColumnName != null)
        {
            columnsNames.Remove(tableInfo.TimeStampColumnName);
        }

        string statsColumn = (tableInfo.BulkConfig.OutputTableHasSqlActionColumn && isOutputTable) ? $", CAST('' AS char(1)) AS [{tableInfo.SqlActionIud}] " : "";
        string indexMappingColumn = (tableInfo.BulkConfig.UseOriginalIndexToIdentityMappingColumn) ? $", CAST(-1 AS int) AS [{tableInfo.OriginalIndexColumnName}] " : "";
        string commaSeparatedColumns = SqlQueryBuilder.GetCommaSeparatedColumns(columnsNames, "T");
        string q = $"SELECT TOP 0 {commaSeparatedColumns} " + statsColumn + indexMappingColumn +
                $"INTO {newTableName} FROM {existingTableName} AS T " +
                $"LEFT JOIN {existingTableName} AS Source ON 1 = 0;"; // removes Identity constraint

        return q;
    }

    #region Methods

    /// <inheritdoc/>
    public void Insert<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress)
    {
        tableInfo.CheckToSetIdentityForPreserveOrder(tableInfo, entities);
        context.Database.OpenConnection();

        try
        {
            DbConnection connection = context.GetUnderlyingConnection(tableInfo.BulkConfig);
            IDbContextTransaction? transaction = context.Database.CurrentTransaction;
            SqlConnection sqlConnection = (SqlConnection)connection;

            using SqlBulkCopy sqlBulkCopy = GetSqlBulkCopy(sqlConnection, transaction, tableInfo.BulkConfig);
            const bool setColumnMapping = false;
            SetSqlBulkCopyConfig(sqlBulkCopy, tableInfo, entities, setColumnMapping, progress);

            try
            {
                DataTable dataTable = GetDataTable(context, type, entities, sqlBulkCopy, tableInfo);
                sqlBulkCopy.WriteToServer(dataTable);
            }
            catch (InvalidOperationException ex)
            {
                HandleColumnMappingException(context, tableInfo, ex);
                throw;
            }
        }
        finally
        {
            context.Database.CloseConnection();
        }

        if (!tableInfo.CreatedOutputTable)
        {
            tableInfo.CheckToSetIdentityForPreserveOrder(tableInfo, entities, reset: true);
        }
    }

    /// <inheritdoc/>
    public async Task InsertAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress, CancellationToken cancellationToken)
    {
        tableInfo.CheckToSetIdentityForPreserveOrder(tableInfo, entities);
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            DbConnection connection = context.GetUnderlyingConnection(tableInfo.BulkConfig);
            IDbContextTransaction? transaction = context.Database.CurrentTransaction;
            SqlConnection sqlConnection = (SqlConnection)connection;

            using SqlBulkCopy sqlBulkCopy = GetSqlBulkCopy(sqlConnection, transaction, tableInfo.BulkConfig);
            const bool setColumnMapping = false;
            SetSqlBulkCopyConfig(sqlBulkCopy, tableInfo, entities, setColumnMapping, progress);

            try
            {
                DataTable dataTable = GetDataTable(context, type, entities, sqlBulkCopy, tableInfo);
                await sqlBulkCopy.WriteToServerAsync(dataTable, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await HandleColumnMappingExceptionAsync(context, tableInfo, ex, cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        if (!tableInfo.CreatedOutputTable)
        {
            tableInfo.CheckToSetIdentityForPreserveOrder(tableInfo, entities, reset: true);
        }
    }

    private static string CheckTableExist(string fullTableName, bool isTempTable)
    {
        string q;

        if (isTempTable)
        {
            q = $"IF OBJECT_ID ('tempdb..[#{fullTableName.Split('#')[1]}', 'U') IS NOT NULL SELECT 1 AS res ELSE SELECT 0 AS res;";
        }
        else
        {
            q = $"IF OBJECT_ID ('{fullTableName}', 'U') IS NOT NULL SELECT 1 AS res ELSE SELECT 0 AS res;";
        }

        return q;
    }

    private static string TruncateTable(string tableName) => $"TRUNCATE TABLE {tableName};";

    private static DbCommand CreateCheckTableExistCommand(DbContext context, TableInfo tableInfo)
    {
        DbConnection sqlConnection = context.Database.GetDbConnection();
        IDbContextTransaction? currentTransaction = context.Database.CurrentTransaction;
        DbCommand command = sqlConnection.CreateCommand();

        if (currentTransaction != null)
        {
            command.Transaction = currentTransaction.GetDbTransaction();
        }

        command.CommandText = CheckTableExist(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);

        return command;
    }

    private static bool CheckTableExist(DbContext context, TableInfo tableInfo)
    {
        context.Database.OpenConnection();

        try
        {
            using DbCommand command = CreateCheckTableExistCommand(context, tableInfo);
            using DbDataReader reader = command.ExecuteReader();
            bool tableExist = false;

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    tableExist = (int)reader[0] == 1;
                }
            }

            return tableExist;
        }
        finally
        {
            context.Database.CloseConnection();
        }
    }

    private static async Task<bool> CheckTableExistAsync(DbContext context, TableInfo tableInfo, CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using DbCommand command = CreateCheckTableExistCommand(context, tableInfo);
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            bool tableExist = false;

            if (reader.HasRows)
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tableExist = (int)reader[0] == 1;
                }
            }

            return tableExist;
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static void HandleColumnMappingException(DbContext context, TableInfo tableInfo, InvalidOperationException ex)
    {
        if (!ex.Message.Contains(BulkExceptionMessage.ColumnMappingNotMatch))
        {
            return;
        }

        bool tableExist = CheckTableExist(context, tableInfo);

        if (tableExist)
        {
            return;
        }

        string sqlCreateTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo);
        string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);

        context.Database.ExecuteSqlRaw(sqlCreateTableCopy);
        context.Database.ExecuteSqlRaw(sqlDropTable);
    }

    private static async Task HandleColumnMappingExceptionAsync(DbContext context, TableInfo tableInfo, InvalidOperationException ex, CancellationToken cancellationToken)
    {
        if (ex.Message.Contains(BulkExceptionMessage.ColumnMappingNotMatch))
        {
            bool tableExist = await CheckTableExistAsync(context, tableInfo, cancellationToken).ConfigureAwait(false);

            if (!tableExist)
            {
                string sqlCreateTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo);
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);

                await context.Database.ExecuteSqlRawAsync(sqlCreateTableCopy, cancellationToken).ConfigureAwait(false);
                await context.Database.ExecuteSqlRawAsync(sqlDropTable, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public void Merge<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, OperationType operationType, Action<decimal>? progress) where T : class
    {
        IEnumerable<string>? entityPropertyWithDefaultValue = entities.GetPropertiesWithDefaultValue(type, tableInfo);
        PrepareMergeTables(context, tableInfo, operationType);
        bool keepIdentity = tableInfo.BulkConfig.BulkCopyOptions.HasFlag(BulkCopyOptions.KeepIdentity);

        try
        {
            if (tableInfo.BulkConfig.CustomSourceTableName == null)
            {
                Insert(context, type, entities, tableInfo, progress);
            }

            if (keepIdentity && tableInfo.HasIdentity)
            {
                string sqlSetIdentityInsertTrue = SetIdentityInsert(tableInfo.FullTableName, true);
                context.Database.OpenConnection();
                context.Database.ExecuteSqlRaw(sqlSetIdentityInsertTrue);
            }

            MergeTableResult mergeTableResult = SqlQueryBuilder.MergeTable<T>(context, tableInfo, operationType, entityPropertyWithDefaultValue);
            context.Database.ExecuteSqlRaw(mergeTableResult.Sql, mergeTableResult.Parameters);

            if (tableInfo.CreatedOutputTable)
            {
                tableInfo.LoadOutputData(context, type, entities, tableInfo);
            }
        }
        finally
        {
            CleanupMergeTables(context, tableInfo, keepIdentity);
        }
    }

    /// <inheritdoc/>
    public async Task MergeAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, OperationType operationType, Action<decimal>? progress, CancellationToken cancellationToken) where T : class
    {
        IEnumerable<string>? entityPropertyWithDefaultValue = entities.GetPropertiesWithDefaultValue(type, tableInfo);
        await PrepareMergeTablesAsync(context, tableInfo, operationType, cancellationToken).ConfigureAwait(false);
        bool keepIdentity = tableInfo.BulkConfig.BulkCopyOptions.HasFlag(BulkCopyOptions.KeepIdentity);

        try
        {
            if (tableInfo.BulkConfig.CustomSourceTableName == null)
            {
                await InsertAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);
            }

            if (keepIdentity && tableInfo.HasIdentity)
            {
                string sqlSetIdentityInsertTrue = SetIdentityInsert(tableInfo.FullTableName, true);
                await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                await context.Database.ExecuteSqlRawAsync(sqlSetIdentityInsertTrue, cancellationToken).ConfigureAwait(false);
            }

            MergeTableResult mergeTableResult = SqlQueryBuilder.MergeTable<T>(context, tableInfo, operationType, entityPropertyWithDefaultValue);
            await context.Database.ExecuteSqlRawAsync(mergeTableResult.Sql, mergeTableResult.Parameters, cancellationToken).ConfigureAwait(false);

            if (tableInfo.CreatedOutputTable)
            {
                await tableInfo.LoadOutputDataAsync(context, type, entities, tableInfo, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await CleanupMergeTablesAsync(context, tableInfo, keepIdentity, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string AlterTableColumnsToNullable(string tableName, TableInfo tableInfo)
    {
        string q = "";

        foreach (KeyValuePair<string, string> column in tableInfo.ColumnNamesTypesDict)
        {
            string columnName = column.Key;
            string columnType = column.Value;

            if (columnName == tableInfo.TimeStampColumnName)
            {
                columnType = TableInfo.TimeStampOutColumnType;
            }

            q += $"ALTER TABLE {tableName} ALTER COLUMN [{columnName}] {columnType}; ";
        }

        return q;
    }

    private static void PrepareMergeTables(DbContext context, TableInfo tableInfo, OperationType operationType)
    {
        if (tableInfo.BulkConfig.CustomSourceTableName == null)
        {
            tableInfo.InsertToTempTable = true;
            bool dropTempTableIfExists = tableInfo.BulkConfig.UseTempDb;

            if (dropTempTableIfExists)
            {
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
                context.Database.ExecuteSqlRaw(sqlDropTable);
            }

            string sqlCreateTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo);
            context.Database.ExecuteSqlRaw(sqlCreateTableCopy);

            if (tableInfo.TimeStampColumnName != null)
            {
                string sqlAddColumn = AddColumn(tableInfo.FullTempTableName, tableInfo.TimeStampColumnName, TableInfo.TimeStampOutColumnType);
                context.Database.ExecuteSqlRaw(sqlAddColumn);
            }
        }

        if (!tableInfo.CreatedOutputTable)
        {
            return;
        }

        string sqlCreateOutputTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempOutputTableName, tableInfo, true);
        context.Database.ExecuteSqlRaw(sqlCreateOutputTableCopy);

        if (tableInfo.TimeStampColumnName != null)
        {
            string sqlAddColumn = AddColumn(tableInfo.FullTempOutputTableName, tableInfo.TimeStampColumnName, TableInfo.TimeStampOutColumnType);
            context.Database.ExecuteSqlRaw(sqlAddColumn);
        }

        if (operationType == OperationType.InsertOrUpdateOrDelete)
        {
            string sqlAlterTableColumnsToNullable = AlterTableColumnsToNullable(tableInfo.FullTempOutputTableName, tableInfo);
            context.Database.ExecuteSqlRaw(sqlAlterTableColumnsToNullable);
        }
    }

    private static async Task PrepareMergeTablesAsync(DbContext context, TableInfo tableInfo, OperationType operationType, CancellationToken cancellationToken)
    {
        if (tableInfo.BulkConfig.CustomSourceTableName == null)
        {
            tableInfo.InsertToTempTable = true;
            bool dropTempTableIfExists = tableInfo.BulkConfig.UseTempDb;

            if (dropTempTableIfExists)
            {
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
                await context.Database.ExecuteSqlRawAsync(sqlDropTable, cancellationToken).ConfigureAwait(false);
            }

            string sqlCreateTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo);
            await context.Database.ExecuteSqlRawAsync(sqlCreateTableCopy, cancellationToken).ConfigureAwait(false);

            if (tableInfo.TimeStampColumnName != null)
            {
                string sqlAddColumn = AddColumn(tableInfo.FullTempTableName, tableInfo.TimeStampColumnName, TableInfo.TimeStampOutColumnType);
                await context.Database.ExecuteSqlRawAsync(sqlAddColumn, cancellationToken).ConfigureAwait(false);
            }
        }

        if (tableInfo.CreatedOutputTable)
        {
            string sqlCreateOutputTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempOutputTableName, tableInfo, true);
            await context.Database.ExecuteSqlRawAsync(sqlCreateOutputTableCopy, cancellationToken).ConfigureAwait(false);

            if (tableInfo.TimeStampColumnName != null)
            {
                string sqlAddColumn = AddColumn(tableInfo.FullTempOutputTableName, tableInfo.TimeStampColumnName, TableInfo.TimeStampOutColumnType);
                await context.Database.ExecuteSqlRawAsync(sqlAddColumn, cancellationToken).ConfigureAwait(false);
            }

            if (operationType == OperationType.InsertOrUpdateOrDelete)
            {
                string sqlAlterTableColumnsToNullable = AlterTableColumnsToNullable(tableInfo.FullTempOutputTableName, tableInfo);
                await context.Database.ExecuteSqlRawAsync(sqlAlterTableColumnsToNullable, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void CleanupMergeTables(DbContext context, TableInfo tableInfo, bool keepIdentity)
    {
        if (!tableInfo.BulkConfig.UseTempDb)
        {
            if (tableInfo.CreatedOutputTable)
            {
                string sqlDropOutputTable = SqlQueryBuilder.DropTable(tableInfo.FullTempOutputTableName, tableInfo.BulkConfig.UseTempDb);
                context.Database.ExecuteSqlRaw(sqlDropOutputTable);
            }

            if (tableInfo.BulkConfig.CustomSourceTableName == null)
            {
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
                context.Database.ExecuteSqlRaw(sqlDropTable);
            }
        }

        if (keepIdentity && tableInfo.HasIdentity)
        {
            string sqlSetIdentityInsertFalse = SetIdentityInsert(tableInfo.FullTableName, false);
            context.Database.ExecuteSqlRaw(sqlSetIdentityInsertFalse);
            context.Database.CloseConnection();
        }
    }

    private static async Task CleanupMergeTablesAsync(DbContext context, TableInfo tableInfo, bool keepIdentity, CancellationToken cancellationToken)
    {
        if (!tableInfo.BulkConfig.UseTempDb)
        {
            if (tableInfo.CreatedOutputTable)
            {
                string sqlDropOutputTable = SqlQueryBuilder.DropTable(tableInfo.FullTempOutputTableName, tableInfo.BulkConfig.UseTempDb);
                await context.Database.ExecuteSqlRawAsync(sqlDropOutputTable, cancellationToken).ConfigureAwait(false);
            }

            if (tableInfo.BulkConfig.CustomSourceTableName == null)
            {
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
                await context.Database.ExecuteSqlRawAsync(sqlDropTable, cancellationToken).ConfigureAwait(false);
            }
        }

        if (keepIdentity && tableInfo.HasIdentity)
        {
            string sqlSetIdentityInsertFalse = SetIdentityInsert(tableInfo.FullTableName, false);
            await context.Database.ExecuteSqlRawAsync(sqlSetIdentityInsertFalse, cancellationToken).ConfigureAwait(false);
            context.Database.CloseConnection();
        }
    }

    /// <inheritdoc/>
    public void Read<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress) where T : class
    {
        Dictionary<string, string> previousPropertyColumnNamesDict = tableInfo.ConfigureBulkReadTableInfo();
        string sqlCreateTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo);
        context.Database.ExecuteSqlRaw(sqlCreateTableCopy);

        try
        {
            Insert(context, type, entities, tableInfo, progress);
            ProcessReadEntities(context, type, entities, tableInfo, previousPropertyColumnNamesDict);
        }
        finally
        {
            if (!tableInfo.BulkConfig.UseTempDb)
            {
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);
                context.Database.ExecuteSqlRaw(sqlDropTable);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReadAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Action<decimal>? progress, CancellationToken cancellationToken) where T : class
    {
        Dictionary<string, string> previousPropertyColumnNamesDict = tableInfo.ConfigureBulkReadTableInfo();
        string sqlCreateTableCopy = CreateTableCopy(tableInfo.FullTableName, tableInfo.FullTempTableName, tableInfo);

        await context.Database.ExecuteSqlRawAsync(sqlCreateTableCopy, cancellationToken).ConfigureAwait(false);

        try
        {
            await InsertAsync(context, type, entities, tableInfo, progress, cancellationToken).ConfigureAwait(false);

            ProcessReadEntities(context, type, entities, tableInfo, previousPropertyColumnNamesDict);
        }
        finally
        {
            if (!tableInfo.BulkConfig.UseTempDb)
            {
                string sqlDropTable = SqlQueryBuilder.DropTable(tableInfo.FullTempTableName, tableInfo.BulkConfig.UseTempDb);

                await context.Database.ExecuteSqlRawAsync(sqlDropTable, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void ProcessReadEntities<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, Dictionary<string, string> previousPropertyColumnNamesDict) where T : class
    {
        tableInfo.PropertyColumnNamesDict = tableInfo.OutputPropertyColumnNamesDict;

        string sqlSelectJoinTable = SqlQueryBuilder.SelectJoinTable(tableInfo);

        tableInfo.PropertyColumnNamesDict = previousPropertyColumnNamesDict;

        if (tableInfo.TimeStampPropertyName != null && !tableInfo.PropertyColumnNamesDict.ContainsKey(tableInfo.TimeStampPropertyName) && tableInfo.TimeStampColumnName is not null)
        {
            tableInfo.PropertyColumnNamesDict.Add(tableInfo.TimeStampPropertyName, tableInfo.TimeStampColumnName);
        }

        List<T> existingEntities = tableInfo.LoadOutputEntities<T>(context, type, sqlSelectJoinTable);

        if (tableInfo.BulkConfig.ReplaceReadEntities)
        {
            tableInfo.ReplaceReadEntities(entities, existingEntities);
        }
        else
        {
            tableInfo.UpdateReadEntities(entities, existingEntities, context);
        }

        if (tableInfo.TimeStampPropertyName != null && !tableInfo.PropertyColumnNamesDict.ContainsKey(tableInfo.TimeStampPropertyName))
        {
            tableInfo.PropertyColumnNamesDict.Remove(tableInfo.TimeStampPropertyName);
        }
    }

    private static string SetIdentityInsert(string tableName, bool identityInsert)
    {
        string onOff = identityInsert ? "ON" : "OFF";
        string q = $"SET IDENTITY_INSERT {tableName} {onOff};";

        return q;
    }

    private static string AddColumn(string fullTableName, string columnName, string columnType) => $"ALTER TABLE {fullTableName} ADD [{columnName}] {columnType};";

    /// <inheritdoc/>
    public void Truncate(DbContext context, TableInfo tableInfo)
    {
        string sql = TruncateTable(tableInfo.FullTableName);
        context.Database.ExecuteSqlRaw(sql);
    }

    /// <inheritdoc/>
    public async Task TruncateAsync(DbContext context, TableInfo tableInfo, CancellationToken cancellationToken)
    {
        string sql = TruncateTable(tableInfo.FullTableName);
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Connection

    private static SqlBulkCopy GetSqlBulkCopy(SqlConnection sqlConnection, IDbContextTransaction? transaction, BulkConfig config)
    {
        SqlTransaction? sqlTransaction = transaction == null ? null : (SqlTransaction)transaction.GetUnderlyingTransaction(config);
        SqlBulkCopyOptions sqlBulkCopyOptions = (SqlBulkCopyOptions)config.BulkCopyOptions;
        var sqlBulkCopy = new SqlBulkCopy(sqlConnection, sqlBulkCopyOptions, sqlTransaction);

        if (config.BulkCopyColumnOrderHints != null)
        {
            foreach (BulkCopyColumnOrderHint hint in config.BulkCopyColumnOrderHints)
            {
                var sqlHint = new SqlBulkCopyColumnOrderHint(hint.ColumnName, (SortOrder)(int)hint.SortOrder);
                sqlBulkCopy.ColumnOrderHints.Add(sqlHint);
            }
        }

        return sqlBulkCopy;
    }

    private static void SetSqlBulkCopyConfig<T>(SqlBulkCopy sqlBulkCopy, TableInfo tableInfo, IList<T> entities, bool setColumnMapping, Action<decimal>? progress)
    {
        sqlBulkCopy.DestinationTableName = tableInfo.InsertToTempTable ? tableInfo.FullTempTableName : tableInfo.FullTableName;
        sqlBulkCopy.BatchSize = tableInfo.BulkConfig.BatchSize;
        sqlBulkCopy.NotifyAfter = tableInfo.BulkConfig.NotifyAfter ?? tableInfo.BulkConfig.BatchSize;
        sqlBulkCopy.SqlRowsCopied += (_, e) =>
        {
            progress?.Invoke(ProgressHelper.GetProgress(entities.Count, e.RowsCopied)); // round to 4 decimal places
        };
        sqlBulkCopy.BulkCopyTimeout = tableInfo.BulkConfig.BulkCopyTimeout ?? sqlBulkCopy.BulkCopyTimeout;
        sqlBulkCopy.EnableStreaming = tableInfo.BulkConfig.EnableStreaming;

        if (setColumnMapping)
        {
            foreach (KeyValuePair<string, string> element in tableInfo.PropertyColumnNamesDict)
            {
                sqlBulkCopy.ColumnMappings.Add(element.Key, element.Value);
            }
        }
    }

    #endregion

    #region DataTable

    internal static DataTable GetDataTable<T>(DbContext context, Type type, IList<T> entities, SqlBulkCopy sqlBulkCopy, TableInfo tableInfo)
    {
        DataTable dataTable = InnerGetDataTable(context, ref type, entities, tableInfo);

        foreach (DataColumn item in dataTable.Columns)  // Add mapping
        {
            sqlBulkCopy.ColumnMappings.Add(item.ColumnName, item.ColumnName);
        }

        return dataTable;
    }

    private static DataTable InnerGetDataTable<T>(DbContext context, ref Type type, IList<T> entities, TableInfo tableInfo)
    {
        var dataTable = new DataTable();
        var columnsDict = new Dictionary<string, object?>();
        var ownedEntitiesMappedProperties = new HashSet<string>();

        var sqlServerBytesWriter = new SqlServerBytesWriter();

        StoreObjectIdentifier objectIdentifier = tableInfo.ObjectIdentifier;
        type = tableInfo.HasAbstractList ? entities[0]!.GetType() : type;
        IEntityType entityType = context.Model.FindEntityType(type) ?? throw new ArgumentException($"Unable to determine entity type from given type - {type.Name}");
        List<IProperty> entityTypeProperties = entityType.GetProperties().ToList();
        Dictionary<string, IProperty> entityPropertiesDict = entityTypeProperties.Where(a => tableInfo.PropertyColumnNamesDict.ContainsKey(a.Name) ||
                                                                   (tableInfo.BulkConfig.OperationType != OperationType.Read && a.Name == tableInfo.TimeStampPropertyName))
                                                       .ToDictionary(a => a.Name, a => a);
        Dictionary<string, INavigation> entityNavigationOwnedDict = entityType.GetNavigations().Where(a => a.TargetEntityType.IsOwned()).ToDictionary(a => a.Name, a => a);
        Dictionary<string, IProperty> entityShadowFkPropertiesDict = entityTypeProperties.Where(a => a.IsShadowProperty() &&
                                                                           a.IsForeignKey() &&
                                                                           a.GetContainingForeignKeys().FirstOrDefault()?.DependentToPrincipal?.Name != null)
                                                                     .ToDictionary(x => x.GetContainingForeignKeys()?.First()?.DependentToPrincipal?.Name ?? string.Empty, a => a);

        Dictionary<string, string?> entityShadowFkPropertyColumnNamesDict = entityShadowFkPropertiesDict
            .ToDictionary(a => a.Key, a => a.Value.GetColumnName(objectIdentifier));
        Dictionary<string, string?> shadowPropertyColumnNamesDict = entityPropertiesDict
            .Where(a => a.Value.IsShadowProperty()).ToDictionary(a => a.Key, a => a.Value.GetColumnName(objectIdentifier));

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        string? discriminatorColumn = GetDiscriminatorColumn(tableInfo);

        foreach (PropertyInfo property in properties)
        {
            bool hasDefaultVauleOnInsert = tableInfo.BulkConfig.OperationType == OperationType.Insert
                && !tableInfo.BulkConfig.SetOutputIdentity
                && tableInfo.DefaultValueProperties.Contains(property.Name);

            if (entityPropertiesDict.TryGetValue(property.Name, out IProperty? propertyEntityType))
            {
                string columnName = propertyEntityType.GetColumnName(objectIdentifier) ?? string.Empty;

                bool isConvertible = tableInfo.ConvertibleColumnConverterDict.ContainsKey(columnName);
                Type propertyType = isConvertible ? tableInfo.ConvertibleColumnConverterDict[columnName].ProviderClrType : property.PropertyType;

                Type? underlyingType = Nullable.GetUnderlyingType(propertyType);

                if (underlyingType != null)
                {
                    propertyType = underlyingType;
                }

                if (propertyType == typeof(Geometry) || propertyType.IsSubclassOf(typeof(Geometry)))
                {
                    propertyType = typeof(byte[]);
                    tableInfo.HasSpatialType = true;

                    if (tableInfo.BulkConfig.PropertiesToIncludeOnCompare != null || tableInfo.BulkConfig.PropertiesToIncludeOnCompare != null)
                    {
                        throw new InvalidOperationException("OnCompare properties Config can not be set for Entity with Spatial types like 'Geometry'");
                    }
                }

                if (propertyType == typeof(HierarchyId) || propertyType.IsSubclassOf(typeof(HierarchyId)))
                {
                    propertyType = typeof(byte[]);
                }

                if (!columnsDict.ContainsKey(property.Name) && !hasDefaultVauleOnInsert)
                {
                    dataTable.Columns.Add(columnName, propertyType);
                    columnsDict.Add(property.Name, null);
                }
            }
            else
            {
                if (entityShadowFkPropertiesDict.TryGetValue(property.Name, out IProperty? fk))
                {
                    entityPropertiesDict.TryGetValue(fk.GetColumnName(objectIdentifier) ?? string.Empty, out IProperty? entityProperty);

                    if (entityProperty == null) // BulkRead
                    {
                        continue;
                    }

                    string? columnName = entityProperty.GetColumnName(objectIdentifier);

                    bool isConvertible = tableInfo.ConvertibleColumnConverterDict.ContainsKey(columnName ?? string.Empty);
                    Type propertyType = isConvertible ? tableInfo.ConvertibleColumnConverterDict[columnName ?? string.Empty].ProviderClrType : entityProperty.ClrType;

                    Type? underlyingType = Nullable.GetUnderlyingType(propertyType);

                    if (underlyingType != null)
                    {
                        propertyType = underlyingType;
                    }

                    if (propertyType == typeof(Geometry))
                    {
                        propertyType = typeof(byte[]);
                    }

                    if (propertyType == typeof(HierarchyId))
                    {
                        propertyType = typeof(byte[]);
                    }

                    if (columnName is not null && !(columnsDict.ContainsKey(columnName)) && !hasDefaultVauleOnInsert)
                    {
                        dataTable.Columns.Add(columnName, propertyType);
                        columnsDict.Add(columnName, null);
                    }
                }
                else
                {
                    if (entityNavigationOwnedDict.ContainsKey(property.Name)) // isOWned
                    {
                        //Type? navOwnedType = type.Assembly.GetType(property.PropertyType.FullName!); // was not used

                        IEntityType? ownedEntityType = context.Model.FindEntityType(property.PropertyType);

                        if (ownedEntityType == null)
                        {
                            ownedEntityType = context.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType == property.PropertyType && x.Name.StartsWith(entityType.Name + "." + property.Name + "#"));
                        }

                        List<IProperty> ownedEntityProperties = ownedEntityType?.GetProperties().ToList() ?? new();
                        Dictionary<string, string> ownedEntityPropertyNameColumnNameDict = new Dictionary<string, string>();

                        foreach (IProperty ownedEntityProperty in ownedEntityProperties)
                        {
                            if (!ownedEntityProperty.IsPrimaryKey())
                            {
                                string? columnName = ownedEntityProperty.GetColumnName(objectIdentifier);

                                if (columnName is not null && tableInfo.PropertyColumnNamesDict.ContainsValue(columnName))
                                {
                                    ownedEntityPropertyNameColumnNameDict.Add(ownedEntityProperty.Name, columnName);
                                    ownedEntitiesMappedProperties.Add(property.Name + "_" + ownedEntityProperty.Name);
                                }
                            }
                        }

                        PropertyInfo[] innerProperties = property.PropertyType.GetProperties();

                        if (!tableInfo.LoadOnlyPkColumn)
                        {
                            foreach (PropertyInfo innerProperty in innerProperties)
                            {
                                if (ownedEntityPropertyNameColumnNameDict.TryGetValue(innerProperty.Name, out string? columnName))
                                {
                                    string propertyName = $"{property.Name}_{innerProperty.Name}";

                                    if (tableInfo.ConvertibleColumnConverterDict.TryGetValue(propertyName, out ValueConverter? convertor))
                                    {
                                        Type underlyingType = Nullable.GetUnderlyingType(convertor.ProviderClrType) ?? convertor.ProviderClrType;

                                        dataTable.Columns.Add(columnName, underlyingType);
                                    }
                                    else
                                    {
                                        Type ownedPropertyType = Nullable.GetUnderlyingType(innerProperty.PropertyType) ?? innerProperty.PropertyType;

                                        if (ownedPropertyType == typeof(Geometry) || ownedPropertyType.IsSubclassOf(typeof(Geometry)))
                                        {
                                            ownedPropertyType = typeof(byte[]);
                                            tableInfo.HasSpatialType = true;

                                            if (tableInfo.BulkConfig.PropertiesToIncludeOnCompare != null || tableInfo.BulkConfig.PropertiesToIncludeOnCompare != null)
                                            {
                                                throw new InvalidOperationException("OnCompare properties Config can not be set for Entity with Spatial types like 'Geometry'");
                                            }
                                        }

                                        if (ownedPropertyType == typeof(HierarchyId) || ownedPropertyType.IsSubclassOf(typeof(HierarchyId)))
                                        {
                                            ownedPropertyType = typeof(byte[]);
                                        }

                                        dataTable.Columns.Add(columnName, ownedPropertyType);
                                    }

                                    columnsDict.Add(property.Name + "_" + innerProperty.Name, null);
                                }
                            }
                        }
                    }
                }
            }
        }

        if (tableInfo.BulkConfig.EnableShadowProperties)
        {
            foreach (IProperty shadowProperty in entityPropertiesDict.Values.Where(a => a.IsShadowProperty()))
            {
                string? columnName = shadowProperty.GetColumnName(objectIdentifier);

                // If a model has an entity which has a relationship without an explicity defined FK, the data table will already contain the foreign key shadow property
                if (columnName is not null && dataTable.Columns.Contains(columnName))
                {
                    continue;
                }

                bool isConvertible = columnName is not null && tableInfo.ConvertibleColumnConverterDict.ContainsKey(columnName);

                Type propertyType = isConvertible
                    ? tableInfo.ConvertibleColumnConverterDict[columnName!].ProviderClrType
                    : shadowProperty.ClrType;

                Type? underlyingType = Nullable.GetUnderlyingType(propertyType);

                if (underlyingType != null)
                {
                    propertyType = underlyingType;
                }

                if (propertyType == typeof(Geometry) || propertyType.IsSubclassOf(typeof(Geometry)))
                {
                    propertyType = typeof(byte[]);
                }

                if (propertyType == typeof(HierarchyId) || propertyType.IsSubclassOf(typeof(HierarchyId)))
                {
                    propertyType = typeof(byte[]);
                }

                dataTable.Columns.Add(columnName, propertyType);
                columnsDict.Add(shadowProperty.Name, null);
            }
        }

        if (discriminatorColumn != null)
        {
            IProperty discriminatorProperty = entityPropertiesDict[discriminatorColumn];

            dataTable.Columns.Add(discriminatorColumn, discriminatorProperty.ClrType);
            columnsDict.Add(discriminatorColumn, entityType.GetDiscriminatorValue());
        }

        bool hasConverterProperties = tableInfo.ConvertiblePropertyColumnDict.Count > 0;

        if (tableInfo.BulkConfig.UseOriginalIndexToIdentityMappingColumn)
        {
            dataTable.Columns.Add(tableInfo.OriginalIndexColumnName, typeof(int));
            columnsDict.Add(tableInfo.OriginalIndexColumnName, -1);
        }

        int index = 0;

        foreach (T entity in entities)
        {
            IEnumerable<PropertyInfo> propertiesToLoad = properties
                .Where(a => !tableInfo.AllNavigationsDictionary.ContainsKey(a.Name)
                            || entityShadowFkPropertiesDict.ContainsKey(a.Name)
                            || tableInfo.OwnedTypesDict.ContainsKey(a.Name)); // omit virtual Navigation (except Owned and ShadowNavig.) since it's Getter can cause unwanted Select-s from Db

            foreach (PropertyInfo property in propertiesToLoad)
            {
                object? propertyValue = tableInfo.FastPropertyDict.ContainsKey(property.Name)
                    ? tableInfo.FastPropertyDict[property.Name].Get(entity!)
                    : null;

                bool hasDefaultVauleOnInsert = tableInfo.BulkConfig.OperationType == OperationType.Insert
                    && !tableInfo.BulkConfig.SetOutputIdentity
                    && tableInfo.DefaultValueProperties.Contains(property.Name);

                if (tableInfo.BulkConfig.DateTime2PrecisionForceRound
                    && tableInfo.DateTime2PropertiesPrecisionLessThen7Dict.TryGetValue(property.Name, out int precision))
                {
                    DateTime? dateTimePropertyValue = (DateTime?)propertyValue;

                    if (dateTimePropertyValue is not null)
                    {
                        int digitsToRemove = 7 - precision;
                        int powerOf10 = (int)Math.Pow(10, digitsToRemove);

                        long subsecondTicks = dateTimePropertyValue.Value.Ticks % 10000000;
                        long ticksToRound = subsecondTicks + (subsecondTicks % 10 == 0 ? 1 : 0); // if ends with 0 add 1 tick to make sure rounding of value .5_zeros is rounded to Upper like SqlServer is doing, not to Even as Math.Round works
                        int roundedTicks = Convert.ToInt32(Math.Round((decimal)ticksToRound / powerOf10, 0)) * powerOf10;
                        dateTimePropertyValue = dateTimePropertyValue.Value.AddTicks(-subsecondTicks).AddTicks(roundedTicks);

                        propertyValue = dateTimePropertyValue;
                    }
                }

                if (hasConverterProperties && tableInfo.ConvertiblePropertyColumnDict.TryGetValue(property.Name, out string? convertibleColumnName))
                {
                    propertyValue = tableInfo.ConvertibleColumnConverterDict[convertibleColumnName].ConvertToProvider.Invoke(propertyValue);
                }

                if (tableInfo.HasSpatialType && propertyValue is Geometry geometryValue)
                {
                    geometryValue.SRID = tableInfo.BulkConfig.Srid;

                    if (tableInfo.PropertyColumnNamesDict.TryGetValue(property.Name, out string? spatialColumnName))
                    {
                        sqlServerBytesWriter.IsGeography = tableInfo.ColumnNamesTypesDict[spatialColumnName] == "geography"; // "geography" type is default, otherwise it's "geometry" type
                    }

                    propertyValue = sqlServerBytesWriter.Write(geometryValue);
                }

                if (propertyValue is HierarchyId hierarchyValue)
                {
                    using MemoryStream memStream = new();
                    using BinaryWriter binWriter = new(memStream);
#if !NET8_0 && !NET10_0
                    hierarchyValue.Write(binWriter);
#endif
                    propertyValue = memStream.ToArray();
                }

                if (entityPropertiesDict.ContainsKey(property.Name) && !hasDefaultVauleOnInsert)
                {
                    columnsDict[property.Name] = propertyValue;
                }
                else
                {
                    if (entityShadowFkPropertiesDict.TryGetValue(property.Name, out IProperty? foreignKeyShadowProperty))
                    {
                        string columnName = entityShadowFkPropertyColumnNamesDict[property.Name] ?? string.Empty;

                        if (!entityPropertiesDict.TryGetValue(columnName, out IProperty? entityProperty) || entityProperty is null)
                        {
                            continue; // BulkRead
                        }

                        columnsDict[columnName] = propertyValue == null
                            ? null
                            : foreignKeyShadowProperty.FindFirstPrincipal()?.PropertyInfo?.GetValue(propertyValue); // TODO Check if can be optimized
                    }
                    else
                    {
                        if (entityNavigationOwnedDict.ContainsKey(property.Name) && !tableInfo.LoadOnlyPkColumn)
                        {
                            IEnumerable<PropertyInfo> ownedProperties = property.PropertyType.GetProperties()
                                .Where(a => ownedEntitiesMappedProperties.Contains(property.Name + "_" + a.Name));

                            foreach (PropertyInfo ownedProperty in ownedProperties)
                            {
                                string columnName = $"{property.Name}_{ownedProperty.Name}";
                                object? ownedPropertyValue = propertyValue == null ? null : tableInfo.FastPropertyDict[columnName].Get(propertyValue);

                                if (tableInfo.ConvertibleColumnConverterDict.TryGetValue(columnName, out ValueConverter? converter))
                                {
                                    columnsDict[columnName] = ownedPropertyValue == null ? null : converter.ConvertToProvider.Invoke(ownedPropertyValue);
                                }
                                else
                                {
                                    if (tableInfo.HasSpatialType && ownedPropertyValue is Geometry ownedGeometryValue)
                                    {
                                        ownedGeometryValue.SRID = tableInfo.BulkConfig.Srid;

                                        if (tableInfo.PropertyColumnNamesDict.TryGetValue(property.Name, out string? ownedSpatialColumnName))
                                        {
                                            sqlServerBytesWriter.IsGeography = tableInfo.ColumnNamesTypesDict[ownedSpatialColumnName] == "geography"; // "geography" type is default, otherwise it's "geometry" type
                                        }

                                        columnsDict[columnName] = sqlServerBytesWriter.Write(ownedGeometryValue);
                                    }
                                    else
                                    {
                                        columnsDict[columnName] = ownedPropertyValue;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (tableInfo.BulkConfig.EnableShadowProperties)
            {
                foreach (string shadowPropertyName in shadowPropertyColumnNamesDict.Keys)
                {
                    string columnName = shadowPropertyColumnNamesDict[shadowPropertyName] ?? string.Empty;

                    object? propertyValue = default(object);

                    if (tableInfo.BulkConfig.ShadowPropertyValue == null)
                    {
                        propertyValue = context.Entry(entity!).Property(shadowPropertyName).CurrentValue;
                    }
                    else
                    {
                        propertyValue = tableInfo.BulkConfig.ShadowPropertyValue(entity!, shadowPropertyName);
                    }

                    if (tableInfo.ConvertibleColumnConverterDict.TryGetValue(columnName, out ValueConverter? converter))
                    {
                        propertyValue = converter.ConvertToProvider.Invoke(propertyValue);
                    }

                    columnsDict[shadowPropertyName] = propertyValue;
                }
            }

            if (tableInfo.BulkConfig.UseOriginalIndexToIdentityMappingColumn)
            {
                columnsDict[tableInfo.OriginalIndexColumnName] = index;
            }

            object?[] record = columnsDict.Values.ToArray();

            dataTable.Rows.Add(record);
            index++;
        }

        return dataTable;
    }

    private static string? GetDiscriminatorColumn(TableInfo tableInfo)
    {
        string? discriminatorColumn = null;

        if (!tableInfo.BulkConfig.EnableShadowProperties && tableInfo.ShadowProperties.Count > 0)
        {
            string[] stringColumns = tableInfo.ColumnNamesTypesDict
                .Where(a => a.Value.Contains("char"))
                .Select(a => a.Key)
                .ToArray();
            discriminatorColumn = tableInfo.ShadowProperties.Where(a => stringColumns.Contains(a)).ElementAt(0);
        }

        return discriminatorColumn;
    }
    #endregion
}