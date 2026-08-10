using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL 生成器测试 - 聚合参数校验。
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 测试 - 原始聚合参数中的 JSON Path 方括号应完全保留。
    /// </summary>
    [Fact]
    public void AggregateRaw_WhenSqlContainsJsonPath_ShouldPreserveBrackets()
    {
        // Arrange
        const string expected = "Select Count(JsonExtract(o.Data, '$[0]')) As [Count] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "Count")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 原始聚合参数中的字符串方括号应完全保留。
    /// </summary>
    [Fact]
    public void AggregateRaw_WhenSqlContainsStringBrackets_ShouldPreserveText()
    {
        // Arrange
        const string expected = "Select Max('[abc]') As [Marker] \r\nFrom [Orders]";

        // Act
        var sql = _builder.AggregateRaw(SqlAggregateFunction.Max, "'[abc]'", "Marker").From("Orders").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Raw Count 通配符参数应保留调用方提供的原始空白文本。
    /// </summary>
    [Fact]
    public void AggregateRaw_WhenCountWildcardContainsWhitespace_ShouldPreserveText()
    {
        // Act
        var sql = _builder.AggregateRaw(SqlAggregateFunction.Count, " * ", "Total").From("Orders").ToSql();

        // Assert
        Assert.Equal("Select Count( * ) As [Total] \r\nFrom [Orders]", sql);
    }

    /// <summary>
    /// 测试 - 可转换聚合表达式应按方言解析方括号标识符。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectQuotes()
    {
        // Arrange
        const string expected = "Select Sum([o].[Quantity] * [o].[Price]) As [Total] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Sum, "[o].[Quantity] * [o].[Price]", "Total")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 可转换聚合表达式只应转换普通 SQL 上下文中的方括号标识符。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenSqlContainsStringsAndComments_ShouldPreserveTheirBrackets()
    {
        // Arrange
        const string expected = "Select Sum(JsonExtract([o].[Data], '$[0].name') + Case When [o].[Code]='[legacy]' Then [o].[Amount] Else 0 End /* [comment] */) As [Total] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Sum,
                "JsonExtract([o].[Data], '$[0].name') + Case When [o].[Code]='[legacy]' Then [o].[Amount] Else 0 End /* [comment] */",
                "Total")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 可转换聚合表达式应保留行注释中的方括号文本。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenSqlContainsLineComment_ShouldPreserveCommentBrackets()
    {
        // Arrange
        const string expected = "Select Sum([o].[Amount] -- [comment]\n + [o].[Tax]) As [Total] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Sum,
                "[o].[Amount] -- [comment]\n + [o].[Tax]", "Total")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 可转换聚合表达式存在未闭合上下文时应拒绝请求且不污染生成器状态。
    /// </summary>
    [Theory]
    [InlineData("[o].[Amount] /* comment")]
    [InlineData("[o].[Amount] + '[text]")]
    [InlineData("[o].[Amount] + [unfinished")]
    public void AggregateExpression_WhenSqlContextIsUnclosed_ShouldThrowWithoutChangingBuilder(string expressionSql)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            _builder.AggregateExpression(SqlAggregateFunction.Sum, expressionSql, "Total"));

        // Assert
        Assert.Equal("expressionSql", exception.ParamName);
        Assert.Empty(_builder.GetParams());
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", _builder.CountAll("Total").From("Orders").ToSql());
    }

    /// <summary>
    /// 测试 - 可转换聚合表达式应在聚合参数内部输出 Distinct。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenCaseExpressionUsesDistinct_ShouldRenderDistinctInsideAggregate()
    {
        // Arrange
        const string expected = "Select Count(Distinct Case When [o].[Enabled]=1 Then [o].[Amount] Else 0 End) As [EnabledAmount] \r\nFrom [Orders] As [o]";

        // Act
        var sql = _builder.AggregateExpression(SqlAggregateFunction.Count,
                "Case When [o].[Enabled]=1 Then [o].[Amount] Else 0 End", "EnabledAmount", distinct: true)
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 非 Count 聚合使用通配符时应拒绝请求且不污染 Builder 状态。
    /// </summary>
    [Theory]
    [InlineData(SqlAggregateFunction.Sum)]
    [InlineData(SqlAggregateFunction.Avg)]
    [InlineData(SqlAggregateFunction.Max)]
    [InlineData(SqlAggregateFunction.Min)]
    public void Aggregate_WhenNonCountFunctionUsesWildcard_ShouldThrowWithoutChangingBuilder(SqlAggregateFunction function)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Aggregate(function, " * ", "Value"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Empty(_builder.GetParams());
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", _builder.CountAll("Total").From("Orders").ToSql());
    }

    /// <summary>
    /// 测试 - Raw 和可转换表达式的精确通配符参数也应遵循 Count 通配符约束。
    /// </summary>
    [Fact]
    public void AggregateRawAndExpression_WhenNonCountFunctionUsesWildcard_ShouldThrowWithoutChangingBuilder()
    {
        // Act
        var rawException = Assert.Throws<ArgumentException>(() =>
            _builder.AggregateRaw(SqlAggregateFunction.Sum, "*", "Total"));
        var expressionException = Assert.Throws<ArgumentException>(() =>
            _builder.AggregateExpression(SqlAggregateFunction.Avg, " * ", "Average"));

        // Assert
        Assert.Equal("argumentSql", rawException.ParamName);
        Assert.Equal("expressionSql", expressionException.ParamName);
        Assert.Empty(_builder.GetParams());
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", _builder.CountAll("Total").From("Orders").ToSql());
    }

    /// <summary>
    /// 测试 - 结构化聚合只允许简单列、限定列和引用标识符。
    /// </summary>
    [Theory]
    [InlineData("Id")]
    [InlineData("u.Id")]
    [InlineData("[u].[Id]")]
    [InlineData("\"u\".\"Id\"")]
    [InlineData("`u`.`Id`")]
    public void Aggregate_WhenColumnIsStructuredIdentifier_ShouldRenderColumn(string column)
    {
        // Arrange
        var expected = column == "Id"
            ? "Select Sum([Id]) As [Total] \r\nFrom [Orders] As [u]"
            : "Select Sum([u].[Id]) As [Total] \r\nFrom [Orders] As [u]";

        // Act
        var sql = _builder.Aggregate(SqlAggregateFunction.Sum, column, "Total").From("Orders", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 结构化聚合应支持含空格和转义结束符的引用标识符。
    /// </summary>
    [Fact]
    public void Aggregate_WhenColumnContainsQuotedSpacesAndEscapedClosingQuote_ShouldRenderStructuredIdentifier()
    {
        // Arrange
        const string expected = "Select Sum([Sales Order].[Order]]Name]) As [Total] \r\nFrom [Orders]";

        // Act
        var sql = _builder.Aggregate(SqlAggregateFunction.Sum, "[Sales Order].[Order]]Name]", "Total")
            .From("Orders")
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 结构化聚合传入表达式、注释或多列时应拒绝且保留后续可用性。
    /// </summary>
    [Theory]
    [InlineData("Amount * Quantity")]
    [InlineData("TenantId,UserId")]
    [InlineData("Coalesce(Amount,0)")]
    [InlineData("Case When Enabled=1 Then Amount End")]
    [InlineData("Amount + Tax")]
    [InlineData("Amount;Drop Table Orders")]
    [InlineData("Amount -- comment")]
    [InlineData("Amount /* comment */")]
    public void Aggregate_WhenColumnIsNotSingleStructuredIdentifier_ShouldThrowWithoutChangingBuilder(string column)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Aggregate(SqlAggregateFunction.Sum, column, "Total"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Empty(_builder.GetParams());
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", _builder.CountAll("Total").From("Orders").ToSql());
    }

    /// <summary>
    /// 测试 - 结构化聚合的空列参数应拒绝且不污染 Builder 状态。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Aggregate_WhenColumnIsEmpty_ShouldThrowWithoutChangingBuilder(string column)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _builder.Aggregate(SqlAggregateFunction.Sum, column, "Total"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Empty(_builder.GetParams());
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", _builder.CountAll("Total").From("Orders").ToSql());
    }

    /// <summary>
    /// 测试 - 未定义的聚合枚举值应在所有聚合入口拒绝。
    /// </summary>
    [Fact]
    public void Aggregate_WhenFunctionIsUndefined_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.Aggregate((SqlAggregateFunction)999, "Amount", "Total"));

        // Assert
        Assert.Equal("function", exception.ParamName);
        Assert.Empty(_builder.GetParams());
    }

    /// <summary>
    /// 测试 - 原始聚合传入未定义枚举值时应拒绝。
    /// </summary>
    [Fact]
    public void AggregateRaw_WhenFunctionIsUndefined_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.AggregateRaw((SqlAggregateFunction)999, "Amount", "Total"));

        // Assert
        Assert.Equal("function", exception.ParamName);
        Assert.Empty(_builder.GetParams());
    }

    /// <summary>
    /// 测试 - 可转换表达式传入未定义枚举值时应拒绝。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenFunctionIsUndefined_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.AggregateExpression((SqlAggregateFunction)999, "[o].[Amount]", "Total"));

        // Assert
        Assert.Equal("function", exception.ParamName);
        Assert.Empty(_builder.GetParams());
    }

    /// <summary>
    /// 测试 - Count 未提供列名时应渲染 Count 通配符。
    /// </summary>
    [Fact]
    public void Count_WhenAliasIsConfigured_ShouldRenderCountWildcard()
    {
        // Act
        var sql = _builder.CountAll("Total").From("Orders").ToSql();

        // Assert
        Assert.Equal("Select Count(*) As [Total] \r\nFrom [Orders]", sql);
    }

    /// <summary>
    /// 测试 - Count 应统计指定列的非空值。
    /// </summary>
    [Fact]
    public void Count_WhenColumnIsConfigured_ShouldRenderColumnCount()
    {
        // Act
        var sql = _builder.CountColumn("o.UserId", "UserCount").From("Orders", "o").ToSql();

        // Assert
        Assert.Equal("Select Count([o].[UserId]) As [UserCount] \r\nFrom [Orders] As [o]", sql);
    }

    /// <summary>
    /// 测试 - Count 应支持聚合参数 Distinct。
    /// </summary>
    [Fact]
    public void Count_WhenDistinctIsConfigured_ShouldRenderDistinctColumnCount()
    {
        // Act
        var sql = _builder.CountColumn("o.UserId", "UserCount", distinct: true).From("Orders", "o").ToSql();

        // Assert
        Assert.Equal("Select Count(Distinct [o].[UserId]) As [UserCount] \r\nFrom [Orders] As [o]", sql);
    }

    /// <summary>
    /// 测试 - 新统一聚合 API 未指定 Alias 时不应生成结果列别名。
    /// </summary>
    [Fact]
    public void AggregateApis_WhenAliasIsNotConfigured_ShouldNotRenderAlias()
    {
        // Act
        var sql = _builder.Aggregate(SqlAggregateFunction.Sum, "o.Amount")
            .CountColumn("o.UserId")
            .AggregateRaw(SqlAggregateFunction.Max, "o.UpdatedAt")
            .AggregateExpression(SqlAggregateFunction.Min, "[o].[CreatedAt]")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal("Select Sum([o].[Amount]),Count([o].[UserId]),Max(o.UpdatedAt),Min([o].[CreatedAt]) \r\nFrom [Orders] As [o]", sql);
    }

    /// <summary>
    /// 测试 - 标准聚合 API 未指定 Alias 时不应使用叶子名称。
    /// </summary>
    [Fact]
    public void AggregateApis_WhenAliasIsNotConfigured_ShouldNotUseLeafColumnAlias()
    {
        // Act
        var sql = _builder.Sum("o.Amount")
            .Avg("o.Amount")
            .Max("o.Amount")
            .Min("o.Amount")
            .CountColumn("o.Id")
            .From("Orders", "o")
            .ToSql();

        // Assert
        Assert.Equal("Select Sum([o].[Amount]),Avg([o].[Amount]),Max([o].[Amount]),Min([o].[Amount]),Count([o].[Id]) \r\nFrom [Orders] As [o]", sql);
    }

    /// <summary>
    /// 测试 - Count 单字符串参数应明确解释为待统计列。
    /// </summary>
    [Fact]
    public void Count_WhenSingleStringIsConfigured_ShouldTreatStringAsColumn()
    {
        // Act
        var sql = _builder.CountColumn("UserId").From("Orders").ToSql();

        // Assert
        Assert.Equal("Select Count([UserId]) \r\nFrom [Orders]", sql);
    }
}