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
    /// 测试 - Rebind 应替换 Builder、别名注册器和参数管理器，并保留共享依赖与执行上下文。
    /// </summary>
    [Fact]
    public void Rebind_ShouldReplaceRuntimeStateAndPreserveSharedDependencies()
    {
        // Arrange
        var source = new TestSqlBuilder();
        var context = source.CreateCurrentClauseContext();
        var target = new TestSqlBuilder();
        var aliasRegister = new EntityAliasRegister();
        var parameterManager = new ParameterManager(TestDialect.Instance);

        // Act
        var rebound = context.Rebind(target, context.EntityResolver, aliasRegister, parameterManager);

        // Assert
        Assert.Same(target, rebound.Builder);
        Assert.Same(aliasRegister, rebound.AliasRegister);
        Assert.Same(parameterManager, rebound.ParameterManager);
        Assert.Same(context.EntityResolver, rebound.EntityResolver);
        Assert.Same(context.Services, rebound.Services);
        Assert.Same(context.ExecutionContext, rebound.ExecutionContext);
        Assert.Same(context.Dialect, rebound.Dialect);
    }

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