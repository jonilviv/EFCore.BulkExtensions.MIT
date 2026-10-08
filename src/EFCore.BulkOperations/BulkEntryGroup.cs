using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EFCore.BulkOperations;

internal sealed class BulkEntryGroup
{
    public Type EntityType { get; set; } = null!;

    public EntityState EntityState { get; set; }

    public List<object> Entities { get; set; } = new List<object>();
}