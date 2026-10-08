using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations;

internal static class DbContextBulkTransactionSaveChanges
{
    #region SaveChanges

    public static void SaveChanges(DbContext context, BulkConfig? bulkConfig, Action<decimal>? progress)
    {
        bulkConfig ??= new BulkConfig();
        ConfigureSaveChanges(context, bulkConfig);

        List<BulkEntryGroup> entryGroups = GetChangedEntryGroups(context);

        if (entryGroups.Count == 0)
        {
            return;
        }

        context.Database.OpenConnection();
        DbConnection connection = context.GetUnderlyingConnection(bulkConfig);
        bool doExplicitCommit = context.Database.CurrentTransaction == null;

        try
        {
            IDbContextTransaction transaction = context.Database.CurrentTransaction ?? context.Database.BeginTransaction();
            var fastPropertyDicts = new Dictionary<string, Dictionary<string, FastProperty>>();

            foreach (BulkEntryGroup entryGroup in entryGroups)
            {
                Type entityType = PrepareGroupForSave(context, entryGroup.EntityType, entryGroup.Entities, bulkConfig, fastPropertyDicts);
                string methodName = EntityStateBulkMethodDict[entryGroup.EntityState].Key;
                InvokeBulkMethod(context, entryGroup.Entities, entityType, methodName, bulkConfig, progress);
            }

            if (doExplicitCommit)
            {
                transaction.Commit();
                context.ChangeTracker.AcceptAllChanges();
            }
        }
        finally
        {
            if (doExplicitCommit)
            {
                context.Database.CloseConnection();
            }
        }
    }

