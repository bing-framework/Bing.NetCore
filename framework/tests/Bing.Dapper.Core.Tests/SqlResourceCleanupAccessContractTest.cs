using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 查询与写入事务清理访问边界契约测试。
/// </summary>
public sealed class SqlResourceCleanupAccessContractTest
{
    /// <summary>
    /// 测试目的：只有查询入口保留短事务清理视图，写入失败直接传播给 Uow。
    /// </summary>
    [Fact]
    public void ExecutionEntrypoints_ShouldOwnOnlyTheirCleanupView()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var queryField = typeof(SqlQueryBase).GetField("_queryResourceCleanup", flags);
        var multipleField = typeof(SqlMultipleQueryExecutorBase).GetField("_queryResourceCleanup", flags);
        var writeField = typeof(SqlExecutorBase).GetField("_writeResourceCleanup", flags);

        Assert.Null(typeof(SqlExecutionBase).GetField("_queryResourceCleanup", flags));
        Assert.Null(typeof(SqlExecutionBase).GetField("_writeResourceCleanup", flags));
        Assert.NotNull(queryField);
        Assert.NotNull(multipleField);
        Assert.Null(writeField);
        Assert.True(queryField.FieldType.IsValueType);
        Assert.Equal(queryField.FieldType, multipleField.FieldType);
        Assert.Null(typeof(SqlQueryBase).GetField("_writeResourceCleanup", flags));
        Assert.Null(typeof(SqlMultipleQueryExecutorBase).GetField("_writeResourceCleanup", flags));
        Assert.Null(typeof(SqlExecutorBase).GetField("_queryResourceCleanup", flags));
    }

    /// <summary>
    /// 测试目的：查询清理视图只能提交或回滚查询事务，不能取得写入专用连接和事务入口。
    /// </summary>
    [Fact]
    public void QueryResourceCleanup_ShouldExposeOnlyTransactionCleanupMethods()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var cleanupType = assembly.GetType("Bing.Data.Sql.SqlQueryResourceCleanup");

        Assert.NotNull(cleanupType);
        Assert.True(cleanupType.IsValueType);
        Assert.NotNull(cleanupType.GetMethod("CompleteQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(cleanupType.GetMethod("CompleteQueryTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(cleanupType.GetMethod("RollbackQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(cleanupType.GetMethod("RollbackQueryTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(cleanupType.GetMethod("EnsureWritableDataSource", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(cleanupType.GetMethod("GetExecutionTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    /// <summary>
    /// 测试目的：写入不应通过查询清理接口接管读事务，也不保留无操作的清理包装。
    /// </summary>
    [Fact]
    public void WriteOwner_ShouldNotOwnQueryTransactionCleanup()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var cleanupType = assembly.GetType("Bing.Data.Sql.SqlWriteResourceCleanup");

        Assert.Null(cleanupType);
        Assert.False(typeof(ISqlQueryResourceCleanupOwner).IsAssignableFrom(typeof(SqlWriteResourceOwner)));
        Assert.Null(typeof(SqlWriteResourceOwner).GetMethod("CompleteQueryTransaction"));
        Assert.Null(typeof(SqlWriteResourceOwner).GetMethod("RollbackQueryTransaction"));
    }

    /// <summary>
    /// 测试目的：通用查询和写入资源访问视图不应继续暴露事务清理入口，清理只能通过专用视图完成。
    /// </summary>
    [Fact]
    public void ResourceAccessViews_ShouldNotExposeTransactionCleanupMethods()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var queryAccess = assembly.GetType("Bing.Data.Sql.SqlQueryResourceAccess");
        var writeAccess = assembly.GetType("Bing.Data.Sql.SqlWriteResourceAccess");

        Assert.NotNull(queryAccess);
        Assert.NotNull(writeAccess);
        Assert.Null(queryAccess.GetMethod("CompleteQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(queryAccess.GetMethod("CompleteQueryTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(queryAccess.GetMethod("RollbackQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(queryAccess.GetMethod("RollbackQueryTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(writeAccess.GetMethod("CompleteQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(writeAccess.GetMethod("CompleteQueryTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(writeAccess.GetMethod("RollbackQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(writeAccess.GetMethod("RollbackQueryTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
    }
}
