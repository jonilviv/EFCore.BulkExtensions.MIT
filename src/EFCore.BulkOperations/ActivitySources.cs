using System.Diagnostics;
using System.Globalization;

namespace EFCore.BulkOperations;

/// <summary> Contains activity sources </summary>
public static class ActivitySources
{
    private static readonly ActivitySource __activitySource = new("EFCore.BulkOperations");

    /// <summary> Starts the activity </summary>
    public static Activity? StartExecuteActivity(OperationType operationType, int entitiesCount)
    {
        Activity? activity = __activitySource.StartActivity("EFCore.BulkOperations.BulkExecute");

        if (activity != null)
        {
            string operationTypeTag = operationType.ToString("G");
            string entitiesCountTag = entitiesCount.ToString(CultureInfo.InvariantCulture);
            activity.AddTag("operationType", operationTypeTag);
            activity.AddTag("entitiesCount", entitiesCountTag);
        }

        return activity;
    }
}