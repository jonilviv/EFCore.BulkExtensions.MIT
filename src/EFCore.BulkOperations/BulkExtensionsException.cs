using System;

namespace EFCore.BulkOperations;

[Obsolete("Use BulkOperationsException instead.")]
public sealed class BulkExtensionsException : Exception
{
    public BulkOperationsExceptionType ExceptionType { get; }

    public BulkExtensionsException(BulkOperationsExceptionType exceptionType, string message) : base(message)
    {
        ExceptionType = exceptionType;
    }
}