    public static async Task SaveChangesAsync(DbContext context, BulkConfig? bulkConfig, Action<decimal>? progress, CancellationToken cancellationToken)
    {
        bulkConfig ??= new BulkConfig();
        ConfigureSaveChanges(context, bulkConfig);

        List<BulkEntryGroup> entryGroups = GetChangedEntryGroups(context);

        if (entryGroups.Count == 0)
        {
            return;
        }

        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        DbConnection connection = context.GetUnderlyingConnection(bulkConfig);
        bool doExplicitCommit = context.Database.CurrentTransaction == null;

        try
        {
            IDbContextTransaction transaction = context.Database.CurrentTransaction ?? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var fastPropertyDicts = new Dictionary<string, Dictionary<string, FastProperty>>();

            foreach (BulkEntryGroup entryGroup in entryGroups)
            {
                Type entityType = PrepareGroupForSave(context, entryGroup.EntityType, entryGroup.Entities, bulkConfig, fastPropertyDicts);
                string methodName = EntityStateBulkMethodDict[entryGroup.EntityState].Key;
                await InvokeBulkMethodAsync(context, entryGroup.Entities, entityType, methodName, bulkConfig, progress, cancellationToken).ConfigureAwait(false);
            }

            if (doExplicitCommit)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                context.ChangeTracker.AcceptAllChanges();
            }
        }
        finally
        {
            if (doExplicitCommit)
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

    private static void ConfigureSaveChanges(DbContext context, BulkConfig bulkConfig)
    {
        DbContextBulkTransaction.CheckForMySqlUnsupportedFeatures(context, OperationType.SaveChanges, bulkConfig);

        if (bulkConfig.OnSaveChangesSetFk && !bulkConfig.SetOutputIdentity)
        {
            bulkConfig.SetOutputIdentity = true;
        }
    }

    private static List<BulkEntryGroup> GetChangedEntryGroups(DbContext context)
    {
        IEnumerable<EntityEntry> entries = context.ChangeTracker.Entries();
        IEnumerable<BulkEntryGroup> entriesGroupedByEntity = entries.GroupBy(a => new { EntityType = a.Entity.GetType(), a.State },
                                                                     (entry, group) => new BulkEntryGroup
                                                                     {
                                                                         EntityType = entry.EntityType,
                                                                         EntityState = entry.State,
                                                                         Entities = group.Select(a => a.Entity).ToList()
                                                                     });
        IEnumerable<BulkEntryGroup> entriesGroupedChanged = entriesGroupedByEntity.Where(a => EntityStateBulkMethodDict.ContainsKey(a.EntityState) & a.Entities.Count >= 0);
        List<BulkEntryGroup> result = entriesGroupedChanged.OrderBy(a => a.EntityState.ToString() != EntityState.Modified.ToString()).ToList();

        return result;
    }

    private static Type PrepareGroupForSave(DbContext context, Type rawEntityType, List<object> entities, BulkConfig bulkConfig, Dictionary<string, Dictionary<string, FastProperty>> fastPropertyDicts)
    {
        Type entityType = (rawEntityType.Namespace == "Castle.Proxies") ? rawEntityType.BaseType! : rawEntityType;
        IEntityType entityModelType = context.Model.FindEntityType(entityType) ?? throw new ArgumentNullException($"Unable to determine EntityType from given type with name {entityType.Name}");

        var entityPropertyDict = new Dictionary<string, FastProperty>();

        if (!fastPropertyDicts.ContainsKey(entityType.Name))
        {
            IEnumerable<IProperty> properties = entityModelType.GetProperties();
            IEnumerable<PropertyInfo?> navigationPropertiesInfo = entityModelType.GetNavigations().Select(x => x.PropertyInfo);

            foreach (IProperty property in properties)
            {
                if (property.PropertyInfo != null)
                {
                    entityPropertyDict.Add(property.Name, FastProperty.GetOrCreate(property.PropertyInfo));
                }
            }

            foreach (PropertyInfo? navigationPropertyInfo in navigationPropertiesInfo)
            {
                if (navigationPropertyInfo != null)
                {
                    entityPropertyDict.Add(navigationPropertyInfo.Name, FastProperty.GetOrCreate(navigationPropertyInfo));
                }
            }

            fastPropertyDicts.Add(entityType.Name, entityPropertyDict);
        }
        else
        {
            entityPropertyDict = fastPropertyDicts[entityType.Name];
        }

        if (bulkConfig.OnSaveChangesSetFk)
        {
            IEnumerable<INavigation> navigations = entityModelType.GetNavigations().Where(x => !x.IsCollection && !x.TargetEntityType.IsOwned());

            if (navigations.Any())
            {
                foreach (INavigation navigation in navigations)
                {
                    if (fastPropertyDicts.ContainsKey(navigation.ClrType.Name))
                    {
                        Dictionary<string, FastProperty> parentPropertyDict = fastPropertyDicts[navigation.ClrType.Name];

                        string? fkName = navigation.ForeignKey.Properties.Count > 0
                                         ? navigation.ForeignKey.Properties[0].Name
                                         : null;

                        string? pkName = navigation.ForeignKey.PrincipalKey.Properties.Count > 0
                                         ? navigation.ForeignKey.PrincipalKey.Properties[0].Name
                                         : null;

                        if (pkName is not null && fkName is not null)
                        {
                            foreach (object entity in entities)
                            {
                                object? parentEntity = entityPropertyDict[navigation.Name].Get(entity);

                                if (parentEntity is not null)
                                {
                                    object? pkValue = parentPropertyDict[pkName].Get(parentEntity);

                                    if (pkValue is not null)
                                    {
                                        entityPropertyDict[fkName].Set(entity, pkValue);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return entityType;
    }

    private static void InvokeBulkMethod(DbContext context, List<object> entities, Type entityType, string methodName, BulkConfig bulkConfig, Action<decimal>? progress)
    {
        MethodInfo? bulkMethod = typeof(DbContextBulkOperations)
                                 .GetMethods()
                                 .Where(a => a.Name == methodName)
                                 .FirstOrDefault();

        bulkMethod = bulkMethod?.MakeGenericMethod(typeof(object));
        var arguments = new List<object?> { context, entities, bulkConfig, progress, entityType };
        object?[] methodArguments = arguments.ToArray();
        bulkMethod?.Invoke(null, methodArguments);
    }

    private static async Task InvokeBulkMethodAsync(DbContext context, List<object> entities, Type entityType, string methodName, BulkConfig bulkConfig, Action<decimal>? progress, CancellationToken cancellationToken)
    {
        string asyncMethodName = methodName + "Async";
        MethodInfo? bulkMethod = typeof(DbContextBulkOperations)
                                 .GetMethods()
                                 .Where(a => a.Name == asyncMethodName)
                                 .FirstOrDefault();

        bulkMethod = bulkMethod?.MakeGenericMethod(typeof(object));
        var arguments = new List<object?> { context, entities, bulkConfig, progress, entityType, cancellationToken };
        object?[] methodArguments = arguments.ToArray();

        if (bulkMethod is not null)
        {
            Task? task = (Task?)bulkMethod.Invoke(null, methodArguments);

            if (task != null)
            {
                await task.ConfigureAwait(false);
            }
        }
    }

    internal static Dictionary<EntityState, KeyValuePair<string, int>> EntityStateBulkMethodDict => new()
    {
        { EntityState.Deleted, new KeyValuePair<string, int>(nameof(DbContextBulkOperations.BulkDelete), 1) },
        { EntityState.Modified, new KeyValuePair<string, int>(nameof(DbContextBulkOperations.BulkUpdate), 2) },
        { EntityState.Added, new KeyValuePair<string, int>(nameof(DbContextBulkOperations.BulkInsert), 3) },
    };

    #endregion

    private static List<BulkMethodEntries> GetBulkMethodEntries(IEnumerable<EntityEntry> entries)
    {
        EntityEntry[] entryList = entries.ToArray();
        Dictionary<Type, DbNode> tree = new Dictionary<Type, DbNode>();

        for (int i = 0; i < entryList.Length; i++)
        {
            EntityEntry entry = entryList[i];

            Type type = GetNonProxyType(entry.Entity.GetType());

            if (!tree.TryGetValue(type, out DbNode? node))
            {
                node = new DbNode() { Type = type };
                tree.TryAdd(type, node);
            }

            node.AddEntry(entry);

            IEnumerable<NavigationEntry> navigations = entry.Navigations.Where(a => a.IsLoaded);

            foreach (NavigationEntry n in navigations.Where(a => a.Metadata.IsCollection))
            {
                Type navType = GetNonProxyType(n.Metadata.ClrType.GenericTypeArguments.Single());

                if (!tree.TryGetValue(navType, out DbNode? childNode))
                {
                    childNode = new DbNode() { Type = navType };

                    tree.TryAdd(navType, childNode);
                }

                ;

                if (!childNode.Parents.Any(a => a.Type == node.Type))
                {
                    childNode.Parents.Add(node);
                }

                if (!node.Children.Any(a => a.Type == navType))
                {
                    node.Children.Add(childNode);
                }
            }

            foreach (NavigationEntry n in navigations.Where(a => !a.Metadata.IsCollection))
            {
                Type navType = GetNonProxyType(n.Metadata.ClrType);

                if (!tree.TryGetValue(navType, out DbNode? parentNode))
                {
                    parentNode = new DbNode() { Type = navType };
                    tree.TryAdd(navType, parentNode);
                }

                ;

                if (!parentNode.Children.Any(a => a.Type == node.Type))
                {
                    parentNode.Children.Add(node);
                }

                if (!node.Parents.Any(a => a.Type == parentNode.Type))
                {
                    node.Parents.Add(parentNode);
                }
            }
        }

        IEnumerable<KeyValuePair<Type, DbNode>> rootNodes = tree.Where(a => a.Value.Parents.Count == 0);
        var handledTypes = new Dictionary<Type, bool>();
        var bulkMehodEntriesList = new List<BulkMethodEntries>();

        bool TryAddNode(DbNode node)
        {
            if (node.Parents.All(a => handledTypes.TryGetValue(a.Type, out bool exists)))
            {
                if (!handledTypes.TryGetValue(node.Type, out bool exists))
                {
                    handledTypes.Add(node.Type, true);

                    foreach (KeyValuePair<string, List<object>> me in node.MethodEntries)
                    {
                        if (me.Value != null && me.Value.Count > 0)
                        {
                            bulkMehodEntriesList.Add(new BulkMethodEntries()
                            {
                                Type = node.Type,
                                MethodName = me.Key,
                                Entries = me.Value,
                            });
                        }
                    }
                }

                foreach (DbNode p in node.Children)
                {
                    TryAddNode(p);
                }

                return exists;
            }

            return false;
        }

        foreach (KeyValuePair<Type, DbNode> r in rootNodes)
        {
            TryAddNode(r.Value);
        }

        return bulkMehodEntriesList;
    }

    private static Type GetNonProxyType(Type type) => type.Namespace == "Castle.Proxies" ? type.BaseType! : type;
}