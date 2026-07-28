using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// 聚合表达式标识符解析器测试。
/// </summary>
public class SqlExpressionIdentifierResolverTest
{
    /// <summary>
    /// 测试 - 普通 SQL 上下文中的单段、多段和四段方括号标识符应按方言转换。
    /// </summary>
    [Theory]
    [InlineData("[Amount]", "$Amount&")]
    [InlineData("[sales].[Orders].[Amount]", "$sales&.$Orders&.$Amount&")]
    [InlineData("[database].[sales].[Orders].[Amount]", "$database&.$sales&.$Orders&.$Amount&")]
    public void Resolve_WhenBracketIdentifiersAreProvided_ShouldConvertEachIdentifier(string expression, string expected)
    {
        // Arrange / Act
        var result = SqlExpressionIdentifierResolver.Resolve(expression, TestDialect2.Instance);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 测试 - 字符串、JSON Path、双引号、反引号和注释中的方括号文本不应被处理。
    /// </summary>
    [Fact]
    public void Resolve_WhenQuotedOrCommentedTextContainsBrackets_ShouldPreserveOriginalText()
    {
        // Arrange
        const string expression = "Json_Value([payload], '$.[items][0]') + '[text]' + 'it''s [text]' + \"[quoted]\" + `[backtick]` -- [line]\n/* [block] */";

        // Act
        var result = SqlExpressionIdentifierResolver.Resolve(expression, TestDialect2.Instance);

        // Assert
        Assert.Equal("Json_Value($payload&, '$.[items][0]') + '[text]' + 'it''s [text]' + \"[quoted]\" + `[backtick]` -- [line]\n/* [block] */", result);
    }

    /// <summary>
    /// 测试 - 嵌套函数和聚合函数内部的方括号字段应转换，非标识符文本保持原样。
    /// </summary>
    [Fact]
    public void Resolve_WhenNestedAggregateExpressionIsProvided_ShouldOnlyConvertIdentifiers()
    {
        // Arrange
        const string expression = "Coalesce(Sum([Orders].[Amount]), 0) + Case When [Orders].[Status] = 'new' Then 1 Else 0 End";

        // Act
        var result = SqlExpressionIdentifierResolver.Resolve(expression, TestDialect2.Instance);

        // Assert
        Assert.Equal("Coalesce(Sum($Orders&.$Amount&), 0) + Case When $Orders&.$Status& = 'new' Then 1 Else 0 End", result);
    }

    /// <summary>
    /// 测试 - 空白和不含方括号标识符的表达式应原样返回。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Count(*) + 1")]
    public void Resolve_WhenNoBracketIdentifierExists_ShouldReturnOriginalExpression(string expression)
    {
        // Arrange / Act
        var result = SqlExpressionIdentifierResolver.Resolve(expression, TestDialect2.Instance);

        // Assert
        Assert.Equal(expression, result);
    }

    /// <summary>
    /// 测试 - 未闭合或无效词法上下文应按现有合同抛出参数异常。
    /// </summary>
    [Theory]
    [InlineData("[Amount")]
    [InlineData("[]")]
    [InlineData("[outer[inner]]")]
    [InlineData("Amount]")]
    [InlineData("'unclosed")]
    [InlineData("/* unclosed")]
    public void Resolve_WhenLexicalContextIsInvalid_ShouldThrowArgumentException(string expression)
    {
        // Arrange / Act
        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionIdentifierResolver.Resolve(expression, TestDialect2.Instance));

        // Assert
        Assert.Equal("expressionSql", exception.ParamName);
    }
}