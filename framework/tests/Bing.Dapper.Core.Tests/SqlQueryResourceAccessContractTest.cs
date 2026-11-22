using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 查询资源访问边界契约测试。
/// </summary>
public sealed class SqlQueryResourceAccessContractTest
{
    /// <summary>
    /// 测试目的：查询资源访问应是零分配值类型，并且只提供查询生命周期操作。
    /// </summary>
    [Fact]
    public void QueryResourceAccess_ShouldExposeQueryLifecycleOnly()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var accessType = assembly.GetType("Bing.Data.Sql.SqlQueryResourceAccess");

        Assert.NotNull(accessType);
        Assert.True(accessType.IsValueType);
        Assert.NotNull(accessType.GetMethod("GetExecutionConnection", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("GetQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("AcquireExecutionLease", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("CreateOperationCancellationLease", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(accessType.GetMethod("GetExecutionTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(accessType.GetMethod("EnsureWritableDataSource", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(accessType.GetMethod("BindConnection", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    /// <summary>
    /// 测试目的：查询基类应通过专用查询资源视图承载查询路径，而不是直接暴露资源管理器。
    /// </summary>
    [Fact]
    public void QueryBase_ShouldOwnQueryResourceAccessView()
    {
        var property = typeof(SqlQueryBase).GetProperty("QueryResources",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal("SqlQueryResourceAccess", property.PropertyType.Name);
    }

    /// <summary>
    /// 测试目的：写入资源访问应是零分配值类型，并集中暴露写入生命周期操作。
    /// </summary>
    [Fact]
    public void WriteResourceAccess_ShouldExposeWriteLifecycleOnly()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var accessType = assembly.GetType("Bing.Data.Sql.SqlWriteResourceAccess");

        Assert.NotNull(accessType);
        Assert.True(accessType.IsValueType);
        Assert.NotNull(accessType.GetMethod("EnsureWritableDataSource", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("GetExecutionConnection", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("GetExecutionTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("GetWriteTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("GetWriteTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(accessType.GetMethod("GetQueryTransaction", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("AcquireExecutionLease", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(accessType.GetMethod("CreateOperationCancellationLease", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(accessType.GetMethod("GetTransactionForDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    /// <summary>
    /// 测试目的：写入执行器应持有专用写入资源视图。
    /// </summary>
    [Fact]
    public void ExecutorBase_ShouldOwnWriteResourceAccessView()
    {
        var property = typeof(SqlExecutorBase).GetProperty("WriteResources",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal("SqlWriteResourceAccess", property.PropertyType.Name);
    }

    /// <summary>
    /// 测试目的：查询和写入视图应分别依赖窄资源所有者接口，避免重新耦合到宽资源类型。
    /// </summary>
    [Fact]
    public void ResourceAccessViews_ShouldDependOnNarrowOwnerContracts()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var queryAccess = assembly.GetType("Bing.Data.Sql.SqlQueryResourceAccess");
        var writeAccess = assembly.GetType("Bing.Data.Sql.SqlWriteResourceAccess");
        var queryOwner = assembly.GetType("Bing.Data.Sql.ISqlQueryResourceOwner");
        var writeOwner = assembly.GetType("Bing.Data.Sql.ISqlWriteResourceOwner");
        var queryResourceOwner = assembly.GetType("Bing.Data.Sql.SqlQueryResourceOwner");
        var writeResourceOwner = assembly.GetType("Bing.Data.Sql.SqlWriteResourceOwner");

        Assert.NotNull(queryAccess);
        Assert.NotNull(writeAccess);
        Assert.NotNull(queryOwner);
        Assert.NotNull(writeOwner);
        Assert.NotNull(queryResourceOwner);
        Assert.NotNull(writeResourceOwner);
        Assert.Equal(queryOwner, queryAccess.GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
        Assert.Equal(writeOwner, writeAccess.GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
        Assert.Contains(queryOwner, queryResourceOwner.GetInterfaces());
        Assert.Contains(writeOwner, writeResourceOwner.GetInterfaces());
        Assert.Equal(typeof(ISqlOperationCancellationLease), queryAccess.GetMethod("CreateOperationCancellationLease",
            BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType);
        Assert.Equal(typeof(ISqlOperationCancellationLease), writeAccess.GetMethod("CreateOperationCancellationLease",
            BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType);
        Assert.DoesNotContain(queryOwner, typeof(SqlExecutionResources).GetInterfaces());
        Assert.DoesNotContain(writeOwner, typeof(SqlExecutionResources).GetInterfaces());
    }

    /// <summary>查询与写入入口只持有自己需要的资源视图。</summary>
    [Fact]
    public void ExecutionEntrypoints_ShouldOwnOnlyTheirResourceAccess()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.Null(typeof(SqlExecutionBase).GetField("_queryResourceOwner", flags));
        Assert.Null(typeof(SqlExecutionBase).GetField("_writeResourceOwner", flags));
        Assert.Null(typeof(SqlExecutionBase).GetField("_queryResources", flags));
        Assert.Null(typeof(SqlExecutionBase).GetField("_writeResources", flags));
        Assert.NotNull(typeof(SqlQueryBase).GetField("_queryResources", flags));
        Assert.Null(typeof(SqlQueryBase).GetField("_writeResources", flags));
        Assert.NotNull(typeof(SqlMultipleQueryExecutorBase).GetField("_queryResources", flags));
        Assert.Null(typeof(SqlMultipleQueryExecutorBase).GetField("_writeResources", flags));
        Assert.NotNull(typeof(SqlExecutorBase).GetField("_writeResources", flags));
        Assert.Null(typeof(SqlExecutorBase).GetField("_queryResources", flags));
    }

    /// <summary>
    /// 测试目的：查询 owner 只能读取诊断事务，不应暴露写入路径使用的底层执行事务 getter。
    /// </summary>
    [Fact]
    public void QueryResourceOwner_ShouldExposeDiagnosticsTransactionOnly()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var queryOwner = assembly.GetType("Bing.Data.Sql.ISqlQueryResourceOwner");
        var queryResourceOwner = assembly.GetType("Bing.Data.Sql.SqlQueryResourceOwner");

        Assert.NotNull(queryOwner);
        Assert.NotNull(queryResourceOwner);
        Assert.NotNull(queryOwner.GetMethod("GetTransactionForDiagnostics"));
        Assert.NotNull(queryResourceOwner.GetMethod("GetTransactionForDiagnostics"));
        Assert.Null(queryOwner.GetMethod("GetExecutionTransaction"));
        Assert.Null(queryResourceOwner.GetMethod("GetExecutionTransaction"));
    }
}
