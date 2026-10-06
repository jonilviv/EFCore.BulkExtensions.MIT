using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.BulkOperations;

/// <summary> Contains a list of Batch IQuerable extensions </summary>
public static class QueryableBatchExtensions
{
    // Delete methods
    #region BatchDelete
    /// <summary> Extension method to batch delete data </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public static int BatchDelete(this IQueryable query)
    {
        BatchDeleteArguments arguments = GetBatchDeleteArguments(query);
        int result = arguments.DbContext.Database.ExecuteSqlRaw(arguments.Sql, arguments.Parameters);

        return result;
    }

    /// <summary> Extension method to batch delete data </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<int> BatchDeleteAsync(this IQueryable query, CancellationToken cancellationToken = default)
    {
        BatchDeleteArguments arguments = GetBatchDeleteArguments(query);
        int result = await arguments.DbContext.Database.ExecuteSqlRawAsync(arguments.Sql, arguments.Parameters, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static BatchDeleteArguments GetBatchDeleteArguments(IQueryable query)
    {
        DbContext? context = BatchUtil.GetDbContext(query);

        if (context is null)
        {
            throw new ArgumentException("Unable to determine context");
        }

        BatchQueryResult deleteResult = BatchUtil.GetSqlDelete(query, context);
        var result = new BatchDeleteArguments(context, deleteResult.Sql, deleteResult.Parameters);

        return result;
    }
    #endregion

    // Update methods
    #region BatchUpdate
    /// <summary> Extension method to batch update data </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="updateValues"></param>
    /// <param name="updateColumns"></param>
    /// <returns></returns>
    public static int BatchUpdate<T>(this IQueryable<T> query, object updateValues, List<string>? updateColumns = null) where T : class
    {
        BatchUpdateArguments arguments = GetBatchUpdateArguments(query, updateValues, updateColumns);
        int result = arguments.DbContext.Database.ExecuteSqlRaw(arguments.Sql, arguments.Parameters);

        return result;
    }

    /// <summary> Extension method to batch update data </summary>
    /// <param name="query"></param>
    /// <param name="updateValues"></param>
    /// <param name="updateColumns"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<int> BatchUpdateAsync(this IQueryable query, object updateValues, List<string>? updateColumns = null, CancellationToken cancellationToken = default)
    {
        BatchUpdateArguments arguments = GetBatchUpdateArguments((IQueryable<object>)query, updateValues, updateColumns);
        int result = await arguments.DbContext.Database.ExecuteSqlRawAsync(arguments.Sql, arguments.Parameters, cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary> Extension method to batch update data </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="updateExpression"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static int BatchUpdate<T>(this IQueryable<T> query, Expression<Func<T, T>> updateExpression, Type? type = null) where T : class
    {
        BatchUpdateArguments arguments = GetBatchUpdateArguments(query, updateExpression: updateExpression, type: type);
        int result = arguments.DbContext.Database.ExecuteSqlRaw(arguments.Sql, arguments.Parameters);

        return result;
    }

    /// <summary> Extension method to batch update data </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="updateExpression"></param>
    /// <param name="type"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<int> BatchUpdateAsync<T>(this IQueryable<T> query, Expression<Func<T, T>> updateExpression, Type? type = null, CancellationToken cancellationToken = default) where T : class
    {
        BatchUpdateArguments arguments = GetBatchUpdateArguments(query, updateExpression: updateExpression, type: type);
        int result = await arguments.DbContext.Database.ExecuteSqlRawAsync(arguments.Sql, arguments.Parameters, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static BatchUpdateArguments GetBatchUpdateArguments<T>(IQueryable<T> query, object? updateValues = null, List<string>? updateColumns = null, Expression<Func<T, T>>? updateExpression = null, Type? type = null) where T : class
    {
        if (type == null)
        {
            type = typeof(T);
        }

        DbContext? context = BatchUtil.GetDbContext(query);

        if (context is null)
        {
            throw new ArgumentException("Unable to determine context");
        }

        BatchQueryResult updateResult;

        if (updateExpression == null)
        {
            updateResult = BatchUtil.GetSqlUpdate(query, context, type, updateValues, updateColumns);
        }
        else
        {
            updateResult = BatchUtil.GetSqlUpdate(query, context, type, updateExpression);
        }

        var result = new BatchUpdateArguments(context, updateResult.Sql, updateResult.Parameters);

        return result;
    }
    #endregion
}