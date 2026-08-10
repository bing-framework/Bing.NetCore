using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Conditions;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql生成器测试 - 条件、分组与分页
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - Where、Like、In 与空值条件应按调用顺序生成参数。
    /// </summary>
    [Fact]
    public void Where_WhenMultipleConditionTypesAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select * \r\nFrom `users` \r\nWhere `Status`=@_p_0 And `Name` Like @_p_1 And `Id` In (@_p_2,@_p_3) And `DeletedAt` Is Null";

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
    /// 测试 - 空 In 与 Not In 集合应生成明确的常量条件且不生成参数。
    /// </summary>
    [Fact]
    public void InAndNotIn_WhenValuesAreEmpty_ShouldRenderConstantConditionsAndKeepParametersEmpty()
    {
        // Arrange
        const string expectedIn = "Select * \r\nFrom `users` \r\nWhere 1 = 0";
        const string expectedNotIn = "Select * \r\nFrom `users` \r\nWhere 1 = 1";

        // Act
        var inBuilder = _builder.New().From("users").In("Id", Array.Empty<object>());
        var notInBuilder = _builder.New().From("users").NotIn("Id", Array.Empty<object>());

        // Assert
        Assert.Equal(expectedIn, inBuilder.ToSql());
        Assert.Equal(expectedNotIn, notInBuilder.ToSql());
        Assert.Empty(inBuilder.GetParams());
        Assert.Empty(notInBuilder.GetParams());
    }

    /// <summary>
    /// 测试 - 空 In 常量条件应在 And 与 Or 组合中保持逻辑位置，避免筛选条件被移除。
    /// </summary>
    [Fact]
    public void In_WhenValuesAreEmptyAndCombinedWithAndOr_ShouldKeepConstantCondition()
    {
        // Arrange
        const string expected = "Select * \r\nFrom `users` \r\nWhere (`Status`=@_p_0 And 1 = 0 Or 1 = 1)";

        // Act
        var sql = _builder.New().From("users").Where("Status", "active")
            .In("Id", Array.Empty<object>()).Or(new SqlCondition("1 = 1")).ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Group By、Having 与多列排序应使用 MySql 引用格式。
    /// </summary>
    [Fact]
    public void GroupBy_WhenHavingAndOrderByAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select `Region`,Sum(`Amount`) As `Total` \r\nFrom `sales` \r\nGroup By `Region` Having Sum(Amount)>100 \r\nOrder By `Region`,`Total` Desc";

        // Act
        var sql = _builder.Select("Region").Sum("Amount", "Total").From("sales")
            .GroupBy("Region").HavingRaw("Sum(Amount)>100").OrderBy("Region").OrderBy("Total desc").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 聚合、分组、排序和分页组合时应保持 MySQL 子句顺序与分页参数。
    /// </summary>
    [Fact]
    public void GroupBy_WhenAggregateOrderAndPageAreCombined_ShouldKeepClauseAndParameterOrder()
    {
        // Arrange
        const string expected = "Select `Region`,Sum(`Amount`) As `Total` \r\nFrom `sales` \r\nGroup By `Region` Having Sum(Amount)>100 \r\nOrder By `Total` Desc \r\nLimit @_p_1 OFFSET @_p_0";

        // Act
        var sql = _builder.Select("Region").Sum("Amount", "Total").From("sales")
            .GroupBy("Region").HavingRaw("Sum(Amount)>100").OrderBy("Total desc").Skip(50).Take(25).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(50, _builder.GetParam("@_p_0"));
        Assert.Equal(25, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - Skip 和 Take 应输出参数化的 MySql Limit Offset 分页语法。
    /// </summary>
    [Fact]
    public void Page_WhenSkipAndTakeAreConfigured_ShouldRenderLimitOffset()
    {
        // Arrange
        const string expected = "Select * \r\nFrom `users` \r\nOrder By `Id` \r\nLimit @_p_1 OFFSET @_p_0";

        // Act
        var sql = _builder.From("users").OrderBy("Id").Skip(50).Take(25).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(50, _builder.GetParam("@_p_0"));
        Assert.Equal(25, _builder.GetParam("@_p_1"));
    }
}