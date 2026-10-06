using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace EFCore.BulkOperations;

internal sealed class GraphDependency
{
    public HashSet<EntityNavigationDependency> DependsOn { get; } = new HashSet<EntityNavigationDependency>();

    public HashSet<EntityNavigationDependency> Dependents { get; } = new HashSet<EntityNavigationDependency>();
}