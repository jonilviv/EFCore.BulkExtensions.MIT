using System;

namespace EFCore.BulkOperations;

/// <summary> Custom exception class </summary>
[Serializable]
public class InvalidBulkConfigException : Exception
{
    /// <summary> Custom exception class to indicate a custom exception was triggered for BulkConfig </summary>
    public InvalidBulkConfigException() { }

    /// <summary> Custom exception class to indicate a custom exception was triggered for BulkConfig </summary>
    public InvalidBulkConfigException(string message) : base(message) { }

    /// <summary> Custom exception class to indicate a custom exception was triggered for BulkConfig </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public InvalidBulkConfigException(string message, Exception innerException) : base(message, innerException) { }
}