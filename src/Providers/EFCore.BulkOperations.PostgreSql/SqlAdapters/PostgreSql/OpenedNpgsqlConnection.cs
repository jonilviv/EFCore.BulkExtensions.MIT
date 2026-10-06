using Npgsql;

namespace EFCore.BulkOperations.SqlAdapters.PostgreSql;

internal sealed class OpenedNpgsqlConnection
{
    public OpenedNpgsqlConnection()
    {
    }

    public OpenedNpgsqlConnection(NpgsqlConnection connection, bool closeInternally)
    {
        Connection = connection;
        CloseInternally = closeInternally;
    }

    public NpgsqlConnection Connection { get; set; } = null!;

    public bool CloseInternally { get; set; }
}