using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Xunit.Abstractions;

namespace Bing.Dapper.Tests.Builders.Clauses;

/// <summary>
/// Select子句测试
/// </summary>
public class SelectClauseTest
{
    /// <summary>
    /// Select子句
    /// </summary>
    private readonly SelectClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public SelectClauseTest(ITestOutputHelper output)
    {
        _clause = new SelectClause(null, PostgreSqlDialect.Instance, new EntityResolver(), new EntityAliasRegister());
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql() => _clause.ToSql();

    /// <summary>
    /// 添加Select子句
    /// </summary>
    [Fact]
    public void Test_AppendSql_1()
    {
        _clause.AppendSql("a");
        Assert.Equal("Select a", GetSql());
    }

    /// <summary>
    /// 添加Select子句 - 带方括号
    /// </summary>
    [Fact]
    public void Test_AppendSql_2()
    {
        _clause.AppendSql("[a].[b]");
        Assert.Equal("Select \"a\".\"b\"", GetSql());
    }
}
