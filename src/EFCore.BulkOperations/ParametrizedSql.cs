using System.Collections.Generic;
using System.Data.Common;

namespace EFCore.BulkOperations;

public sealed class ParametrizedSql
{
    public ParametrizedSql()
    {
    }

    public ParametrizedSql(string sql, IEnumerable<DbParameter> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    public string Sql { get; set; } = null!;

    public IEnumerable<DbParameter> Parameters { get; set; } = null!;
}