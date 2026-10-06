namespace EFCore.BulkOperations.SqlAdapters;

public sealed class BatchTableAliasTopStatement
{
    public BatchTableAliasTopStatement()
    {
    }

    public BatchTableAliasTopStatement(string tableAlias, string topStatement)
    {
        TableAlias = tableAlias;
        TopStatement = topStatement;
    }

    public string TableAlias { get; set; } = null!;

    public string TopStatement { get; set; } = null!;
}