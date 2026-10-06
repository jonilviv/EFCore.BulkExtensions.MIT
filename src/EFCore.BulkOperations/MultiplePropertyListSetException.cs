using System;

namespace EFCore.BulkOperations;

[Serializable]
internal sealed class MultiplePropertyListSetException : InvalidBulkConfigException
{
    public MultiplePropertyListSetException() { }

    public MultiplePropertyListSetException(string propertyList1Name, string propertyList2Name)
        : base(string.Format(BulkExceptionMessage.SpecifiedDoubleConfigLists, propertyList1Name, propertyList2Name)) { }
}