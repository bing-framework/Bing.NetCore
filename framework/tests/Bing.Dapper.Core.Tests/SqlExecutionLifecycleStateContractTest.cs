using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 验证共享资源核心的执行生命周期状态与连接、事务状态保持独立。
/// </summary>
public sealed class SqlExecutionLifecycleStateContractTest
{
    /// <summary>
    /// 测试目的：执行租约、释放标志和同步锁应由独立值类型状态块持有。
    /// </summary>
    [Fact]
    public void SharedResourceCore_ShouldDelegateExecutionLifecycleState()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var resourcesType = typeof(SqlExecutionResources);
        var coreType = assembly.GetType("Bing.Data.Sql.SqlExecutionResourceCore");
        var lifecycleType = assembly.GetType("Bing.Data.Sql.SqlExecutionLifecycleState");
        var lifecycleField = coreType?.GetField("LifecycleState",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(coreType);
        Assert.NotNull(lifecycleType);
        Assert.NotNull(lifecycleField);
        Assert.Equal(lifecycleType, lifecycleField.FieldType);
        Assert.True(lifecycleType.IsValueType);

        foreach (var fieldName in new[] { "ExecutionLease", "ExecutionStateLock", "IsDisposed" })
        {
            Assert.Null(coreType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.NotNull(lifecycleType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
        }

        Assert.Equal(typeof(object), lifecycleType.GetField("ExecutionStateLock",
            BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
    }
}
