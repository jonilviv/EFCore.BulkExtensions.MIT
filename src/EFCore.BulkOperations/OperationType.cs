namespace EFCore.BulkOperations;

/// <summary> Describes the operation type </summary>
public enum OperationType
{
    /// <summary> Operation to insert a list of entities </summary>
    Insert,
    /// <summary> Operation to insert or update a list of entities </summary>
    InsertOrUpdate,
    /// <summary> Operation to sync source table with a list of entities by inserting (or updating) and deleting records </summary>
    InsertOrUpdateOrDelete,
    /// <summary> Operation to update a list of entities </summary>
    Update,
    /// <summary> Operation to delete a list of entities </summary>
    Delete,
    /// <summary> Operation to read a list of entities </summary>
    Read,
    /// <summary> Operation to truncate source table </summary>
    Truncate,
    /// <summary> Operation to use Entity Change Tracker to update/insert/delete entities </summary>
    SaveChanges,
}