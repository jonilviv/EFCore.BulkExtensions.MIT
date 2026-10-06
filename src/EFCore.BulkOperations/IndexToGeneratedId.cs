namespace EFCore.BulkOperations;

internal sealed class IndexToGeneratedId
{
    public IndexToGeneratedId()
    {
    }

    public IndexToGeneratedId(int originalIndex, object generatedId, object? generatedTimestamp)
    {
        OriginalIndex = originalIndex;
        GeneratedId = generatedId;
        GeneratedTimestamp = generatedTimestamp;
    }

    public int OriginalIndex { get; set; }

    public object GeneratedId { get; set; } = null!;

    public object? GeneratedTimestamp { get; set; }
}