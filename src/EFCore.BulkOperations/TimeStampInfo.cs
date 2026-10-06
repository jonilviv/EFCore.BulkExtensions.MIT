using System.Collections.Generic;

namespace EFCore.BulkOperations;

/// <summary> Provides information about entities. </summary>
public sealed class TimeStampInfo
{
    /// <summary> Indicates the number of entities skipped for an update. </summary>
    public int NumberOfSkippedForUpdate { get; set; }

    /// <summary> Output the entities. </summary>
    public List<object> EntitiesOutput { get; set; } = null!;
}