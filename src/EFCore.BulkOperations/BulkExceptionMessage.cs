namespace EFCore.BulkOperations;

/// <summary> Provides a list of static exception messages </summary>
public static class BulkExceptionMessage
{
    /// <summary> Exception message to define column mapping does not match </summary>
    public const string ColumnMappingNotMatch = "The given ColumnMapping does not match up with any column in the source or destination";

    /// <summary> Exception message to define specified double config list is not valid </summary> 
    public const string SpecifiedDoubleConfigLists = "Only one group of properties, either {0} or {1} can be specified, specifying both not allowed.";
}