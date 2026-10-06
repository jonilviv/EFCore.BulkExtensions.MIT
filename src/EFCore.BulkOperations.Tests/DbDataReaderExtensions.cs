using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations.Tests;

public static class DbDataReaderExtensions
{
    public static T Field<T>(this DbDataReader reader, string columnName)
    {
        int columnIndex = reader.GetOrdinal(columnName);
        T result = reader.GetFieldValue<T>(columnIndex);

        return result;
    }

    public static async Task<T> FieldAsync<T>(this DbDataReader reader, string columnName, CancellationToken cancellationToken = default)
    {
        int columnIndex = reader.GetOrdinal(columnName);
        T result = await reader.GetFieldValueAsync<T>(columnIndex, cancellationToken);

        return result;
    }
}