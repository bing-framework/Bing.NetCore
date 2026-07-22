using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PgSql Sql生成器测试
/// </summary>
public class PostgreSqlBuilderTest
{
    /// <summary>
    /// PgSql Sql生成器
    /// </summary>
    private readonly PostgreSqlBuilder _builder;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public PostgreSqlBuilderTest()
    {
        _builder = new PostgreSqlBuilder();
    }

    /// <summary>
    /// 测试输出的调试SQL - 布尔值输出false，而不是0
    /// </summary>
    [Fact]
    public void Test_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From \"Test\" ");
        result.Append("Where \"A\"=1 And \"B\"=2 And \"C\"=false And \"D\"=true And \"E\"=5 And \"F\"=6 And ");
        result.Append("\"G\"=7 And \"H\"=8 And \"I\"=9 And \"J\"=10 And \"K\"=11 And \"L\"=12");

        //执行
        _builder.Select("*")
            .From("Test")
            .Where("A", 1)
            .Where("B", 2)
            .Where("C", false)
            .Where("D", true)
            .Where("E", 5)
            .Where("F", 6)
            .Where("G", 7)
            .Where("H", 8)
            .Where("I", 9)
            .Where("J", 10)
            .Where("K", 11)
            .Where("L", 12);

        //验证
        Assert.Equal(result.ToString(), _builder.ToDebugSql());
    }

    /// <summary>
    /// 测试目的：PostgreSQL 字符串表名应保留既有的分段渲染行为。
    /// </summary>
    [Fact]
    public void From_WhenTableNameContainsDot_ShouldFormatAsQualifiedIdentifier()
    {
        _builder.Select("*").From("Order.Log2025", "o");

        Assert.Equal("Select * \r\nFrom \"Order\".\"Log2025\" As \"o\"", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：PostgreSQL 字符串 From 和 Join 应继续按句点分段渲染。
    /// </summary>
    [Fact]
    public void StringQualifiedTables_ShouldKeepPostgreSqlSegmentedFormatting()
    {
        var sql = _builder.Select("u.Id")
            .From("public.users", "u")
            .Join("audit.roles", "r")
            .ToSql();

        Assert.Equal("Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\" \r\nJoin \"audit\".\"roles\" As \"r\"", sql);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 分页应使用 LIMIT 和 OFFSET，并在克隆后保持独立状态。
    /// </summary>
    [Fact]
    public void Page_ShouldRenderLimitOffsetAndKeepCloneAndNewStateIsolated()
    {
        // Arrange
        _builder.Select("u.Id").From("public.users", "u").OrderBy("u.Id").Page(new Pager(2, 10, "u.Id"));

        // Act
        var sql = _builder.ToSql();
        var cloneSql = _builder.Clone().ToSql();
        var newBuilder = _builder.New();
        newBuilder.Select("u.Id").From("public.users", "u");

        // Assert
        Assert.Equal("Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\" \r\nOrder By \"u\".\"Id\" \r\nLimit @_p_1 OFFSET @_p_0", sql);
        Assert.Equal(sql, cloneSql);
        Assert.Equal("Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\"", newBuilder.ToSql());
        Assert.Equal(10, _builder.GetParam("_p_0"));
        Assert.Equal(10, _builder.GetParam("_p_1"));
    }
}
