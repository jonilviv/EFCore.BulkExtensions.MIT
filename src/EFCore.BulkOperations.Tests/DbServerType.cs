using System.ComponentModel;

namespace EFCore.BulkOperations.SqlAdapters;

/// <summary> A list of database servers targeted by the EFCore.BulkOperations test suite </summary>
public enum DbServerType
{
    [Description("SqlServer")] SqlServer,
    [Description("SQLite")] SqLite,
    [Description("PostgreSql")] PostgreSql,
    [Description("MySql")] MySql,
}