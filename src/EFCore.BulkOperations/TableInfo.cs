using EFCore.BulkOperations.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations;

/// <summary> Provides a list of information for EFCore.BulkOperations that is used internally to know what to do with the data source received </summary>
public sealed class TableInfo
{
    public string EscL { get; set; } = null!;

    public string EscR { get; set; } = null!;

    public string? Schema { get; set; }
    public string SchemaFormated => Schema != null ? $"{EscL}{Schema}{EscR}." : "";
    public string? TempSchema { get; set; }
    public string TempSchemaFormated => TempSchema != null ? $"{EscL}{TempSchema}{EscR}." : "";
    public string? TableName { get; set; }
    public string FullTableName => $"{SchemaFormated}{EscL}{TableName}{EscR}";
    public Dictionary<string, string> PrimaryKeysPropertyColumnNameDict { get; set; } = null!;
    public Dictionary<string, string> EntityPkPropertyColumnNameDict { get; set; } = null!;
    public bool HasSinglePrimaryKey { get; set; }
    public bool UpdateByPropertiesAreNullable { get; set; }

    internal string TempDbPrefix => BulkConfig.UseTempDb ? "#" : "";
    public string? TempTableSufix { get; set; }
    public string? TempTableName { get; set; }
    public string FullTempTableName => $"{TempSchemaFormated}{EscL}{TempDbPrefix}{TempTableName}{EscR}";
    public string FullTempOutputTableName => $"{SchemaFormated}{EscL}{TempDbPrefix}{TempTableName}Output{EscR}";

    public bool CreatedOutputTable => BulkConfig.SetOutputIdentity || BulkConfig.CalculateStats;

    public bool InsertToTempTable { get; set; }
    public string? IdentityColumnName { get; set; }

    public bool HasIdentity => IdentityColumnName != null;

    public bool HasTimeStampColumn => TimeStampColumnName != null;
    public ValueConverter? IdentityColumnConverter { get; set; }
    public bool HasOwnedTypes { get; set; }
    public bool HasAbstractList { get; set; }
    public bool LoadOnlyPkColumn { get; set; }
    public bool HasSpatialType { get; set; }
    public bool HasTemporalColumns { get; set; }
    public int NumberOfEntities { get; set; }

    public BulkConfig BulkConfig { get; set; } = null!;
    public Dictionary<string, string> OutputPropertyColumnNamesDict { get; set; } = new();
    public Dictionary<string, string> PropertyColumnNamesDict { get; set; } = new();
    public Dictionary<string, string> ColumnNamesTypesDict { get; set; } = new();
    public Dictionary<string, IProperty> ColumnToPropertyDictionary { get; set; } = new();
    public Dictionary<string, string> PropertyColumnNamesCompareDict { get; set; } = new();
    public Dictionary<string, string> PropertyColumnNamesUpdateDict { get; set; } = new();
    public Dictionary<string, FastProperty> FastPropertyDict { get; set; } = new();
    public Dictionary<string, INavigation> AllNavigationsDictionary { get; private set; } = null!;
    public Dictionary<string, INavigation> OwnedTypesDict { get; set; } = new();
    public HashSet<string> ShadowProperties { get; set; } = new HashSet<string>();
    public HashSet<string> DefaultValueProperties { get; set; } = new HashSet<string>();

