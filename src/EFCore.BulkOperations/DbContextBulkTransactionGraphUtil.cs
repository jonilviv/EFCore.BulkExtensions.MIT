using EFCore.BulkOperations.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace EFCore.BulkOperations;

internal static class DbContextBulkTransactionGraphUtil
{
    public static void ExecuteWithGraph(DbContext context, IEnumerable<object> entities, OperationType operationType, BulkConfig bulkConfig, Action<decimal>? progress)
    {
        ConfigureAndValidateGraph(context, operationType, bulkConfig);
        IEnumerable<GraphNode>? graphNodes = GraphUtil.GetTopologicallySortedGraph(context, entities);

        if (graphNodes == null)
        {
            return;
        }

        bool hasExistingTransaction = context.Database.CurrentTransaction != null || Transaction.Current != null;
        IDbContextTransaction? transaction = hasExistingTransaction ? null : context.Database.CurrentTransaction ?? context.Database.BeginTransaction();

        try
        {
            IEnumerable<IGrouping<Type, GraphNode>> graphNodesGroupedByType = graphNodes.GroupBy(y => y.Entity.GetType());

            foreach (IGrouping<Type, GraphNode> graphNodeGroup in graphNodesGroupedByType)
            {
                Type entityClrType = graphNodeGroup.Key;
                IEntityType entityType = context.Model.FindEntityType(entityClrType) ?? throw new ArgumentException($"Unable to determine EntityType from given type {entityClrType.Name}");

                if (OwnedTypeUtil.IsOwnedInSameTableAsOwner(entityType))
                {
                    continue;
                }

                IEnumerable<object> entitySelection = graphNodeGroup.Select(y => y.Entity);
                List<object> entitiesToAction = GetUniqueEntities(context, entitySelection).ToList();
                TableInfo tableInfo = TableInfo.CreateInstance(context, entityClrType, entitiesToAction, operationType, bulkConfig);

                SqlBulkOperation.Merge(context, entityClrType, entitiesToAction, tableInfo, operationType, progress);

                List<object> dependentsOfSameType = SetForeignKeysForDependentsAndYieldSameTypeDependents(context, entityClrType, graphNodeGroup).ToList();

                if (dependentsOfSameType.Any())
                {
                    TableInfo dependentTableInfo = TableInfo.CreateInstance(context, entityClrType, dependentsOfSameType, operationType, bulkConfig);

                    SqlBulkOperation.Merge(context, entityClrType, dependentsOfSameType, dependentTableInfo, operationType, progress);
                }
            }

            if (!hasExistingTransaction)
            {
                transaction!.Commit();
            }
        }
        finally
        {
            if (!hasExistingTransaction)
            {
                transaction?.Dispose();
            }
        }
    }

