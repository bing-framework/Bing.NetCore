using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 子句运行上下文测试。
/// </summary>
public class SqlClauseContextTest
{
    /// <summary>
    /// 测试 - New 应共享不可变服务依赖，但使用独立的运行状态。
    /// </summary>
    [Fact]
    public void New_ShouldShareDependenciesAndUseIndependentRuntimeState()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Select("*").From("Users", "u").Where("u.Id", 1);

        // Act
        var fresh = (TestSqlBuilder)source.New();

        // Assert
        Assert.Same(source.SharedServices, fresh.SharedServices);
        Assert.NotSame(source.ParameterManager, fresh.ParameterManager);
        Assert.Empty(fresh.GetParams());
    }
}