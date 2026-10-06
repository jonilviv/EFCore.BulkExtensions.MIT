using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace EFCore.BulkOperations;

internal sealed class EntityNavigationDependency : IEquatable<EntityNavigationDependency>
{
    public EntityNavigationDependency()
    {
    }

    public EntityNavigationDependency(object entity, INavigation navigation)
    {
        Entity = entity;
        Navigation = navigation;
    }

    public object Entity { get; set; } = null!;

    public INavigation Navigation { get; set; } = null!;

    public bool Equals(EntityNavigationDependency? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        bool result = Equals(Entity, other.Entity) && Equals(Navigation, other.Navigation);

        return result;
    }

    public override bool Equals(object? obj)
    {
        bool result = Equals(obj as EntityNavigationDependency);

        return result;
    }

    public override int GetHashCode()
    {
        int result = HashCode.Combine(Entity, Navigation);

        return result;
    }
}