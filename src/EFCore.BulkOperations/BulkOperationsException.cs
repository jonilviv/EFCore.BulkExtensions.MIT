using System;

namespace EFCore.BulkOperations;

public sealed class BulkOperationsException : Exception
{
    public BulkOperationsExceptionType ExceptionType { get; }

    public BulkOperationsException(BulkOperationsExceptionType exceptionType, string message) : base(message)
    {
        ExceptionType = exceptionType;
    }
}