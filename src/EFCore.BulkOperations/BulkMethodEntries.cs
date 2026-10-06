using System;
using System.Collections.Generic;

namespace EFCore.BulkOperations;

internal sealed class BulkMethodEntries
{
    public BulkMethodEntries()
    {
        Entries = new List<object>();
    }

    public string MethodName { get; set; } = null!;

    public Type Type { get; set; } = null!;

    public List<object> Entries { get; set; }
}