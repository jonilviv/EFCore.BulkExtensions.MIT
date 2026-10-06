using System;
using EFCore.BulkOperations.SqlAdapters;
using EFCore.BulkOperations.SqlAdapters.PostgreSql;

namespace EFCore.BulkOperations.Providers;

internal static class PostgreSqlProviderRegistration
{
    internal static void Register()
    {
        Func<string, bool> matcher = CanHandle;
        Func<IDbServer> factory = Create;
        DbServerRegistry.Register(matcher, factory);
    }

    private static bool CanHandle(string providerName)
    {
        bool canHandle = providerName.EndsWith("postgresql", StringComparison.OrdinalIgnoreCase);

        return canHandle;
    }

    private static IDbServer Create()
    {
        var server = new PostgreSqlDbServer();

        return server;
    }
}