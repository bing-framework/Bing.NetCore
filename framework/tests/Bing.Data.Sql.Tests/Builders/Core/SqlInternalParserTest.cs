using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// SQL 内部解析与校验器测试。
/// </summary>
public class SqlInternalParserTest
{
    /// <summary>
    /// 测试目的：标识符路径解析器应识别三段路径及各方言引用符中的转义文本。
    /// </summary>
    [Fact]
    public void IdentifierPathParser_WhenQuotedThreePartPathIsProvided_ShouldReturnLogicalSegments()
    {
        // Arrange / Act
        var parsed = SqlIdentifierPathParser.TryParse("[sales.order].\"Line\"\"Item\".`amount`", out var path);

        // Assert
        Assert.True(parsed);
        Assert.Equal("sales.order", path.DatabaseName);
        Assert.Equal("Line\"Item", path.Prefix);
        Assert.Equal("amount", path.Name);
    }

    /// <summary>
    /// 测试目的：标识符路径解析器应拒绝空段、超长路径和 SQL 语句分隔符。
    /// </summary>
    [Theory]
    [InlineData("Orders;Drop")]
    [InlineData("Orders..Id")]
    [InlineData("a.b.c.d")]
    [InlineData("[Orders")]
    public void IdentifierPathParser_WhenPathIsInvalid_ShouldReturnFalse(string value)
    {
        // Arrange / Act
        var parsed = SqlIdentifierPathParser.TryParse(value, out var path);

        // Assert
        Assert.False(parsed);
        Assert.Null(path);
    }

    /// <summary>
    /// 测试目的：聚合参数校验器仅允许 Count 使用非 Distinct 通配符参数。
    /// </summary>
    [Fact]
    public void AggregateArgumentValidator_WhenWildcardContractIsViolated_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.False(SqlAggregateArgumentValidator.ValidateWildcard(SqlAggregateFunction.Sum, "Amount", false,
            "column"));
        Assert.Throws<ArgumentException>(() => SqlAggregateArgumentValidator.ValidateWildcard(
            SqlAggregateFunction.Sum, "*", false, "column"));
        Assert.Throws<ArgumentException>(() => SqlAggregateArgumentValidator.ValidateWildcard(
            SqlAggregateFunction.Count, "*", true, "column"));
    }

    /// <summary>
    /// 测试目的：动态表引用应规范化成原子名称与别名，并拒绝语句分隔符。
    /// </summary>
    [Fact]
    public void TableNameParser_WhenValidAliasOrUnsafeInputIsProvided_ShouldParseOrReject()
    {
        // Arrange / Act
        var table = SqlTableNameParser.Parse("[Orders] as [o]", schema: "[dbo]");
        var exception = Assert.Throws<ArgumentException>(() => SqlTableNameParser.Parse("Orders; Drop Table Users"));

        // Assert
        Assert.Equal("Orders", table.TableName);
        Assert.Equal("o", table.Alias);
        Assert.Equal("dbo", table.Schema);
        Assert.Equal("table", exception.ParamName);
    }
}