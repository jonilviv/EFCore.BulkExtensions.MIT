using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.Internal;
using System.Data.Common;

namespace EFCore.BulkOperations.SqlAdapters.PostgreSql;

/// <inheritdoc/>
public sealed class PostgreSqlDbServer : IDbServer
{
    PostgreSqlAdapter __adapter = new();

    ISqlOperationsAdapter IDbServer.Adapter => __adapter;

    PostgreSqlDialect __dialect = new();

    IQueryBuilderSpecialization IDbServer.Dialect => __dialect;

    /// <inheritdoc/>
    public DbConnection? DbConnection { get; set; }

    /// <inheritdoc/>
    public DbTransaction? DbTransaction { get; set; }

    QueryBuilderExtensions __queryBuilder = new SqlQueryBuilderPostgreSql();

    /// <inheritdoc/>
    public QueryBuilderExtensions QueryBuilder => __queryBuilder;

    bool IDbServer.PropertyHasIdentity(IProperty property)
    {
        IAnnotation? annotation = property.FindAnnotation(NpgsqlAnnotationNames.ValueGenerationStrategy);

        if (annotation == null)
        {
            return false;
        }

        NpgsqlValueGenerationStrategy? strategy = (NpgsqlValueGenerationStrategy?)annotation.Value;
        bool hasIdentity = strategy == NpgsqlValueGenerationStrategy.IdentityByDefaultColumn;

        return hasIdentity;
    }
}