    public static async Task ExecuteWithGraphAsync(DbContext context, IEnumerable<object> entities, OperationType operationType, BulkConfig bulkConfig, Action<decimal>? progress, CancellationToken cancellationToken)
    {
        ConfigureAndValidateGraph(context, operationType, bulkConfig);
        IEnumerable<GraphNode>? graphNodes = GraphUtil.GetTopologicallySortedGraph(context, entities);

        if (graphNodes == null)
        {
            return;
        }

        bool hasExistingTransaction = context.Database.CurrentTransaction != null || Transaction.Current != null;
        IDbContextTransaction? transaction = hasExistingTransaction ? null : context.Database.CurrentTransaction ?? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            IEnumerable<IGrouping<Type, GraphNode>> graphNodesGroupedByType = graphNodes.GroupBy(y => y.Entity.GetType());

            foreach (IGrouping<Type, GraphNode> graphNodeGroup in graphNodesGroupedByType)
            {
                Type entityClrType = graphNodeGroup.Key;
                IEntityType entityType = context.Model.FindEntityType(entityClrType) ?? throw new ArgumentException($"Unable to determine EntityType from given type {entityClrType.Name}");

                if (OwnedTypeUtil.IsOwnedInSameTableAsOwner(entityType))
                {
                    continue;
                }

                IEnumerable<object> entitySelection = graphNodeGroup.Select(y => y.Entity);
                List<object> entitiesToAction = GetUniqueEntities(context, entitySelection).ToList();
                TableInfo tableInfo = TableInfo.CreateInstance(context, entityClrType, entitiesToAction, operationType, bulkConfig);

                await SqlBulkOperation.MergeAsync(context, entityClrType, entitiesToAction, tableInfo, operationType, progress, cancellationToken).ConfigureAwait(false);

                List<object> dependentsOfSameType = SetForeignKeysForDependentsAndYieldSameTypeDependents(context, entityClrType, graphNodeGroup).ToList();

                if (dependentsOfSameType.Any())
                {
                    TableInfo dependentTableInfo = TableInfo.CreateInstance(context, entityClrType, dependentsOfSameType, operationType, bulkConfig);

                    await SqlBulkOperation.MergeAsync(context, entityClrType, dependentsOfSameType, dependentTableInfo, operationType, progress, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!hasExistingTransaction)
            {
                await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!hasExistingTransaction)
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private static void ConfigureAndValidateGraph(DbContext context, OperationType operationType, BulkConfig bulkConfig)
    {
        if (operationType != OperationType.Insert
            && operationType != OperationType.InsertOrUpdate
            && operationType != OperationType.InsertOrUpdateOrDelete
            && operationType != OperationType.Update)
        {
            throw new InvalidBulkConfigException($"{nameof(BulkConfig)}.{nameof(BulkConfig.IncludeGraph)} only supports Insert or Update operations.");
        }

        if (!SqlAdaptersMapping.GetAdapterDialect(context).SupportsGraphOperations)
        {
            throw new NotSupportedException("Sqlite is not currently supported due to its BulkInsert implementation.");
        }

        bulkConfig.PreserveInsertOrder = true;
        bulkConfig.SetOutputIdentity = true;
        bulkConfig.EnableShadowProperties = true;
    }

    private static IEnumerable<object> SetForeignKeysForDependentsAndYieldSameTypeDependents(DbContext context, Type entityClrType, IEnumerable<GraphNode> graphNodeGroup)
    {
        // Loop through the dependants and update their foreign keys with the PK values of the just inserted / merged entities
        foreach (GraphNode graphNode in graphNodeGroup)
        {
            object entity = graphNode.Entity;

            foreach (EntityNavigationDependency dependent in graphNode.Dependencies.Dependents)
            {
                SetForeignKeyForRelationship(context, dependent.Navigation, dependent.Entity, entity);

                if (dependent.Entity.GetType() == entityClrType)
                {
                    yield return dependent.Entity;
                }
            }
        }
    }

    private static IEnumerable<object> GetUniqueEntities(DbContext context, IEnumerable<object> entities)
    {
        Type firstEntityType = entities.First().GetType();
        IEntityType entityType = context.Model.FindEntityType(firstEntityType) ?? throw new ArgumentException($"Unable to determine EntityType from given type {firstEntityType.Name}");
        IKey? pk = entityType.FindPrimaryKey();
        var processedPks = new HashSet<PrimaryKeyList>();

        foreach (object entity in entities)
        {
            EntityEntry entry = context.Entry(entity);

            // If the entry has its key set, make sure its unique. It is possible for an entity to exist more than once in a graph.
            if (entry.IsKeySet)
            {
                var primaryKeyComparer = new PrimaryKeyList();

                if (pk is not null)
                {
                    foreach (IProperty pkProp in pk.Properties)
                    {
                        object? currentValue = entry.Property(pkProp.Name).CurrentValue;
                        primaryKeyComparer.Add(currentValue);
                    }

                    // If the processed pk already exists in the HashSet, its not unique.
                    if (processedPks.Add(primaryKeyComparer))
                    {
                        yield return entity;
                    }
                }

            }
            else
            {
                yield return entity;
            }
        }
    }

    private static void SetForeignKeyForRelationship(DbContext context, INavigation navigation, object dependent, object principal)
    {
        IReadOnlyList<IProperty> principalKeyProperties = navigation.ForeignKey.PrincipalKey.Properties;
        var pkValues = new List<object?>();

        foreach (IProperty pk in principalKeyProperties)
        {
            object? value = context.Entry(principal).Property(pk.Name).CurrentValue;
            pkValues.Add(value);
        }

        IReadOnlyList<IProperty> dependantKeyProperties = navigation.ForeignKey.Properties;

        for (int i = 0; i < pkValues.Count; i++)
        {
            IProperty dk = dependantKeyProperties[i];
            object? pkVal = pkValues[i];

            context.Entry(dependent).Property(dk.Name).CurrentValue = pkVal;
        }
    }
}