    public Dictionary<string, string> ConvertiblePropertyColumnDict { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, ValueConverter> ConvertibleColumnConverterDict { get; set; } = new Dictionary<string, ValueConverter>();
    public Dictionary<string, int> DateTime2PropertiesPrecisionLessThen7Dict { get; set; } = new Dictionary<string, int>();

    public static string TimeStampOutColumnType => "varbinary(8)";
    public string? TimeStampPropertyName { get; set; }
    public string? TimeStampColumnName { get; set; }

    internal IList<object>? EntitiesSortedReference { get; set; } // Operation Merge writes In Output table first Existing that were Updated then for new that were Inserted so this makes sure order is same in list when need to set Output

    public StoreObjectIdentifier ObjectIdentifier { get; set; }

    public DbTransaction? DbTransaction { get; set; }

    public string SqlActionIud => $"EF_BULK_EXTENSIONS_MIT_MERGE_ACTION_IUD";

    public string OriginalIndexColumnName => $"EF_BULK_EXTENSIONS_MIT_ORIGINAL_INDEX";

    /// <summary> Creates an instance of TableInfo </summary>
    public static TableInfo CreateInstance<T>(DbContext context, Type? type, IList<T> entities, OperationType operationType, BulkConfig? bulkConfig)
    {
        TableInfo tableInfo = new TableInfo
        {
            EscL = context.GetAdapterDialect().EscL.ToString(),
            EscR = context.GetAdapterDialect().EscR.ToString(),
            NumberOfEntities = entities.Count,
            BulkConfig = bulkConfig ?? new BulkConfig(),
        };

        tableInfo.BulkConfig.OperationType = operationType;

        bool isExplicitTransaction = context.Database.GetDbConnection().State == ConnectionState.Open;

        if (tableInfo.BulkConfig.UseTempDb && !isExplicitTransaction && (operationType != OperationType.Insert || tableInfo.BulkConfig.SetOutputIdentity))
        {
            throw new InvalidOperationException("When 'UseTempDB' is set then BulkOperation has to be inside Transaction. " +
                                                "Otherwise destination table gets dropped too early because transaction ends before operation is finished."); // throws: 'Cannot access destination table'
        }

        bool isDeleteOperation = operationType == OperationType.Delete;
        tableInfo.LoadData(context, type, entities, isDeleteOperation);
        TableInfo createResult = tableInfo;

        return createResult;
    }

    #region Main

    /// <summary> Configures the table info based on entity data  </summary>
    public void LoadData<T>(DbContext context, Type? type, IList<T> entities, bool loadOnlyPkColumn)

    {
        LoadOnlyPkColumn = loadOnlyPkColumn;
        IEntityType? entityType = type is null ? null : context.Model.FindEntityType(type);

        if (entityType == null)
        {
            type = entities[0]?.GetType() ?? throw new ArgumentNullException(nameof(type));
            entityType = context.Model.FindEntityType(type);
            HasAbstractList = true;
        }

        if (entityType == null)
        {
            throw new InvalidOperationException($"DbContext does not contain EntitySet for Type: {type?.Name}");
        }

        IQueryBuilderSpecialization dialect = SqlAdaptersMapping.GetAdapterDialect(context);

        string? defaultSchema = dialect.DefaultSchema;

        string? customSchema = null;
        string? customTableName = null;

        if (BulkConfig.CustomDestinationTableName != null)
        {
            customTableName = BulkConfig.CustomDestinationTableName;

            if (customTableName.Contains('.'))
            {
                string[] tableNameSplitList = customTableName.Split('.');
                customSchema = tableNameSplitList[0];
                customTableName = tableNameSplitList[1];
            }
        }
        Schema = customSchema ?? entityType.GetSchema() ?? defaultSchema;
        string? entityTableName = entityType.GetTableName();
        TableName = customTableName ?? entityTableName;

        string? sourceSchema = null;
        string? sourceTableName = null;

        if (BulkConfig.CustomSourceTableName != null)
        {
            sourceTableName = BulkConfig.CustomSourceTableName;

            if (sourceTableName.Contains('.'))
            {
                string[] tableNameSplitList = sourceTableName.Split('.');
                sourceSchema = tableNameSplitList[0];
                sourceTableName = tableNameSplitList[1];
            }
            BulkConfig.UseTempDb = false;
        }

        TempSchema = sourceSchema ?? Schema;
        TempTableSufix = sourceTableName != null ? "" : "Temp";

        if (BulkConfig.UniqueTableNameTempDb)
        {
            // 8 chars of Guid as tableNameSufix to avoid same name collision with other tables
            TempTableSufix += Guid.NewGuid().ToString()[..8];
            // TODO Consider Hash                                                             
        }
        TempTableName = sourceTableName ?? $"{TableName}{TempTableSufix}";

        if (entityTableName is null)
        {
            throw new ArgumentException("Entity does not contain a table name");
        }

        ObjectIdentifier = StoreObjectIdentifier.Table(entityTableName, entityType.GetSchema());

        var allProperties = new List<IProperty>();

        foreach (IProperty entityProperty in entityType.GetProperties())
        {
            string? columnName = entityProperty.GetColumnName(ObjectIdentifier);
            bool isTemporalColumn = columnName is not null
                && entityProperty.IsShadowProperty()
                && entityProperty.ClrType == typeof(DateTime)
                && BulkConfig.TemporalColumns.Contains(columnName);

            HasTemporalColumns = HasTemporalColumns || isTemporalColumn;

            if (columnName == null || isTemporalColumn)
            {
                continue;
            }

            allProperties.Add(entityProperty);
            ColumnNamesTypesDict.Add(columnName, entityProperty.GetColumnType());
            ColumnToPropertyDictionary.Add(columnName, entityProperty);

            if (BulkConfig.DateTime2PrecisionForceRound)
            {
                IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IColumnMapping> columnMappings = entityProperty.GetTableColumnMappings();
                Microsoft.EntityFrameworkCore.Metadata.IColumnMapping? firstMapping = columnMappings.FirstOrDefault();
                string? columnType = firstMapping?.Column.StoreType;

                if ((columnType?.StartsWith("datetime2(") ?? false) && (!columnType?.EndsWith("7)") ?? false))
                {
                    string precisionText = columnType!.Substring(10, 1);
                    int precision = int.Parse(precisionText);
                    DateTime2PropertiesPrecisionLessThen7Dict.Add(firstMapping!.Property.Name, precision); // SqlBulkCopy does Floor instead of Round so Rounding done in memory
                }
            }
        }

        bool areSpecifiedUpdateByProperties = BulkConfig.UpdateByProperties?.Count > 0;
        Dictionary<string, string>? primaryKeys = entityType.FindPrimaryKey()?.Properties?.ToDictionary(a => a.Name, b => b.GetColumnName(ObjectIdentifier) ?? string.Empty);
        EntityPkPropertyColumnNameDict = primaryKeys ?? new Dictionary<string, string>();

        HasSinglePrimaryKey = primaryKeys?.Count == 1;
        PrimaryKeysPropertyColumnNameDict = areSpecifiedUpdateByProperties ? BulkConfig.UpdateByProperties?.ToDictionary(a => a, b => allProperties.First(p => p.Name == b).GetColumnName(ObjectIdentifier) ?? string.Empty) ?? new()
                                                                           : (primaryKeys ?? new Dictionary<string, string>());

        // load all derived type properties
        if (entityType.IsAbstract())
        {
            foreach (IEntityType derivedType in entityType.GetDirectlyDerivedTypes())
            {
                foreach (IProperty derivedProperty in derivedType.GetProperties())
                {
                    if (!allProperties.Contains(derivedProperty))
                    {
                        allProperties.Add(derivedProperty);
                    }
                }
            }
        }

        IEnumerable<INavigation> navigations = entityType.GetNavigations();
        AllNavigationsDictionary = navigations.ToDictionary(nav => nav.Name, nav => nav);

        IEnumerable<INavigation> ownedTypes = navigations.Where(a => a.TargetEntityType.IsOwned());
        HasOwnedTypes = ownedTypes.Any();
        OwnedTypesDict = ownedTypes.ToDictionary(a => a.Name, a => a);

        if (dialect.UseValueGenerationStrategyForIdentity)
        {
            foreach (IProperty property in allProperties)
            {
                if (SqlAdaptersMapping.DbServer(context).PropertyHasIdentity(property))
                {
                    IdentityColumnName = property.GetColumnName(ObjectIdentifier);
                    break;
                }
            }
        }

        if (dialect.DetectIdentityByIntegerPrimaryKey) // SQLite no ValueGenerationStrategy
        {
            // for HiLo on SqlServer was returning True when should be False
            IdentityColumnName = allProperties.SingleOrDefault(a => a.IsPrimaryKey() &&
                                                    a.ValueGenerated == ValueGenerated.OnAdd && // ValueGenerated equals OnAdd for nonIdentity column like Guid so take only number types
                                                    (a.ClrType.Name.StartsWith("Byte") ||
                                                     a.ClrType.Name.StartsWith("SByte") ||
                                                     a.ClrType.Name.StartsWith("Int") ||
                                                     a.ClrType.Name.StartsWith("UInt"))
                                              )?.GetColumnName(ObjectIdentifier);
        }

        // timestamp/row version properties are only set by the Db, the property has a Timestamp] Attribute or is configured in FluentAPI with .IsRowVersion()
        // They can be identified by the columne type "timestamp" or .IsConcurrencyToken in combination with .ValueGenerated == ValueGenerated.OnAddOrUpdate
        IEnumerable<IProperty> timeStampProperties;

        if (BulkConfig.IgnoreRowVersion)
        {
            timeStampProperties = new List<IProperty>();
        }
        else
        {
            timeStampProperties = allProperties.Where(a => a.IsConcurrencyToken && a.ValueGenerated == ValueGenerated.OnAddOrUpdate); // || a.GetColumnType() == timestampDbTypeName // removed as unnecessary and might not be correct
        }

        TimeStampColumnName = timeStampProperties.FirstOrDefault()?.GetColumnName(ObjectIdentifier); // can be only One
        TimeStampPropertyName = timeStampProperties.FirstOrDefault()?.Name; // can be only One
        IEnumerable<IProperty> allPropertiesExceptTimeStamp = allProperties.Except(timeStampProperties);
        IEnumerable<IProperty> properties = allPropertiesExceptTimeStamp.Where(a => a.GetComputedColumnSql() == null);

        IEnumerable<IProperty> propertiesWithDefaultValues = allPropertiesExceptTimeStamp.Where(a =>
            !a.IsShadowProperty() &&
            (a.GetDefaultValueSql() != null ||
             (a.GetDefaultValue() != null &&
              a.ValueGenerated != ValueGenerated.Never &&
              a.ClrType != typeof(Guid)) // Since .Net_6.0 in EF 'Guid' type has DefaultValue even when not explicitly defined with Annotation or FluentApi
            ));

        foreach (IProperty? propertyWithDefaultValue in propertiesWithDefaultValues)
        {
            Type propertyType = propertyWithDefaultValue.ClrType;
            object? instance = propertyType.IsValueType || propertyType.GetConstructor(Type.EmptyTypes) != null
                              ? Activator.CreateInstance(propertyType)
                              : null; // when type does not have parameterless constructor, like String for example, then default value is 'null'

            bool listHasAllDefaultValues = !entities.Any(a => a?.GetType().GetProperty(propertyWithDefaultValue.Name)?.GetValue(a, null)?.ToString() != instance?.ToString());
            // it is not feasible to have in same list simultaneously both entities groups With and Without default values, they are omitted OnInsert only if all have default values or if it is PK (like Guid DbGenerated)
            if (listHasAllDefaultValues || (PrimaryKeysPropertyColumnNameDict.ContainsKey(propertyWithDefaultValue.Name) && propertyType == typeof(Guid)))
            {
                DefaultValueProperties.Add(propertyWithDefaultValue.Name);
            }
        }

        IEnumerable<IProperty> propertiesOnCompare = allPropertiesExceptTimeStamp.Where(a => a.GetComputedColumnSql() == null);
        IEnumerable<IProperty> propertiesOnUpdate = allPropertiesExceptTimeStamp.Where(a => a.GetComputedColumnSql() == null);

        // TimeStamp prop. is last column in OutputTable since it is added later with varbinary(8) type in which Output can be inserted
        IEnumerable<IProperty> outputProperties = allPropertiesExceptTimeStamp.Where(a => a.GetColumnName(ObjectIdentifier) != null).Concat(timeStampProperties);
        OutputPropertyColumnNamesDict = outputProperties.ToDictionary(a => a.Name, b => b.GetColumnName(ObjectIdentifier)?.Replace("]", "]]") ?? string.Empty); // square brackets have to be escaped

        bool areSpecifiedPropertiesToInclude = BulkConfig.PropertiesToInclude?.Count > 0;
        bool areSpecifiedPropertiesToExclude = BulkConfig.PropertiesToExclude?.Count > 0;

        bool areSpecifiedPropertiesToIncludeOnCompare = BulkConfig.PropertiesToIncludeOnCompare?.Count > 0;
        bool areSpecifiedPropertiesToExcludeOnCompare = BulkConfig.PropertiesToExcludeOnCompare?.Count > 0;

        bool areSpecifiedPropertiesToIncludeOnUpdate = BulkConfig.PropertiesToIncludeOnUpdate?.Count > 0;
        bool areSpecifiedPropertiesToExcludeOnUpdate = BulkConfig.PropertiesToExcludeOnUpdate?.Count > 0;

        if (areSpecifiedPropertiesToInclude)
        {
            if (areSpecifiedUpdateByProperties) // Adds UpdateByProperties to PropertyToInclude if they are not already explicitly listed
            {
                if (BulkConfig.UpdateByProperties is not null)
                {
                    foreach (string updateByProperty in BulkConfig.UpdateByProperties)
                    {
                        if (!BulkConfig.PropertiesToInclude?.Contains(updateByProperty) ?? false)
                        {
                            BulkConfig.PropertiesToInclude?.Add(updateByProperty);
                        }
                    }
                }
            }
            else // Adds PrimaryKeys to PropertyToInclude if they are not already explicitly listed
            {
                foreach (KeyValuePair<string, string> primaryKey in PrimaryKeysPropertyColumnNameDict)
                {
                    if (!BulkConfig.PropertiesToInclude?.Contains(primaryKey.Key) ?? false)
                    {
                        BulkConfig.PropertiesToInclude?.Add(primaryKey.Key);
                    }
                }
            }
        }

        foreach (IProperty property in allProperties)
        {
            if (property.PropertyInfo != null) // skip Shadow Property
            {
                FastPropertyDict.Add(property.Name, FastProperty.GetOrCreate(property.PropertyInfo));
            }

            if (property.IsShadowProperty() && property.IsForeignKey())
            {
                // TODO: Does Shadow ForeignKey Property aways contain only one ForgeignKey? 
                PropertyInfo? navigationProperty = property.GetContainingForeignKeys().FirstOrDefault()?.DependentToPrincipal?.PropertyInfo;

                if (navigationProperty is not null)
                {
                    IEntityType? navigationEntityType = context.Model.FindEntityType(navigationProperty.PropertyType);
                    List<IProperty> navigationProperties = navigationEntityType?.GetProperties().Where(p => p.IsPrimaryKey()).ToList() ?? new();

                    foreach (IProperty? navEntityProperty in navigationProperties)
                    {
                        string fullName = navigationProperty.Name + "_" + navEntityProperty.Name;

                        if (!FastPropertyDict.ContainsKey(fullName) && navEntityProperty.PropertyInfo is not null)
                        {
                            FastPropertyDict.Add(fullName, FastProperty.GetOrCreate(navEntityProperty.PropertyInfo));
                        }
                    }
                }
            }

            ValueConverter? converter = property.GetTypeMapping().Converter;

            if (converter is not null)
            {
                string columnName = property.GetColumnName(ObjectIdentifier) ?? string.Empty;
                ConvertiblePropertyColumnDict.Add(property.Name, columnName);
                ConvertibleColumnConverterDict.Add(columnName, converter);

                if (columnName == IdentityColumnName)
                {
                    IdentityColumnConverter = converter;
                }
            }
        }

        UpdateByPropertiesAreNullable = properties.Any(a => PrimaryKeysPropertyColumnNameDict.ContainsKey(a.Name) && a.IsNullable);

        if (areSpecifiedPropertiesToInclude || areSpecifiedPropertiesToExclude)
        {
            if (areSpecifiedPropertiesToInclude && areSpecifiedPropertiesToExclude)
            {
                throw new MultiplePropertyListSetException(nameof(BulkConfig.PropertiesToInclude), nameof(BulkConfig.PropertiesToExclude));
            }

            if (areSpecifiedPropertiesToInclude)
            {
                properties = properties.Where(a => BulkConfig.PropertiesToInclude?.Contains(a.Name) ?? false);
                ValidateSpecifiedPropertiesList(BulkConfig.PropertiesToInclude, nameof(BulkConfig.PropertiesToInclude));
            }

            if (areSpecifiedPropertiesToExclude)
            {
                properties = properties.Where(a => !BulkConfig.PropertiesToExclude?.Contains(a.Name) ?? false);
                ValidateSpecifiedPropertiesList(BulkConfig.PropertiesToExclude, nameof(BulkConfig.PropertiesToExclude));
            }
        }

        if (areSpecifiedPropertiesToIncludeOnCompare || areSpecifiedPropertiesToExcludeOnCompare)
        {
            if (areSpecifiedPropertiesToIncludeOnCompare && areSpecifiedPropertiesToExcludeOnCompare)
            {
                throw new MultiplePropertyListSetException(nameof(BulkConfig.PropertiesToIncludeOnCompare), nameof(BulkConfig.PropertiesToExcludeOnCompare));
            }

            if (areSpecifiedPropertiesToIncludeOnCompare)
            {
                propertiesOnCompare = propertiesOnCompare.Where(a => BulkConfig.PropertiesToIncludeOnCompare?.Contains(a.Name) ?? false);
                ValidateSpecifiedPropertiesList(BulkConfig.PropertiesToIncludeOnCompare, nameof(BulkConfig.PropertiesToIncludeOnCompare));
            }

            if (areSpecifiedPropertiesToExcludeOnCompare)
            {
                propertiesOnCompare = propertiesOnCompare.Where(a => !BulkConfig.PropertiesToExcludeOnCompare?.Contains(a.Name) ?? false);
                ValidateSpecifiedPropertiesList(BulkConfig.PropertiesToExcludeOnCompare, nameof(BulkConfig.PropertiesToExcludeOnCompare));
            }
        }
        else
        {
            propertiesOnCompare = properties;
        }

        if (areSpecifiedPropertiesToIncludeOnUpdate || areSpecifiedPropertiesToExcludeOnUpdate)
        {
            if (areSpecifiedPropertiesToIncludeOnUpdate && areSpecifiedPropertiesToExcludeOnUpdate)
            {
                throw new MultiplePropertyListSetException(nameof(BulkConfig.PropertiesToIncludeOnUpdate), nameof(BulkConfig.PropertiesToExcludeOnUpdate));
            }

            if (areSpecifiedPropertiesToIncludeOnUpdate)
            {
                propertiesOnUpdate = propertiesOnUpdate.Where(a => BulkConfig.PropertiesToIncludeOnUpdate?.Contains(a.Name) ?? false);
                ValidateSpecifiedPropertiesList(BulkConfig.PropertiesToIncludeOnUpdate, nameof(BulkConfig.PropertiesToIncludeOnUpdate));
            }

            if (areSpecifiedPropertiesToExcludeOnUpdate)
            {
                propertiesOnUpdate = propertiesOnUpdate.Where(a => !BulkConfig.PropertiesToExcludeOnUpdate?.Contains(a.Name) ?? false);
                ValidateSpecifiedPropertiesList(BulkConfig.PropertiesToExcludeOnUpdate, nameof(BulkConfig.PropertiesToExcludeOnUpdate));
            }
        }
        else
        {
            propertiesOnUpdate = properties;

            if (BulkConfig.UpdateByProperties != null) // to remove NonIdentity PK like Guid from SET ID = ID, ...
            {
                propertiesOnUpdate = propertiesOnUpdate.Where(a => !BulkConfig.UpdateByProperties.Contains(a.Name));
            }
            else if (primaryKeys != null)
            {
                propertiesOnUpdate = propertiesOnUpdate.Where(a => !primaryKeys.ContainsKey(a.Name));
            }
        }

        PropertyColumnNamesCompareDict = propertiesOnCompare.ToDictionary(a => a.Name, b => b.GetColumnName(ObjectIdentifier)?.Replace("]", "]]") ?? string.Empty);
        PropertyColumnNamesUpdateDict = propertiesOnUpdate.ToDictionary(a => a.Name, b => b.GetColumnName(ObjectIdentifier)?.Replace("]", "]]") ?? string.Empty);

        if (loadOnlyPkColumn)
        {
            if (PrimaryKeysPropertyColumnNameDict.Count == 0)
            {
                throw new InvalidBulkConfigException("If no PrimaryKey is defined operation requres bulkConfig set with 'UpdatedByProperties'.");
            }

            PropertyColumnNamesDict = properties.Where(a => PrimaryKeysPropertyColumnNameDict.ContainsKey(a.Name)).ToDictionary(a => a.Name, b => b.GetColumnName(ObjectIdentifier)?.Replace("]", "]]") ?? string.Empty);
        }
        else
        {
            PropertyColumnNamesDict = properties.ToDictionary(a => a.Name, b => b.GetColumnName(ObjectIdentifier)?.Replace("]", "]]") ?? string.Empty);
            ShadowProperties = new HashSet<string>(properties.Where(p => p.IsShadowProperty() && !p.IsForeignKey()).Select(p => p.GetColumnName(ObjectIdentifier) ?? string.Empty));

            foreach (INavigation? navigation in entityType.GetNavigations().Where(a => !a.IsCollection && !a.TargetEntityType.IsOwned()))
            {
                if (navigation.PropertyInfo is not null)
                {
                    FastPropertyDict.Add(navigation.Name, FastProperty.GetOrCreate(navigation.PropertyInfo));
                }
            }

            if (HasOwnedTypes)  // Support owned entity property update. TODO: Optimize
            {
                foreach (INavigation? navigationProperty in ownedTypes)
                {
                    PropertyInfo? property = navigationProperty.PropertyInfo;
                    FastPropertyDict.Add(property!.Name, FastProperty.GetOrCreate(property));

                    // If the OwnedType is mapped to the separate table, don't try merge it into its owner
                    if (OwnedTypeUtil.IsOwnedInSameTableAsOwner(navigationProperty) == false)
                    {
                        continue;
                    }

                    //Type navOwnedType = type?.Assembly.GetType(property.PropertyType.FullName!) ?? throw new ArgumentException("Unable to determine Type"); // was not used
                    IEntityType? ownedEntityType = context.Model.FindEntityType(property.PropertyType);

                    if (ownedEntityType == null) // when entity has more then one ownedType (e.g. Address HomeAddress, Address WorkAddress) or one ownedType is in multiple Entities like Audit is usually.
                    {
                        ownedEntityType = context.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType == property.PropertyType && x.Name.StartsWith(entityType.Name + "." + property.Name + "#"));
                    }
                    List<IProperty> ownedEntityProperties = ownedEntityType?.GetProperties().ToList() ?? new();
                    var ownedEntityPropertyNameColumnNameDict = new Dictionary<string, string>();

                    foreach (IProperty? ownedEntityProperty in ownedEntityProperties)
                    {
                        string columnName = ownedEntityProperty.GetColumnName(ObjectIdentifier) ?? string.Empty;

                        if (!ownedEntityProperty.IsPrimaryKey())
                        {
                            ownedEntityPropertyNameColumnNameDict.Add(ownedEntityProperty.Name, columnName);
                            string ownedEntityPropertyFullName = property.Name + "_" + ownedEntityProperty.Name;

                            if (!FastPropertyDict.ContainsKey(ownedEntityPropertyFullName) && ownedEntityProperty.PropertyInfo is not null)
                            {
                                FastPropertyDict.Add(ownedEntityPropertyFullName, FastProperty.GetOrCreate(ownedEntityProperty.PropertyInfo));
                            }
                        }

                        ValueConverter? converter = ownedEntityProperty.GetValueConverter();

                        if (converter != null)
                        {
                            ConvertibleColumnConverterDict.Add($"{navigationProperty.Name}_{ownedEntityProperty.Name}", converter);
                        }

                        ColumnNamesTypesDict[columnName] = ownedEntityProperty.GetColumnType();
                    }
                    PropertyInfo[] ownedProperties = property.PropertyType.GetProperties();

                    foreach (PropertyInfo ownedProperty in ownedProperties)
                    {
                        if (ownedEntityPropertyNameColumnNameDict.TryGetValue(ownedProperty.Name, out string? columnName))
                        {
                            string ownedPropertyFullName = property.Name + "." + ownedProperty.Name;
                            Type ownedPropertyType = Nullable.GetUnderlyingType(ownedProperty.PropertyType) ?? ownedProperty.PropertyType;

                            bool doAddProperty = true;

                            if (areSpecifiedPropertiesToInclude && !(BulkConfig.PropertiesToInclude?.Contains(ownedPropertyFullName) ?? false))
                            {
                                doAddProperty = false;
                            }

                            if (areSpecifiedPropertiesToExclude && (BulkConfig.PropertiesToExclude?.Contains(ownedPropertyFullName) ?? false))
                            {
                                doAddProperty = false;
                            }

                            if (doAddProperty)
                            {
                                PropertyColumnNamesDict.Add(ownedPropertyFullName, columnName);
                                PropertyColumnNamesCompareDict.Add(ownedPropertyFullName, columnName);
                                PropertyColumnNamesUpdateDict.Add(ownedPropertyFullName, columnName);
                                OutputPropertyColumnNamesDict.Add(ownedPropertyFullName, columnName);
                            }
                        }
                    }
                }
            }
        }
    }

