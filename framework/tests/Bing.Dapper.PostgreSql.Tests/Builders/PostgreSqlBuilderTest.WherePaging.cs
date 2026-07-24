using Bing.Data.Enums;
using Bing.Data;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSql生成器测试 - 条件、分组与分页
/// </summary>
public partial class PostgreSqlBuilderTest
{
    /// <summary>
    /// 测试 - Where、Like、In 与空值条件应按调用顺序生成参数。
    /// </summary>
    [Fact]
    public void Where_WhenMultipleConditionTypesAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"users\" \r\nWhere \"Status\"=@_p_0 And \"Name\" Like @_p_1 And \"Id\" In (@_p_2,@_p_3) And \"DeletedAt\" Is Null";

        // Act
        var sql = _builder.From("users").Where("Status", "active").Where("Name", "admin", Operator.Contains)
            .In("Id", new object[] { 1, 2 }).IsNull("DeletedAt").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal("active", _builder.GetParam("@_p_0"));
        Assert.Equal("%admin%", _builder.GetParam("@_p_1"));
        Assert.Equal(1, _builder.GetParam("@_p_2"));
        Assert.Equal(2, _builder.GetParam("@_p_3"));
    }

    /// <summary>
    /// 测试 - 全部比较运算符应引用限定列并保持参数调用顺序。
    /// </summary>
    [Fact]
    public void Where_WhenAllComparisonOperatorsConfigured_ShouldRenderQuotedColumnsAndOrderedParameters()
    {
        // Arrange
        var identifier = Guid.Parse("4a4a4b01-3b62-4be9-b1e7-f0a4194c8e4b");
        var changedAt = new DateTimeOffset(2026, 7, 24, 8, 30, 0, TimeSpan.Zero);
        const string expected = "Select * \r\nFrom \"public\".\"users\" As \"u\" \r\nWhere \"u\".\"Id\"=@_p_0 And \"u\".\"Enabled\"<>@_p_1 And \"u\".\"Amount\">@_p_2 And \"u\".\"CreatedAt\"<@_p_3 And \"u\".\"ChangedAt\">=@_p_4 And \"u\".\"State\"<=@_p_5";

        // Act
        var sql = _builder.From("public.users", "u")
            .Where("u.Id", identifier)
            .Where("u.Enabled", true, Operator.NotEqual)
            .Where("u.Amount", 12.5m, Operator.Greater)
            .Where("u.CreatedAt", new DateTime(2026, 7, 24), Operator.Less)
            .Where("u.ChangedAt", changedAt, Operator.GreaterEqual)
            .Where("u.State", 4, Operator.LessEqual)
            .ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(identifier, _builder.GetParam("@_p_0"));
        Assert.True((bool)_builder.GetParam("@_p_1"));
        Assert.Equal(12.5m, _builder.GetParam("@_p_2"));
        Assert.Equal(new DateTime(2026, 7, 24), _builder.GetParam("@_p_3"));
        Assert.Equal(changedAt, _builder.GetParam("@_p_4"));
        Assert.Equal(4, _builder.GetParam("@_p_5"));
        Assert.Equal(6, _builder.GetParams().Count);
    }

    /// <summary>
    /// 测试 - 空 In 与 Not In 集合应忽略条件且不生成参数。
    /// </summary>
    [Fact]
    public void InAndNotIn_WhenValuesAreEmpty_ShouldOmitConditionsAndKeepParametersEmpty()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"users\"";

        // Act
        var inSql = _builder.New().From("users").In("Id", Array.Empty<object>()).ToSql();
        var notInSql = _builder.New().From("users").NotIn("Id", Array.Empty<object>()).ToSql();

        // Assert
        Assert.Equal(expected, inSql);
        Assert.Equal(expected, notInSql);
        Assert.Empty(_builder.GetParams());
    }

    /// <summary>
    /// 测试 - Group By、Having 与多列排序应使用 PostgreSql 引用格式。
    /// </summary>
    [Fact]
    public void GroupBy_WhenHavingAndOrderByAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select \"Region\",Sum(\"Amount\") As \"Total\" \r\nFrom \"sales\" \r\nGroup By \"Region\" Having Sum(Amount)>100 \r\nOrder By \"Region\",\"Total\" Desc";

        // Act
        var sql = _builder.Select("Region").Sum("Amount", "Total").From("sales")
            .GroupBy("Region", "Sum(Amount)>100").OrderBy("Region").OrderBy("Total desc").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 限定分组、Having、排序和分页组合时应保持子句及参数顺序。
    /// </summary>
    [Fact]
    public void GroupBy_WhenQualifiedGroupHavingOrderAndPageAreCombined_ShouldKeepClauseAndParameterOrder()
    {
        // Arrange
        const string expected = "Select \"u\".\"Region\",\"u\".\"Currency\",Sum(\"u\".\"Amount\") As \"Total\" \r\nFrom \"public\".\"sales\" As \"u\" \r\nWhere \"u\".\"Enabled\"=@_p_0 \r\nGroup By \"u\".\"Region\",\"u\".\"Currency\" Having Sum(\"u\".\"Amount\")>@Minimum \r\nOrder By \"u\".\"Region\",\"Total\" Desc \r\nLimit @_p_2 OFFSET @_p_1";

        // Act
        var sql = _builder.Select("u.Region,u.Currency").Sum("u.Amount", "Total")
            .From("public.sales", "u").Where("u.Enabled", true)
            .GroupBy("u.Region,u.Currency", "Sum(\"u\".\"Amount\")>@Minimum")
            .AddParam("Minimum", 100).OrderBy("u.Region").OrderBy("Total desc").Skip(20).Take(10).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.True((bool)_builder.GetParam("@_p_0"));
        Assert.Equal(100, _builder.GetParam("@Minimum"));
        Assert.Equal(20, _builder.GetParam("@_p_1"));
        Assert.Equal(10, _builder.GetParam("@_p_2"));
    }

    /// <summary>
    /// 测试 - Skip 和 Take 应输出参数化的 PostgreSql Limit Offset 分页语法。
    /// </summary>
    [Fact]
    public void Page_WhenSkipAndTakeAreConfigured_ShouldRenderLimitOffset()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"users\" \r\nOrder By \"Id\" \r\nLimit @_p_1 OFFSET @_p_0";

        // Act
        var sql = _builder.From("users").OrderBy("Id").Skip(50).Take(25).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(50, _builder.GetParam("@_p_0"));
        Assert.Equal(25, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - Where 参数应先于分页 Offset 与 Limit 参数生成。
    /// </summary>
    [Fact]
    public void Page_WhenWhereParametersExist_ShouldKeepWhereThenOffsetThenLimitParameterOrder()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"public\".\"users\" As \"u\" \r\nWhere \"u\".\"Enabled\"=@_p_0 \r\nOrder By \"u\".\"Id\" \r\nLimit @_p_2 OFFSET @_p_1";

        // Act
        var sql = _builder.From("public.users", "u").Where("u.Enabled", true).OrderBy("u.Id").Skip(20).Take(10).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.True((bool)_builder.GetParam("@_p_0"));
        Assert.Equal(20, _builder.GetParam("@_p_1"));
        Assert.Equal(10, _builder.GetParam("@_p_2"));
    }
}