using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EFCore.BulkOperations;

internal sealed class BatchUpdateArguments
{
    public BatchUpdateArguments()
    {
    }

    public BatchUpdateArguments(DbContext dbContext, string sql, List<object> parameters)
    {
        DbContext = dbContext;
        Sql = sql;
        Parameters = parameters;
    }

    public DbContext DbContext { get; set; } = null!;

    public string Sql { get; set; } = null!;

    public List<object> Parameters { get; set; } = null!;
}