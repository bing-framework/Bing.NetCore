using Bing.Data.Sql.Builders.Conditions;

namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// SQL 条件工厂测试。
/// </summary>
public sealed class SqlConditionFactoryTest
{
    /// <summary>
    /// 每个受支持运算符必须生成完整且稳定的列比较 SQL。
    /// </summary>
    /// <param name="operator">比较操作符。</param>
    /// <param name="expectedSql">预期完整条件 SQL。</param>
    [Theory]
    [InlineData(Operator.Equal, "[t].[Id]=[s].[Id]")]
    [InlineData(Operator.NotEqual, "[t].[Id]<>[s].[Id]")]
    [InlineData(Operator.Greater, "[t].[Id]>[s].[Id]")]
    [InlineData(Operator.GreaterEqual, "[t].[Id]>=[s].[Id]")]
    [InlineData(Operator.Less, "[t].[Id]<[s].[Id]")]
    [InlineData(Operator.LessEqual, "[t].[Id]<=[s].[Id]")]
    [InlineData(Operator.In, "[t].[Id] In ([s].[Id])")]
    [InlineData(Operator.NotIn, "[t].[Id] Not In ([s].[Id])")]
    [InlineData(Operator.Contains, "[t].[Id] Like [s].[Id]")]
    [InlineData(Operator.Starts, "[t].[Id] Like [s].[Id]")]
    [InlineData(Operator.Ends, "[t].[Id] Like [s].[Id]")]
    public void Create_WhenOperatorIsSupported_ShouldCreateExpectedCondition(Operator @operator,
        string expectedSql)
    {
        // Arrange

        // Act
        var condition = SqlConditionFactory.Create("[t].[Id]", "[s].[Id]", @operator);

        // Assert
        Assert.Equal(expectedSql, condition.GetCondition());
    }

    /// <summary>
    /// 未定义运算符的错误只依赖数值，不需要反射读取枚举描述。
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    [InlineData(int.MinValue)]
    public void Create_WhenOperatorIsUndefined_ShouldReportValueWithoutDescriptionLookup(int value)
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            SqlConditionFactory.Create("[t].[Id]", "[s].[Id]", (Operator)value));

        Assert.Equal($"运算符 值 {value} 当前条件工厂不支持。请使用已支持的 Equal、NotEqual、Greater、GreaterEqual、Less、LessEqual、In、NotIn、Contains、Starts 或 Ends 运算符。",
            exception.Message);
    }

    /// <summary>
    /// 相等/不等空值继续生成 SQL 的空值条件。
    /// </summary>
    [Theory]
    [InlineData(Operator.Equal, "[t].[Id] Is Null")]
    [InlineData(Operator.NotEqual, "[t].[Id] Is Not Null")]
    public void Create_WhenRightOperandIsNull_ShouldPreserveNullCondition(Operator @operator, string expectedSql)
    {
        Assert.Equal(expectedSql, SqlConditionFactory.Create("[t].[Id]", null, @operator).GetCondition());
    }

}
