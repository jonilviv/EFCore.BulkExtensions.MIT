using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data.Common;

namespace EFCore.BulkOperations.SqlAdapters.MySql;

public sealed class MySqlDbServer : IDbServer
{
    MySqlAdapter __adapter = new();

    ISqlOperationsAdapter IDbServer.Adapter => __adapter;

    MySqlDialect __dialect = new();

    IQueryBuilderSpecialization IDbServer.Dialect => __dialect;

    QueryBuilderExtensions __queryBuilder = new SqlQueryBuilderMySql();

    /// <inheritdoc/>
    public QueryBuilderExtensions QueryBuilder => __queryBuilder;

    public DbConnection? DbConnection { get; set; }

    public DbTransaction? DbTransaction { get; set; }

    bool IDbServer.PropertyHasIdentity(IProperty property)
    {
        MySqlValueGenerationStrategy strategy = MySqlPropertyExtensions.GetValueGenerationStrategy(property);
        bool hasIdentity = strategy == MySqlValueGenerationStrategy.IdentityColumn;

        return hasIdentity;
    }
}