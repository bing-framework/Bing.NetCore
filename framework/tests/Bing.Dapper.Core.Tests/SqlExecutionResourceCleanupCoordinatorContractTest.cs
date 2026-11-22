using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 共享执行资源清理协调器的结构契约测试。
/// </summary>
public sealed class SqlExecutionResourceCleanupCoordinatorContractTest
{
    /// <summary>
    /// 测试目的：同步和异步 Root 清理应由独立的值类型协调器承载，避免资源门面继续承载两套编排流程。
    /// </summary>
    [Fact]
    public void ExecutionResources_ShouldDelegateCleanupToValueTypeCoordinator()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var resourcesType = typeof(SqlExecutionResources);
        var coordinatorType = assembly.GetType("Bing.Data.Sql.SqlExecutionResourceCleanupCoordinator");
        var coordinatorField = resourcesType.GetField("_cleanupCoordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(coordinatorType);
        Assert.True(coordinatorType.IsValueType);
        Assert.NotNull(coordinatorField);
        Assert.Equal(coordinatorType, coordinatorField.FieldType);
        Assert.Equal(typeof(void), coordinatorType.GetMethod("Dispose",
            BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType);
        Assert.Equal(typeof(ValueTask), coordinatorType.GetMethod("DisposeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)?.ReturnType);
    }
}
