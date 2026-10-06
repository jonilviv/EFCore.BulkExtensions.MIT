using System.Collections.Generic;
using System.Data.Common;

namespace EFCore.BulkOperations.Tests;

/// <summary> Information about an intercepted <see cref="DbCommand"/> </summary>
public sealed class DbCommandInformation
{
    public DbCommandInformation()
    {
    }

    public DbCommandInformation(IReadOnlyList<DbParameter> dbParameters, string sql)
    {
        DbParameters = dbParameters;
        Sql = sql;
    }

    public IReadOnlyList<DbParameter> DbParameters { get; set; } = null!;

    public string Sql { get; set; } = null!;
}