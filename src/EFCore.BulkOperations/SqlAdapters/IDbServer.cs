using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EFCore.BulkOperations.SqlAdapters;

public interface IDbServer
{
    ISqlOperationsAdapter Adapter { get; }

    IQueryBuilderSpecialization Dialect { get; }

    QueryBuilderExtensions QueryBuilder { get; }

    bool PropertyHasIdentity(IProperty annotation);

    DbConnection? DbConnection { get; set; }
}