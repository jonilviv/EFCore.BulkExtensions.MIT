using System.Collections.Generic;

namespace EFCore.BulkOperations;

public sealed class BatchSqlParts
{
    public BatchSqlParts()
    {
    }

    public BatchSqlParts(string sql, string tableAlias, string tableAliasSuffixAs, string topStatement, string leadingComments, IEnumerable<object> innerParameters)
    {
        Sql = sql;
        TableAlias = tableAlias;
        TableAliasSuffixAs = tableAliasSuffixAs;
        TopStatement = topStatement;
        LeadingComments = leadingComments;
        InnerParameters = innerParameters;
    }

    public string Sql { get; set; } = null!;

    public string TableAlias { get; set; } = null!;

    public string TableAliasSuffixAs { get; set; } = null!;

    public string TopStatement { get; set; } = null!;

    public string LeadingComments { get; set; } = null!;

    public IEnumerable<object> InnerParameters { get; set; } = null!;
}