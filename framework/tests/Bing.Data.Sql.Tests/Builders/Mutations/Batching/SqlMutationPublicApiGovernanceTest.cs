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
    /// 测试目的：Provider 能力对象只应公开具有生产消费链的能力标志。
    /// </summary>
    [Fact]
    public void ProviderCapabilities_WhenPublicPropertiesAreInspected_ShouldExposeConsumedCapabilities()
    {
        // Arrange and Act
        var properties = typeof(SqlProviderCapabilities).GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(new[]
        {
            "SupportsDeleteUsing", "SupportsMultiRowValues", "SupportsMultipleResultSets", "SupportsReturning",
            "SupportsUpdateFrom"
        }, properties);
    }

    /// <summary>
    /// 测试目的：新增能力标志后必须保留原三参数和四参数 CLR 构造签名，避免破坏已编译 Provider。
    /// </summary>
    [Fact]
    public void ProviderCapabilities_WhenConstructorsAreInspected_ShouldPreserveLegacySignature()
    {
        // Arrange and Act
        var constructor = typeof(SqlProviderCapabilities).GetConstructor(new[]
        {
            typeof(bool), typeof(bool), typeof(bool)
        });

        // Assert
        Assert.NotNull(constructor);
        Assert.NotNull(typeof(SqlProviderCapabilities).GetConstructor(new[]
        {
            typeof(bool), typeof(bool), typeof(bool), typeof(bool)
        }));
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