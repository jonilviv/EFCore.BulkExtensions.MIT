using System.Collections.Generic;

namespace EFCore.BulkOperations;

public sealed class MergeTableResult
{
    public MergeTableResult()
    {
    }

    public MergeTableResult(string sql, IEnumerable<object> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    public string Sql { get; set; } = null!;

    public IEnumerable<object> Parameters { get; set; } = null!;
}