    internal void ValidateSpecifiedPropertiesList(List<string>? specifiedPropertiesList, string specifiedPropertiesListName)

    {
        if (specifiedPropertiesList is not null)
        {
            foreach (string configSpecifiedPropertyName in specifiedPropertiesList)
            {

                if (!FastPropertyDict.Any(a => a.Key == configSpecifiedPropertyName) &&
                    !configSpecifiedPropertyName.Contains('.') && // Those with dot "." skiped from validating for now since FastPropertyDict here does not contain them
                    !(specifiedPropertiesListName == nameof(BulkConfig.PropertiesToIncludeOnUpdate) && configSpecifiedPropertyName == "") && // In PropsToIncludeOnUpdate empty is allowed as config for skipping Update
                    !BulkConfig.TemporalColumns.Contains(configSpecifiedPropertyName)
                    )
                {
                    throw new InvalidOperationException($"PropertyName '{configSpecifiedPropertyName}' specified in '{specifiedPropertiesListName}' not found in Properties.");
                }
            }
        }
    }

    #endregion

    #region SqlCommands

    public async Task<MergeActionCounts> GetMergeActionCounts(DbContext context, bool isAsync, CancellationToken cancellationToken)
    {
        string commandText = $"SELECT COUNT (*) FROM {FullTempOutputTableName} WHERE {EscL}{SqlActionIud}{EscR} = 'I';\n"
                          + $"SELECT COUNT (*) FROM {FullTempOutputTableName} WHERE {EscL}{SqlActionIud}{EscR} = 'U' ;\n"
                          + $"SELECT COUNT (*) FROM {FullTempOutputTableName} WHERE {EscL}{SqlActionIud}{EscR} = 'D';";

        int inserted = -1;
        int updated = -1;
        int deleted = -1;

        if (isAsync)
        {
            await GetMergeActionCountsInternalAsync().ConfigureAwait(false);
        }
        else
        {
            GetMergeActionCountsInternal();
        }

        var mergeCounts = new MergeActionCounts(inserted, updated, deleted);

        return mergeCounts;

        async Task GetMergeActionCountsInternalAsync()
        {
#pragma warning disable CA2007

            await using DbCommand command = context.Database.GetDbConnection().CreateCommand();

            command.CommandText = commandText;

            if (command.Connection!.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            if (context.Database.CurrentTransaction != null)
            {
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            }

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            inserted = reader.GetInt32(0);
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            updated = reader.GetInt32(0);
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            deleted = reader.GetInt32(0);
#pragma warning restore CA2007
        }

        void GetMergeActionCountsInternal()
        {
            using DbCommand command = context.Database.GetDbConnection().CreateCommand();

            command.CommandText = commandText;

            if (command.Connection!.State != ConnectionState.Open)
            {
                command.Connection.Open();
            }

            if (context.Database.CurrentTransaction != null)
            {
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            }

            using DbDataReader reader = command.ExecuteReader();

            reader.Read();
            inserted = reader.GetInt32(0);
            reader.NextResult();

            reader.Read();
            updated = reader.GetInt32(0);
            reader.NextResult();

            reader.Read();
            deleted = reader.GetInt32(0);
        }
    }

    #endregion

    /// <summary> Returns the unique property values </summary>
    public static string GetUniquePropertyValues(object entity, List<string> propertiesNames, Dictionary<string, FastProperty> fastPropertyDict)
    {
        StringBuilder uniqueBuilder = new(1024);
        string delimiter = "_"; // TODO: Consider making it Config-urable

        foreach (string propertyName in propertiesNames)
        {
            object? property = fastPropertyDict[propertyName].Get(entity);

            if (property is Array propertyArray)
            {
                foreach (object? element in propertyArray)
                {
                    uniqueBuilder.Append(element?.ToString() ?? "null");
                }
            }
            else
            {
                uniqueBuilder.Append(property?.ToString() ?? "null");
            }

            uniqueBuilder.Append(delimiter);
        }
        string result = uniqueBuilder.ToString();
        result = result[0..^1]; // removes last delimiter

        return result;
    }

    #region ReadProcedures

    /// <summary> Configures the bulk read column names for the table info </summary>
    public Dictionary<string, string> ConfigureBulkReadTableInfo()
    {
        InsertToTempTable = true;

        Dictionary<string, string> previousPropertyColumnNamesDict = PropertyColumnNamesDict;
        BulkConfig.PropertiesToInclude = PrimaryKeysPropertyColumnNameDict.Select(a => a.Key).ToList();
        PropertyColumnNamesDict = PropertyColumnNamesDict.Where(a => PrimaryKeysPropertyColumnNameDict.ContainsKey(a.Key)).ToDictionary(a => a.Key, a => a.Value);
        Dictionary<string, string> configureResult = previousPropertyColumnNamesDict;

        return configureResult;
    }

    public void UpdateReadEntities<T>(IList<T> entities, IList<T> existingEntities, DbContext context)
    {
        List<string> propertyNames = PropertyColumnNamesDict.Keys.ToList();

        if (HasOwnedTypes)
        {
            foreach (string ownedTypeName in OwnedTypesDict.Keys)
            {
                PropertyInfo[] ownedTypeProperties = OwnedTypesDict[ownedTypeName].ClrType.GetProperties();

                foreach (PropertyInfo ownedTypeProperty in ownedTypeProperties)
                {
                    propertyNames.Remove(ownedTypeName + "." + ownedTypeProperty.Name);
                }
                propertyNames.Add(ownedTypeName);
            }
        }

        List<string> selectByPropertyNames = PropertyColumnNamesDict.Keys
            .Where(a => PrimaryKeysPropertyColumnNameDict.ContainsKey(a)).ToList();

        Dictionary<string, T> existingEntitiesDict = new();

        foreach (T? existingEntity in existingEntities)
        {
            string uniqueProperyValues = GetUniquePropertyValues(existingEntity!, selectByPropertyNames, FastPropertyDict);
            existingEntitiesDict.TryAdd(uniqueProperyValues, existingEntity);
        }

        bool matchEntitiesByPosition = SqlAdaptersMapping.GetAdapterDialect(context).MatchEntitiesByPosition;

        for (int i = 0; i < NumberOfEntities; i++)
        {
            T entity = entities[i];
            string uniqueProperyValues = GetUniquePropertyValues(entity!, selectByPropertyNames, FastPropertyDict);

            existingEntitiesDict.TryGetValue(uniqueProperyValues, out T? existingEntity);

            if (existingEntity == null && matchEntitiesByPosition && i < existingEntities.Count)
            {
                existingEntity = existingEntities[i]; // TODO check if BinaryImport with COPY on Postgres preserves order
            }

            if (existingEntity != null)
            {
                foreach (string propertyName in propertyNames)
                {
                    if (FastPropertyDict.ContainsKey(propertyName))
                    {
                        object? propertyValue = FastPropertyDict[propertyName].Get(existingEntity);
                        FastPropertyDict[propertyName].Set(entity!, propertyValue);
                    }
                    else
                    {
                        //TODO: Shadow FK property update
                    }

                }
            }
        }
    }

    public void ReplaceReadEntities<T>(IList<T> entities, IList<T> existingEntities)
    {
        entities.Clear();

        foreach (T? existingEntity in existingEntities)
        {
            entities.Add(existingEntity);
        }
    }

    #endregion

    /// <summary> Sets the identity preserve order </summary>
    public void CheckToSetIdentityForPreserveOrder<T>(TableInfo tableInfo, IList<T> entities, bool reset = false)
    {
        string identityPropertyName = PropertyColumnNamesDict.SingleOrDefault(a => a.Value == IdentityColumnName).Key;

        bool doSetIdentityColumnsForInsertOrder = BulkConfig.PreserveInsertOrder &&
                                                  entities.Count > 1 &&
                                                  PrimaryKeysPropertyColumnNameDict?.Count == 1 &&
                                                  PrimaryKeysPropertyColumnNameDict?.Select(a => a.Value).First() == IdentityColumnName;

        OperationType operationType = tableInfo.BulkConfig.OperationType;

        if (doSetIdentityColumnsForInsertOrder)
        {
            if (operationType == OperationType.Insert) // Insert should either have all zeros for automatic order, or they can be manually set
            {
                object? propertyValue = FastPropertyDict[identityPropertyName].Get(entities[0]!);
                long identityValue = Convert.ToInt64(IdentityColumnConverter != null ? IdentityColumnConverter.ConvertToProvider(propertyValue) : propertyValue);

                if (identityValue != 0) // (to check it fast, condition for all 0s is only done on first one)
                {
                    doSetIdentityColumnsForInsertOrder = false;
                }
            }
        }

        if (doSetIdentityColumnsForInsertOrder)
        {
            bool sortEntities = !reset && BulkConfig.SetOutputIdentity &&
                                (operationType == OperationType.Update || operationType == OperationType.InsertOrUpdate || operationType == OperationType.InsertOrUpdateOrDelete);
            var entitiesExistingDict = new Dictionary<long, T>();
            var entitiesNew = new List<T>();

            long i = -entities.Count;

            foreach (T? entity in entities)
            {
                FastProperty identityFastProperty = FastPropertyDict[identityPropertyName];
                object? propertyValue = identityFastProperty.Get(entity!);
                long identityValue = Convert.ToInt64(IdentityColumnConverter != null ? IdentityColumnConverter.ConvertToProvider(propertyValue) : propertyValue);

                if (identityValue == 0 ||         // set only zero(0) values
                    (identityValue < 0 && reset)) // set only negative(-N) values if reset
                {
                    long value = reset ? 0 : i;
                    object idValue;
                    Type idType = identityFastProperty.Property.PropertyType;

                    if (idType == typeof(ushort))
                    {
                        idValue = (ushort)value;
                    }

                    if (idType == typeof(short))
                    {
                        idValue = (short)value;
                    }
                    else if (idType == typeof(uint))
                    {
                        idValue = (uint)value;
                    }
                    else if (idType == typeof(int))
                    {
                        idValue = (int)value;
                    }
                    else if (idType == typeof(ulong))
                    {
                        idValue = (ulong)value;
                    }
                    else if (idType == typeof(decimal))
                    {
                        idValue = (decimal)value;
                    }
                    else
                    {
                        idValue = value; // type 'long' left as default
                    }

                    identityFastProperty.Set(entity!, IdentityColumnConverter != null ? IdentityColumnConverter.ConvertFromProvider(idValue) : idValue);
                    i++;
                }

                if (sortEntities)
                {
                    if (identityValue != 0)
                    {
                        entitiesExistingDict.Add(identityValue, entity); // first load existing ones
                    }
                    else
                    {
                        entitiesNew.Add(entity);
                    }
                }
            }

            if (sortEntities)
            {
                List<T> entitiesSorted = entitiesExistingDict.OrderBy(a => a.Key).Select(a => a.Value).ToList();
                entitiesSorted.AddRange(entitiesNew); // then append new ones
                tableInfo.EntitiesSortedReference = entitiesSorted.Cast<object>().ToList();
            }
        }
    }

    /// <summary> Loads the output entities </summary>
    public List<T> LoadOutputEntities<T>(DbContext context, Type type, string sqlSelect) where T : class
    {
        List<T> existingEntities;

        if (typeof(T) == type)
        {
            Expression<Func<DbContext, IQueryable<T>>> expression = GetQueryExpression<T>(sqlSelect, false);
            Func<DbContext, IEnumerable<T>> compiled = EF.CompileQuery(expression); // instead using Compiled queries
            existingEntities = compiled(context).ToList();
        }
        else // TODO: Consider removing
        {
            Expression<Func<DbContext, IEnumerable>> expression = GetQueryExpression(type, sqlSelect, false);
            Func<DbContext, IEnumerable> compiled = EF.CompileQuery(expression); // instead using Compiled queries
            existingEntities = compiled(context).Cast<T>().ToList();
        }
        List<T> loadResult = existingEntities;

        return loadResult;
    }

    internal void UpdateEntitiesIdentity<T>(TableInfo tableInfo, IList<T> entities, IList<object> entitiesWithOutputIdentity)
    {
        string? identifierPropertyName = IdentityColumnName != null ? OutputPropertyColumnNamesDict.SingleOrDefault(a => a.Value == IdentityColumnName).Key // it Identity autoincrement 
                                                                : PrimaryKeysPropertyColumnNameDict.FirstOrDefault().Key;                               // or PK with default sql value

        if (BulkConfig.PreserveInsertOrder) // Updates Db changed Columns in entityList
        {
            int countDiff = entities.Count - entitiesWithOutputIdentity.Count;

            if (countDiff > 0) // When some ommited from Merge because of TimeStamp conflict then changes are not loaded but output is set in TimeStampInfo
            {
                tableInfo.BulkConfig.TimeStampInfo = new TimeStampInfo
                {
                    NumberOfSkippedForUpdate = countDiff,
                    EntitiesOutput = entitiesWithOutputIdentity.ToList()
                };

                return;
            }


            Dictionary<string, string>.KeyCollection customPk = tableInfo.PrimaryKeysPropertyColumnNameDict.Keys;

            if (countDiff < 0)
            {
                // This might happen in case of BulkInsertOrUpdate with custom UpdateBy properties, when there are multiple matching rows in table which is updated
                // for more see: https://github.com/borisdj/EFCore.BulkOperations/issues/1251
                // In case of setting output identity, we cannot decide which id we should use (as there might be multiple rows in output table, which 'belong' to only one row in source table).

                List<PrimaryKeysPropertyColumnNameValues> nonUniqueKeys = entitiesWithOutputIdentity.GroupBy(x => new PrimaryKeysPropertyColumnNameValues(customPk.Select(c => FastPropertyDict[c].Get(x)))).Where(x => x.Count() > 1).Select(x => x.Key).ToList();

                throw new BulkOperationsException(BulkOperationsExceptionType.CannotSetOutputIdentityForNonUniqueUpdateByProperties,
                                                  "Items were Inserted/Updated successfully in db, but we cannot set output identity correctly since single source row(s) matched multiple rows in db. "
                                                  + "Keys which matched more rows: "
                                                  + string.Join("\n", nonUniqueKeys.Select(x => x.ToLogString())));
            }

            if (tableInfo.EntitiesSortedReference != null)
            {
                entities = tableInfo.EntitiesSortedReference.Cast<T>().ToList();
            }


            // (UpsertOrderTest) fix for BulkInsertOrUpdate assigns wrong output IDs when PreserveInsertOrder = true and SetOutputIdentity = true
            bool setByDictionary = !(customPk.Count == 1 && customPk.First() == identifierPropertyName)
                                  && (tableInfo.BulkConfig.OperationType == OperationType.Update
                                      || tableInfo.BulkConfig.OperationType == OperationType.InsertOrUpdate
                                      || tableInfo.BulkConfig.OperationType == OperationType.InsertOrUpdateOrDelete);

            Dictionary<PrimaryKeysPropertyColumnNameValues, T> entitiesDict;

            if (setByDictionary)
            {
                entitiesDict = new Dictionary<PrimaryKeysPropertyColumnNameValues, T>();

                foreach (T? entity in entities)
                {
                    PrimaryKeysPropertyColumnNameValues customPkValue = new(customPk.Select(c => FastPropertyDict[c].Get(entity!)));
                    entitiesDict.Add(customPkValue, entity);
                }
            }
            else
            {
                // we will not be using the dictionary in the loop below.
                entitiesDict = null!;
            }

            for (int i = 0; i < NumberOfEntities; i++)
            {
                T entityToBeFilled;
                object elementFromOutputTable = entitiesWithOutputIdentity.ElementAt(i);

                if (setByDictionary)
                {
                    PrimaryKeysPropertyColumnNameValues customPkOutputValue = new(customPk.Select(c => FastPropertyDict[c].Get(elementFromOutputTable)));
                    entityToBeFilled = entitiesDict[customPkOutputValue]!;
                }
                else
                {
                    // We rely on the order:
                    entityToBeFilled = entities.ElementAt(i)!;
                }

                if (identifierPropertyName != null)
                {
                    bool selectOnlyIdentityColumn = false;
                    object? identityPropertyValue = selectOnlyIdentityColumn ? elementFromOutputTable : FastPropertyDict[identifierPropertyName].Get(elementFromOutputTable);
                    FastPropertyDict[identifierPropertyName].Set(entityToBeFilled, identityPropertyValue);
                }

                if (TimeStampColumnName != null) // timestamp/rowversion is also generated by the SqlServer so if exist should be updated as well
                {
                    string timeStampPropertyName = OutputPropertyColumnNamesDict.SingleOrDefault(a => a.Value == TimeStampColumnName).Key;
                    object? timeStampPropertyValue = FastPropertyDict[timeStampPropertyName].Get(elementFromOutputTable);
                    FastPropertyDict[timeStampPropertyName].Set(entityToBeFilled, timeStampPropertyValue);
                }

                IEnumerable<string> propertiesToLoad = tableInfo.OutputPropertyColumnNamesDict.Keys.Where(a => a != identifierPropertyName && a != TimeStampColumnName && // already loaded in segmet above
                                                                                               (tableInfo.DefaultValueProperties.Contains(a) ||           // add Computed and DefaultValues
                                                                                                !tableInfo.PropertyColumnNamesDict.ContainsKey(a)));      // remove others since already have same have (could be omited)

                foreach (string? outputPropertyName in propertiesToLoad)
                {
                    object? propertyValue = FastPropertyDict[outputPropertyName].Get(elementFromOutputTable);
                    FastPropertyDict[outputPropertyName].Set(entityToBeFilled, propertyValue);
                }
            }
        }
        else // Clears entityList and then refills it with loaded entites from Db
        {
            entities.Clear();

            if (typeof(T) == entitiesWithOutputIdentity.FirstOrDefault()?.GetType())
            {
                ((List<T>)entities).AddRange(entitiesWithOutputIdentity.Cast<T>().ToList());
            }
            else
            {
                List<object> entitiesObjects = entities.Cast<object>().ToList();
                entitiesObjects.AddRange(entitiesWithOutputIdentity);
            }
        }
    }

    internal void UpdateEntitiesIdentityByMap<T>(TableInfo tableInfo, IList<T> entities, List<IndexToGeneratedId> indexToGeneratedIds)
    {
        string? identifierPropertyName = IdentityColumnName != null ? OutputPropertyColumnNamesDict.SingleOrDefault(a => a.Value == IdentityColumnName).Key // it Identity autoincrement 
                                         : PrimaryKeysPropertyColumnNameDict.FirstOrDefault().Key;                                                      // or PK with default sql value

        string timeStampPropertyName = OutputPropertyColumnNamesDict.SingleOrDefault(a => a.Value == TimeStampColumnName).Key;

        Dictionary<int, List<IndexToGeneratedId>> mappingDictionary = indexToGeneratedIds.GroupBy(x => x.OriginalIndex).ToDictionary(x => x.Key, x => x.ToList());

        if (mappingDictionary.Any(x => x.Value.Count > 1))
        {
            // This might happen in case of BulkInsertOrUpdate with custom UpdateBy properties, when there are multiple matching rows in table which is updated
            // for more see: https://github.com/borisdj/EFCore.BulkOperations/issues/1251
            // In case of setting output identity, we cannot decide which id we should use (as there might be multiple rows in output table, which 'belong' to only one row in source table).

            Dictionary<string, string>.KeyCollection customPk = tableInfo.PrimaryKeysPropertyColumnNameDict.Keys;
            List<T> nonUniqueEntities = mappingDictionary.Where(x => x.Value.Count > 1).Select(x => x.Key).Select(x => entities[x]).ToList();

            List<PrimaryKeysPropertyColumnNameValues> nonUniqueKeys = nonUniqueEntities.Select(x => new PrimaryKeysPropertyColumnNameValues(customPk.Select(c => FastPropertyDict[c].Get(x!)))).ToList();

            throw new BulkOperationsException(BulkOperationsExceptionType.CannotSetOutputIdentityForNonUniqueUpdateByProperties,
                                              "Items were Inserted/Updated successfully in db, but we cannot set output identity correctly since single source row(s) matched multiple rows in db. "
                                              + "Keys which matched more rows: "
                                              + string.Join("\n", nonUniqueKeys.Select(x => x.ToLogString())));
        }

        for (int index = 0; index < NumberOfEntities; index++)
        {
            T entityToBeFilled = entities[index]!;

            if (!mappingDictionary.TryGetValue(index, out List<IndexToGeneratedId>? mapping))
            {
                // If any item is excluded from MERGE because of TimeStamp conflict
                // then the output identities are not loaded but output is set in TimeStampInfo
                tableInfo.BulkConfig.TimeStampInfo = new TimeStampInfo
                {
                    NumberOfSkippedForUpdate = entities.Count - mappingDictionary.Count,
                    EntitiesOutput = indexToGeneratedIds.Cast<object>().ToList(),
                };

                return;
            }

            if (identifierPropertyName != null)
            {
                object generatedId = mapping.Single().GeneratedId;
                FastPropertyDict[identifierPropertyName].Set(entityToBeFilled, generatedId);
            }

            if (HasTimeStampColumn) // timestamp/rowversion is also generated by the SqlServer so if it exist should be updated as well
            {
                object? timestampValue = mapping.Single().GeneratedTimestamp;
                FastPropertyDict[timeStampPropertyName].Set(entityToBeFilled, timestampValue);
            }
        }
    }

    // Compiled queries created manually to avoid EF Memory leak bug when using EF with dynamic SQL:
    // https://github.com/borisdj/EFCore.BulkOperations/issues/73
    // Once the following Issue gets fixed(expected in EF 3.0) this can be replaced with code segment: DirectQuery
    // https://github.com/aspnet/EntityFrameworkCore/issues/12905

    #region CompiledQuery

    public async Task LoadOutputDataAsync<T>(DbContext context, Type type, IList<T> entities, TableInfo tableInfo, bool isAsync, CancellationToken cancellationToken) where T : class
    {
        bool hasIdentity = OutputPropertyColumnNamesDict.Any(a => a.Value == IdentityColumnName) ||
                           (tableInfo.HasSinglePrimaryKey && tableInfo.DefaultValueProperties.Contains(tableInfo.PrimaryKeysPropertyColumnNameDict.FirstOrDefault().Key));

        if (BulkConfig.SetOutputIdentity && hasIdentity)
        {
            if (BulkConfig.UseOriginalIndexToIdentityMappingColumn)
            {
                List<IndexToGeneratedId> map = isAsync ? await QueryOutputTableForIndexToIdMapping(context, isAsync, cancellationToken).ConfigureAwait(false)
                              : QueryOutputTableForIndexToIdMapping(context, isAsync, cancellationToken).GetAwaiter().GetResult();

                UpdateEntitiesIdentityByMap(tableInfo, entities, map);
            }
            else
            {
                string sqlQuery = SqlAdaptersMapping.DbServer(context).QueryBuilder.SelectFromOutputTable(this);
                //var entitiesWithOutputIdentity = await QueryOutputTableAsync<T>(context, sqlQuery).ToListAsync(cancellationToken).ConfigureAwait(false); // TempFIX
                List<object> entitiesWithOutputIdentity = QueryOutputTable(context, type, sqlQuery).Cast<object>().ToList();
                //var entitiesWithOutputIdentity = (typeof(T) == type) ? QueryOutputTable<object>(context, sqlQuery).ToList() : QueryOutputTable(context, type, sqlQuery).Cast<object>().ToList();

                UpdateEntitiesIdentity(tableInfo, entities, entitiesWithOutputIdentity);
            }
        }

        if (BulkConfig.CalculateStats)
        {
            MergeActionCounts mergeCounts = isAsync ? await GetMergeActionCounts(context, isAsync: true, cancellationToken).ConfigureAwait(false)
                                  : GetMergeActionCounts(context, isAsync: false, cancellationToken).GetAwaiter().GetResult();
            BulkConfig.StatsInfo = new StatsInfo
            {
                StatsNumberUpdated = mergeCounts.Updated,
                StatsNumberDeleted = mergeCounts.Deleted,
                StatsNumberInserted = mergeCounts.Inserted,
            };
        }
    }

    internal async Task<List<IndexToGeneratedId>> QueryOutputTableForIndexToIdMapping(DbContext context, bool isAsync, CancellationToken cancellationToken)
    {
        bool shouldLoadAlsoTimestamp = false;
        string? idColumn = HasIdentity ? IdentityColumnName : PrimaryKeysPropertyColumnNameDict.Values.Single();
        string identityColumn = $",{EscL}{idColumn}{EscR}";
        string timestampColumn = HasTimeStampColumn ? $", {EscL}{TimeStampColumnName}{EscR}" : string.Empty;
        string sql = $"SELECT {EscL}{OriginalIndexColumnName}{EscR} {identityColumn} {timestampColumn} FROM {FullTempOutputTableName} WHERE {EscL}{OriginalIndexColumnName}{EscR} is not null;";
        var results = new List<IndexToGeneratedId>();

        List<IndexToGeneratedId> queryMapping = isAsync ? await LoadResultsInternalAsync().ConfigureAwait(false) : LoadResultsInternal();

        return queryMapping;

        void ReadAndAddRow(DbDataReader reader)
        {
            int index = reader.GetInt32(0);
            object id = reader.GetValue(1);
            object? timestampValue = shouldLoadAlsoTimestamp ? reader.GetValue(2) : null;
            results.Add(new IndexToGeneratedId(index, id, timestampValue));
        }

        async Task<List<IndexToGeneratedId>> LoadResultsInternalAsync()
        {
#pragma warning disable CA2007
            await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            if (command.Connection!.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            if (context.Database.CurrentTransaction != null)
            {
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            }

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ReadAndAddRow(reader);
            }

            List<IndexToGeneratedId> asyncResults = results;

            return asyncResults;
        }

        List<IndexToGeneratedId> LoadResultsInternal()
        {
            using DbCommand command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            if (command.Connection!.State != ConnectionState.Open)
            {
                command.Connection.Open();
            }

            if (context.Database.CurrentTransaction != null)
            {
                command.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            }

            using DbDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                ReadAndAddRow(reader);
            }

            List<IndexToGeneratedId> syncResults = results;

            return syncResults;
        }
    }

