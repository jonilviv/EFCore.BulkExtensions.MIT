using System;

namespace EFCore.BulkOperations.SqlAdapters;

internal sealed class DbServerRegistration
{
    private readonly Func<IDbServer> __factory;
    private readonly object __instanceLock = new();
    private IDbServer? __instance;

    public DbServerRegistration(Func<string, bool> canHandleProviderName, Func<IDbServer> factory, bool isFallback)
    {
        CanHandleProviderName = canHandleProviderName;
        __factory = factory;
        IsFallback = isFallback;
    }

    public Func<string, bool> CanHandleProviderName { get; }

    public bool IsFallback { get; }

    public IDbServer GetOrCreate()
    {
        if (__instance is not null)
        {
            return __instance;
        }

        lock (__instanceLock)
        {
            __instance ??= __factory();
        }

        return __instance;
    }
}