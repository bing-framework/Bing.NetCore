using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;

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
    /// 测试 - Group By、Having 与多列排序应使用 MySql 引用格式。
    /// </summary>
    [Fact]
    public void GroupBy_WhenHavingAndOrderByAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select `Region`,Sum(`Amount`) As `Total` \r\nFrom `sales` \r\nGroup By `Region` Having Sum(Amount)>100 \r\nOrder By `Region`,`Total` Desc";

        // Act
        var sql = _builder.Select("Region").Sum("Amount", "Total").From("sales")
            .GroupBy("Region", "Sum(Amount)>100").OrderBy("Region").OrderBy("Total desc").ToSql();

        // Assert
        Assert.Equal(expected, sql);
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