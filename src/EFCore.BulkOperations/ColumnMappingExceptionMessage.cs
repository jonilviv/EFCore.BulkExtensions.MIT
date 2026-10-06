using System;

namespace EFCore.BulkOperations;

[Serializable]
internal sealed class ColumnMappingExceptionMessage : InvalidBulkConfigException
{
    public ColumnMappingExceptionMessage() : base(BulkExceptionMessage.ColumnMappingNotMatch) { }
}