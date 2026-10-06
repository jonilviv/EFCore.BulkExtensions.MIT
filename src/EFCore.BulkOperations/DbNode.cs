using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EFCore.BulkOperations;

internal sealed class DbNode
{
    public DbNode()
    {
        Parents = new List<DbNode>();
        Children = new List<DbNode>();
        MethodEntries = new KeyValuePair<string, List<object>>[DbContextBulkTransactionSaveChanges.EntityStateBulkMethodDict.Count];
    }

    public Type Type { get; set; } = null!;

    public List<DbNode> Parents { get; set; }

    public List<DbNode> Children { get; set; }

    public KeyValuePair<string, List<object>>[] MethodEntries { get; private set; }

    public void AddEntry(EntityEntry entry)
    {
        if (DbContextBulkTransactionSaveChanges.EntityStateBulkMethodDict.TryGetValue(entry.State, out KeyValuePair<string, int> method))
        {
            KeyValuePair<string, List<object>> methodEntry = MethodEntries.FirstOrDefault(a => a.Key == method.Key);

            if (methodEntry.Key == null)
            {
                methodEntry = new KeyValuePair<string, List<object>>(method.Key, new List<object>());
                MethodEntries[method.Value - 1] = methodEntry;
            }

            methodEntry.Value.Add(entry.Entity);
        }
    }
}