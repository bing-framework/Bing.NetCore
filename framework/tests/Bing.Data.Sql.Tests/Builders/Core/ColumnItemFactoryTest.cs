using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// ColumnItem 结构化工厂测试。
/// </summary>
public class ColumnItemFactoryTest
{
    /// <summary>
    /// 测试 - 普通列工厂应创建结构化列。
    /// </summary>
    [Fact]
    public void CreateColumn_ShouldCreateNormalColumn()
    {
        // Act
        var item = ColumnItem.CreateColumn("Id", "u", "UserId");

        // Assert
        Assert.False(item.Raw);
        Assert.Null(item.AggregateFunction);
        Assert.Equal("[u].[Id] As [UserId]", item.ToSql(TestDialect.Instance, null));
    }

    /// <summary>
    /// 测试 - 结构化聚合工厂及 Clone 应保留数据库段和 Distinct 描述。
    /// </summary>
    [Fact]
    public void CreateAggregate_WhenCloned_ShouldPreserveStructuredDescriptor()
    {
        // Arrange
        var item = ColumnItem.CreateAggregate(SqlAggregateFunction.Sum, "Amount", "o", "Total", true,
            "archive");

        // Act
        var clone = item.Clone();

        // Assert
        Assert.Equal(SqlAggregateFunction.Sum, clone.AggregateFunction);
        Assert.True(clone.AggregateDistinct);
        Assert.Equal("Sum(Distinct [archive].[o].[Amount]) As [Total]", clone.ToSql(TestDialect.Instance, null));
    }

    /// <summary>
    /// 测试 - 表达式与原始聚合工厂应保留调用方 SQL 文本。
    /// </summary>
    [Fact]
    public void CreateAggregateExpressionAndRaw_ShouldPreserveArgumentText()
    {
        // Arrange
        var expression = ColumnItem.CreateAggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount] * 2", "Total");
        var raw = ColumnItem.CreateAggregateRaw(SqlAggregateFunction.Max, "JSON_VALUE(Data, '$.score')", "Score");

        // Assert
        Assert.Equal("Sum([o].[Amount] * 2) As [Total]", expression.ToSql(TestDialect.Instance, null));
        Assert.Equal("Max(JSON_VALUE(Data, '$.score')) As [Score]", raw.ToSql(TestDialect.Instance, null));
    }
}