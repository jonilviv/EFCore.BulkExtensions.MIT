namespace EFCore.BulkOperations;

public sealed class MergeActionCounts
{
    public MergeActionCounts()
    {
    }

    public MergeActionCounts(int inserted, int updated, int deleted)
    {
        Inserted = inserted;
        Updated = updated;
        Deleted = deleted;
    }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    public int Deleted { get; set; }
}