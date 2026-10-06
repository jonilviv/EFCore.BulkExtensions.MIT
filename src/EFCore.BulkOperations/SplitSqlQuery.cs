namespace EFCore.BulkOperations;

public sealed class SplitSqlQuery
{
    public SplitSqlQuery()
    {
    }

    public SplitSqlQuery(string leadingComments, string mainSqlQuery)
    {
        LeadingComments = leadingComments;
        MainSqlQuery = mainSqlQuery;
    }

    public string LeadingComments { get; set; } = null!;

    public string MainSqlQuery { get; set; } = null!;
}