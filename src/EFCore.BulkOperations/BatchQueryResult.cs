using System.Collections.Generic;

namespace EFCore.BulkOperations;

public sealed class BatchQueryResult
{
    public BatchQueryResult()
    {
    }

    public BatchQueryResult(string sql, List<object> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    public string Sql { get; set; } = null!;

    public List<object> Parameters { get; set; } = null!;
}