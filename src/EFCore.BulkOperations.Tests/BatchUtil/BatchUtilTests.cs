using EFCore.BulkOperations.SqlAdapters;

namespace EFCore.BulkOperations.Tests.BatchUtil;

public class BatchUtilTests
{
    [Fact]
    public void GetBatchSql_UpdateSqlite_ReturnsExpectedValues()
    {
        ContextUtil.DbServer = DbServerType.SqLite;

        using var context = new TestContext(ContextUtil.GetOptions());
        BatchSqlParts batchSqlParts = BulkOperations.BatchUtil.GetBatchSql(context.Items, context, true);

        Assert.Equal("\"Item\"", batchSqlParts.TableAlias);
        Assert.Equal(" AS \"i\"", batchSqlParts.TableAliasSuffixAs);
    }
}