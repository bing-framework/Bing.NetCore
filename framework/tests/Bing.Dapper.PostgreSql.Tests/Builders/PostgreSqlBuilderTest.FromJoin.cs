using Bing.Data;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSql生成器测试 - From 与 Join 子句
/// </summary>
public partial class PostgreSqlBuilderTest
{
    /// <summary>
    /// 测试 - 带 Schema 的来源表应分段引用并保留表别名。
    /// </summary>
    [Fact]
    public void From_WhenSchemaAndAliasAreConfigured_ShouldRenderQualifiedTable()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"reporting\".\"monthly_summary\" As \"m\"";

        // Act
        var sql = _builder.From("reporting.monthly_summary", "m").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - From 子查询应合并参数且引用子查询别名。
    /// </summary>
    [Fact]
    public void From_WhenSubqueryIsConfigured_ShouldRenderSubqueryAndParameters()
    {
        // Arrange
        const string expected = "Select \"s\".\"Total\" \r\nFrom (Select Sum(\"Amount\") As \"Total\" \r\nFrom \"sales\" \r\nWhere \"TenantId\"=@_p_0) As \"s\"";
        var subquery = _builder.New().Sum("Amount", "Total").From("sales").Where("TenantId", 3);

        // Act
        var sql = _builder.Select("Total", "s").From(subquery, "s").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Single(_builder.GetParams());
        Assert.Equal(3, _builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 原始 Join 条件应连接到最后一个 Join 项。
    /// </summary>
    [Fact]
    public void Join_WhenRawOnConditionIsConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\",\"r\".\"Name\" \r\nFrom \"public\".\"users\" As \"u\" \r\nLeft Join \"audit\".\"roles\" As \"r\" On u.RoleId=r.Id";

        // Act
        var sql = _builder.Select("Id", "u").Select("Name", "r").From("public.users", "u")
            .LeftJoin("audit.roles", "r").AppendOn("u.RoleId=r.Id").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}