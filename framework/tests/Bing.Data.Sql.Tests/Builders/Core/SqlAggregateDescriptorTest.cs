using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// 结构化聚合描述测试。
/// </summary>
public class SqlAggregateDescriptorTest
{
    /// <summary>
    /// 测试 - 描述对象应保留函数、去重、参数类型和数据库名称，并支持不可变复制。
    /// </summary>
    [Theory]
    [InlineData((int)SqlAggregateArgumentKind.Column)]
    [InlineData((int)SqlAggregateArgumentKind.Expression)]
    [InlineData((int)SqlAggregateArgumentKind.Raw)]
    [InlineData((int)SqlAggregateArgumentKind.Wildcard)]
    public void Descriptor_WhenInitialized_ShouldPreserveSemanticProperties(int argumentKindValue)
    {
        // Arrange
        var argumentKind = (SqlAggregateArgumentKind)argumentKindValue;
        var descriptor = new SqlAggregateDescriptor
        {
            Function = SqlAggregateFunction.Sum,
            Distinct = true,
            ArgumentKind = argumentKind,
            DatabaseName = "reporting"
        };

        // Act
        var copy = descriptor with { Distinct = false };

        // Assert
        Assert.Equal(SqlAggregateFunction.Sum, descriptor.Function);
        Assert.True(descriptor.Distinct);
        Assert.Equal(argumentKind, descriptor.ArgumentKind);
        Assert.Equal("reporting", descriptor.DatabaseName);
        Assert.False(copy.Distinct);
        Assert.Equal(descriptor.ArgumentKind, copy.ArgumentKind);
    }

    /// <summary>
    /// 测试 - ColumnItem 聚合工厂应将描述语义映射到公开的聚合视图。
    /// </summary>
    [Fact]
    public void ColumnItemFactory_WhenCreatingAggregate_ShouldExposeDescriptorSemantics()
    {
        // Arrange / Act
        var column = ColumnItem.CreateAggregate(SqlAggregateFunction.Count, "Id", "o", distinct: true,
            databaseName: "reporting");

        // Assert
        Assert.Equal(SqlAggregateFunction.Count, column.AggregateFunction);
        Assert.True(column.AggregateDistinct);
        Assert.False(column.AggregateWildcard);
        Assert.False(column.AggregateArgumentRaw);
    }
}