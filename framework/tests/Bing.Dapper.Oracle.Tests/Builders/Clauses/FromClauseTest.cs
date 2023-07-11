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
    /// 测试初始化
    /// </summary>
    public FromClauseTest()
    {
        _clause = new OracleFromClause(null, OracleDialect.Instance, new EntityResolver(), new EntityAliasRegister(), null);
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
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
}
