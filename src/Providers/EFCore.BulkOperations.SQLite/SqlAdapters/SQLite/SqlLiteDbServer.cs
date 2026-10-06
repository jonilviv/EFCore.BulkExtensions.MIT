using Microsoft.EntityFrameworkCore.Metadata;
using System.Data.Common;

namespace EFCore.BulkOperations.SqlAdapters.SQLite;

public sealed class SqlLiteDbServer : IDbServer
{
    SqliteOperationsAdapter __adapter = new();
    ISqlOperationsAdapter IDbServer.Adapter => __adapter;

    SqliteDialect __dialect = new();
    IQueryBuilderSpecialization IDbServer.Dialect => __dialect;

    public DbConnection? DbConnection { get; set; }

    public DbTransaction? DbTransaction { get; set; }

    QueryBuilderExtensions __queryBuilder = new SqlQueryBuilderSqlite();
    public QueryBuilderExtensions QueryBuilder => __queryBuilder;


    bool IDbServer.PropertyHasIdentity(IProperty property) => false;
}