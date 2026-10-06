using System;
using EFCore.BulkOperations.SqlAdapters;
using EFCore.BulkOperations.SqlAdapters.SqlServer;

namespace EFCore.BulkOperations.Providers;

internal static class SqlServerProviderRegistration
{
    internal static void Register()
    {
        Func<string, bool> matcher = CanHandle;
        Func<IDbServer> factory = Create;
        DbServerRegistry.Register(matcher, factory, isFallback: true);
    }

    private static bool CanHandle(string providerName)
    {
        bool canHandle = providerName.EndsWith("sqlserver", StringComparison.OrdinalIgnoreCase);

        return canHandle;
    }

    private static IDbServer Create()
    {
        var server = new SqlServerDbServer();

        return server;
    }
}