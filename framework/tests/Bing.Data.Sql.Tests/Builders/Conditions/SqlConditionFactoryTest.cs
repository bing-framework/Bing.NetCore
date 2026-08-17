using Bing.Data.Sql.Builders.Conditions;

namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// SQL 条件工厂测试。
/// </summary>
public sealed class SqlConditionFactoryTest
{
    /// <summary>
    /// 测试目的：列对列的 In 和 NotIn 操作符必须创建集合条件，不能进入未实现分支。
    /// </summary>
    /// <param name="operator">集合比较操作符。</param>
    /// <param name="expectedSql">预期完整条件 SQL。</param>
    [Theory]
    [InlineData(Operator.In, "[t].[Id] In ([s].[Id])")]
    [InlineData(Operator.NotIn, "[t].[Id] Not In ([s].[Id])")]
    public void Create_WhenSetOperatorIsConfigured_ShouldCreateExpectedCondition(Operator @operator,
        string expectedSql)
    {
        // Arrange

        // Act
        var condition = SqlConditionFactory.Create("[t].[Id]", "[s].[Id]", @operator);

        // Assert
        Assert.Equal(expectedSql, condition.GetCondition());
    }
}