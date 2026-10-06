namespace EFCore.BulkOperations.SqlAdapters.MySql;

public sealed class MySqlDialect : SqlDefaultDialect
{
    public override char EscL => '`';

    public override char EscR => '`';
}