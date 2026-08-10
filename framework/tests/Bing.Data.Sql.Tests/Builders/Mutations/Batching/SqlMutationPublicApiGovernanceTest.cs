using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations.Batching;

namespace Bing.Data.Sql.Tests.Builders.Mutations.Batching;

/// <summary>
/// Mutation 公共 API 治理测试。
/// </summary>
public sealed class SqlMutationPublicApiGovernanceTest
{
    /// <summary>
    /// 测试目的：删除 Insert 占位策略后应保留现有有效枚举值，避免序列化整数漂移。
    /// </summary>
    [Fact]
    public void InsertStrategy_WhenPlaceholderIsRemoved_ShouldPreserveExistingValues()
    {
        // Arrange and Act
        var values = Enum.GetValues<SqlBatchInsertStrategy>();

        // Assert
        Assert.Equal(new[]
        {
            SqlBatchInsertStrategy.Auto,
            SqlBatchInsertStrategy.MultiRowValues,
            SqlBatchInsertStrategy.PerEntity
        }, values);
        Assert.Equal(3, (int)SqlBatchInsertStrategy.PerEntity);
    }

    /// <summary>
    /// 测试目的：删除 Delete 占位策略后应保留现有有效枚举值，避免序列化整数漂移。
    /// </summary>
    [Fact]
    public void DeleteStrategy_WhenPlaceholderIsRemoved_ShouldPreserveExistingValues()
    {
        // Arrange and Act
        var values = Enum.GetValues<SqlBatchDeleteStrategy>();

        // Assert
        Assert.Equal(new[]
        {
            SqlBatchDeleteStrategy.Auto,
            SqlBatchDeleteStrategy.InPredicate,
            SqlBatchDeleteStrategy.CompositePredicate,
            SqlBatchDeleteStrategy.PerEntity
        }, values);
        Assert.Equal(4, (int)SqlBatchDeleteStrategy.PerEntity);
    }

    /// <summary>
    /// 测试目的：Provider 能力应按查询、Mutation、执行、事务、过程和资源限制责任域分组公开。
    /// </summary>
    [Fact]
    public void ProviderProfile_WhenPublicPropertiesAreInspected_ShouldExposeCapabilityDomains()
    {
        // Arrange and Act
        var properties = typeof(SqlProviderProfile).GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(new[]
        {
            "Execution", "Limits", "Mutation", "Procedure", "Query", "Transaction"
        }, properties);
    }

    /// <summary>
    /// 测试目的：Profile 各能力分组应使用无位置布尔参数的对象初始化模型。
    /// </summary>
    [Fact]
    public void ProviderProfile_WhenConstructorsAreInspected_ShouldAvoidPositionalBooleanConstructors()
    {
        // Arrange and Act
        var capabilityTypes = new[]
        {
            typeof(SqlProviderMutationCapabilities),
            typeof(SqlProviderExecutionCapabilities),
            typeof(SqlProviderTransactionCapabilities),
            typeof(SqlProviderProcedureCapabilities),
            typeof(SqlProviderLimits)
        };

        // Assert
        foreach (var capabilityType in capabilityTypes)
        {
            var constructors = capabilityType.GetConstructors();
            Assert.Single(constructors);
            Assert.Empty(constructors[0].GetParameters());
        }
    }

    /// <summary>
    /// 测试目的：默认 Planner 应作为实际实现直接使用，不再声明不可替换的占位接口。
    /// </summary>
    [Fact]
    public void BatchPlanner_WhenInterfacesAreInspected_ShouldNotExposePlaceholderSpi()
    {
        // Arrange and Act
        var interfaces = typeof(SqlMutationBatchPlanner).GetInterfaces();

        // Assert
        Assert.Empty(interfaces);
    }
}