    internal IEnumerable QueryOutputTable(DbContext context, Type type, string sqlQuery)
    {
        Func<DbContext, IEnumerable> compiled = EF.CompileQuery(GetQueryExpression(type, sqlQuery));
        IEnumerable result = compiled(context);

        return result;
    }

    /// <summary> Returns an expression for the SQL query </summary>
    public Expression<Func<DbContext, IQueryable<T>>> GetQueryExpression<T>(string sqlQuery, bool ordered = true) where T : class
    {
        Expression<Func<DbContext, IQueryable<T>>>? expression;

        if (BulkConfig.TrackingEntities) // If Else can not be replaced with Ternary operator for Expression
        {
            expression = BulkConfig.IgnoreGlobalQueryFilters ?
                (ctx) => ctx.Set<T>().FromSqlRaw(sqlQuery).IgnoreQueryFilters() :
                (ctx) => ctx.Set<T>().FromSqlRaw(sqlQuery);
        }
        else
        {
            expression = BulkConfig.IgnoreGlobalQueryFilters ?
                (ctx) => ctx.Set<T>().FromSqlRaw(sqlQuery).AsNoTracking().IgnoreQueryFilters() :
                (ctx) => ctx.Set<T>().FromSqlRaw(sqlQuery).AsNoTracking();
        }

        return ordered ?
            Expression.Lambda<Func<DbContext, IQueryable<T>>>(OrderBy(typeof(T), expression.Body, PrimaryKeysPropertyColumnNameDict.Select(a => a.Key).ToList()), expression.Parameters) :
            expression;

        // ALTERNATIVELY OrderBy with DynamicLinq ('using System.Linq.Dynamic.Core;' NuGet required) that eliminates need for custom OrderBy<T> method with Expression.
        //var queryOrdered = query.OrderBy(PrimaryKeys[0]);
    }

