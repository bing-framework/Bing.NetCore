using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Dapper.Tests.Builders.Clauses;

/// <summary>
/// From子句测试
/// </summary>
public class FromClauseTest
{
    /// <summary>
    /// From子句
    /// </summary>
    private readonly FromClause _clause;

    /// <summary>
    /// 初始化一个 <see cref="FromClauseTest"/> 类型的实例。
    /// </summary>
    public FromClauseTest()
    {
        _clause = (OracleFromClause)((ISqlQueryClauseAccessor)new OracleBuilder()).FromClause;
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    /// <returns>From 子句生成的 SQL 文本。</returns>
    private string GetSql() => _clause.ToSql();

    /// <summary>
    /// 默认输出空
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 设置表
    /// </summary>
    [Fact]
    public void Test_From_1()
    {
        _clause.From("a");
        Assert.Equal("From \"a\"", GetSql());
    }

    /// <summary>
    /// 设置表 - 别名
    /// </summary>
    [Fact]
    public void Test_From_2()
    {
        _clause.From("a", "b");
        Assert.Equal("From \"a\" \"b\"", GetSql());
    }

    /// <summary>
    /// 设置表 - 架构
    /// </summary>
    [Fact]
    public void Test_From_3()
    {
        _clause.From("a.b", "c");
        Assert.Equal("From \"a\".\"b\" \"c\"", GetSql());
    }

    /// <summary>
    /// 测试目的：带点物理表名必须作为单个 Oracle 标识符渲染。
    /// </summary>
    [Fact]
    public void From_WhenTableNameContainsDot_ShouldFormatAsQualifiedIdentifier()
    {
        _clause.From("Order.Log2025", "o");

        Assert.Equal("From \"Order\".\"Log2025\" \"o\"", GetSql());
    }

    /// <summary>
    /// 测试目的：Oracle 字符串限定表名应继续按句点分段渲染。
    /// </summary>
    [Fact]
    public void From_WhenSchemaAndTableAreProvided_ShouldFormatEachIdentifier()
    {
        _clause.From("SCOTT.USERS", "u");

        Assert.Equal("From \"SCOTT\".\"USERS\" \"u\"", GetSql());
    }
}
