using System;
using EFCore.BulkOperations.SqlAdapters;
using EFCore.BulkOperations.SqlAdapters.SQLite;

namespace EFCore.BulkOperations.Providers;

internal static class SqliteProviderRegistration
{
    internal static void Register()
    {
        Func<string, bool> matcher = CanHandle;
        Func<IDbServer> factory = Create;
        DbServerRegistry.Register(matcher, factory);
    }

    private static bool CanHandle(string providerName)
    {
        bool canHandle = providerName.EndsWith("sqlite", StringComparison.OrdinalIgnoreCase);

        return canHandle;
    }

    private static IDbServer Create()
    {
        var server = new SqlLiteDbServer();

        return server;
    }
}