    /// <summary> Returns an expression for the SQL query </summary>
    public Expression<Func<DbContext, IEnumerable>> GetQueryExpression(Type entityType, string sqlQuery, bool ordered = true)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(DbContext), "ctx");
        MethodCallExpression expression = Expression.Call(parameter, "Set", new[] { entityType });
        expression = Expression.Call(typeof(RelationalQueryableExtensions), "FromSqlRaw", new[] { entityType }, expression, Expression.Constant(sqlQuery), Expression.Constant(Array.Empty<object>()));

        if (!BulkConfig.TrackingEntities)
        {
            expression = Expression.Call(typeof(EntityFrameworkQueryableExtensions), "AsNoTracking", new[] { entityType }, expression);
        }

        if (ordered)
        {
            List<string> orderings = PrimaryKeysPropertyColumnNameDict.Select(a => a.Key).ToList();
            expression = OrderBy(entityType, expression, orderings);
        }

        Expression<Func<DbContext, IEnumerable>> lambda = Expression.Lambda<Func<DbContext, IEnumerable>>(expression, parameter);

        return lambda;

        // ALTERNATIVELY OrderBy with DynamicLinq ('using System.Linq.Dynamic.Core;' NuGet required) that eliminates need for custom OrderBy<T> method with Expression.
        //var queryOrdered = query.OrderBy(PrimaryKeys[0]);
    }

    private static MethodCallExpression OrderBy(Type entityType, Expression source, List<string> orderings)
    {
        MethodCallExpression expression = (MethodCallExpression)source;
        ParameterExpression parameter = Expression.Parameter(entityType);
        bool firstArgOrderBy = true;

        foreach (string ordering in orderings)
        {
            PropertyInfo? property = entityType.GetProperty(ordering);

            if (property != null)
            {
                MemberExpression propertyAccess = Expression.MakeMemberAccess(parameter, property);
                LambdaExpression orderByExp = Expression.Lambda(propertyAccess, parameter);
                string methodName = firstArgOrderBy ? "OrderBy" : "ThenBy";
                expression = Expression.Call(typeof(Queryable), methodName, new Type[] { entityType, property.PropertyType }, expression, Expression.Quote(orderByExp));
                firstArgOrderBy = false;
            }
        }

        return expression;
    }

    #endregion
}