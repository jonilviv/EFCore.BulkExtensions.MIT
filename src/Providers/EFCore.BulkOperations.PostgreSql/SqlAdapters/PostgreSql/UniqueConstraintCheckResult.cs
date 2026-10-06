namespace EFCore.BulkOperations.SqlAdapters.PostgreSql;

internal sealed class UniqueConstraintCheckResult
{
    public UniqueConstraintCheckResult()
    {
    }

    public UniqueConstraintCheckResult(bool hasUniqueConstraint, bool connectionOpenedInternally)
    {
        HasUniqueConstraint = hasUniqueConstraint;
        ConnectionOpenedInternally = connectionOpenedInternally;
    }

    public bool HasUniqueConstraint { get; set; }

    public bool ConnectionOpenedInternally { get; set; }
}