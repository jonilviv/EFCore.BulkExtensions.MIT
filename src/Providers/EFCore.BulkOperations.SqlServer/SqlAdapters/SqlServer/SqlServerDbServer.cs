using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data.Common;

namespace EFCore.BulkOperations.SqlAdapters.SqlServer;

public sealed class SqlServerDbServer : IDbServer
{
    readonly SqlOperationsServerAdapter __adapter = new();

    ISqlOperationsAdapter IDbServer.Adapter => __adapter;

    readonly SqlServerDialect __dialect = new();

    IQueryBuilderSpecialization IDbServer.Dialect => __dialect;

    public DbConnection? DbConnection { get; set; }

    public DbTransaction? DbTransaction { get; set; }

    readonly QueryBuilderExtensions __queryBuilder = new SqlQueryBuilderSqlServer();

    public QueryBuilderExtensions QueryBuilder => __queryBuilder;

    bool IDbServer.PropertyHasIdentity(IProperty property)
    {
        SqlServerValueGenerationStrategy strategy = property.GetValueGenerationStrategy();
        bool hasIdentity = strategy == SqlServerValueGenerationStrategy.IdentityColumn;

        return hasIdentity;
    }
}